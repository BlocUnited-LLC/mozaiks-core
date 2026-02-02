"""
FalkorDB Client
===============
Async-compatible client for FalkorDB graph database operations.
"""

import asyncio
import logging
import os
from functools import lru_cache
from typing import Any, Dict, List, Optional, Union, cast

logger = logging.getLogger(__name__)

# Optional dependency - graceful degradation if not installed
try:
    from falkordb import FalkorDB
    FALKORDB_AVAILABLE = True
except ImportError:
    FALKORDB_AVAILABLE = False
    FalkorDB = None

try:
    from falkordb.asyncio import FalkorDB as AsyncFalkorDB
    FALKORDB_ASYNC_AVAILABLE = True
except ImportError:
    FALKORDB_ASYNC_AVAILABLE = False
    AsyncFalkorDB = None


class QueryResult:
    """Wrapper for FalkorDB query results."""
    
    def __init__(self, result_set: List[List[Any]], header: List[str]):
        self.result_set = result_set
        self.header = header
    
    @property
    def rows(self) -> List[Dict[str, Any]]:
        """Convert result to list of dicts with column names as keys."""
        return [
            {self.header[i]: row[i] for i in range(len(self.header))}
            for row in self.result_set
        ]
    
    @property
    def count(self) -> int:
        """Number of result rows."""
        return len(self.result_set)
    
    def first(self) -> Optional[Dict[str, Any]]:
        """Get first result row or None."""
        rows = self.rows
        return rows[0] if rows else None
    
    def scalar(self) -> Optional[Any]:
        """Get first column of first row, or None."""
        if self.result_set and self.result_set[0]:
            return self.result_set[0][0]
        return None


class FalkorDBClient:
    """
    FalkorDB client with connection pooling and multi-tenant support.
    
    Provides async-compatible query execution with graceful degradation
    when FalkorDB is unavailable.
    
    Usage:
        client = FalkorDBClient()
        result = await client.query("mozaiks_app123", "MATCH (n) RETURN n LIMIT 10")
        for row in result.rows:
            print(row)
    """
    
    def __init__(
        self,
        host: Optional[str] = None,
        port: Optional[int] = None,
        password: Optional[str] = None,
        default_graph: str = "mozaiks"
    ):
        """
        Initialize FalkorDB client.
        
        Args:
            host: FalkorDB host (default: FALKORDB_HOST env or localhost)
            port: FalkorDB port (default: FALKORDB_PORT env or 6380)
            password: Optional password (default: FALKORDB_PASSWORD env)
            default_graph: Default graph name for queries without explicit graph
        """
        self.host = host or os.getenv("FALKORDB_HOST", "localhost")
        self.port = port or int(os.getenv("FALKORDB_PORT", "6380"))
        self.password = password or os.getenv("FALKORDB_PASSWORD", None)
        self.default_graph = default_graph
        self._db: Any = None  # FalkorDB instance (optional import)
        self._graphs: Dict[str, Any] = {}
        self._connected = False
        self._lock = asyncio.Lock()
        self._use_async = (
            os.getenv("FALKORDB_ASYNC", "false").lower() in ("1", "true", "yes", "on")
            and FALKORDB_ASYNC_AVAILABLE
        )
    
    @property
    def available(self) -> bool:
        """Check if FalkorDB library is installed."""
        return FALKORDB_AVAILABLE
    
    @property
    def connected(self) -> bool:
        """Check if currently connected to FalkorDB."""
        return self._connected
    
    async def connect(self) -> bool:
        """
        Establish connection to FalkorDB.
        
        Returns:
            True if connected successfully, False otherwise
        """
        if not FALKORDB_AVAILABLE and not FALKORDB_ASYNC_AVAILABLE:
            logger.warning("FalkorDB library not installed - graph injection disabled")
            return False
        
        async with self._lock:
            if self._connected:
                return True
            
            try:
                if self._use_async and AsyncFalkorDB is not None:
                    self._db = AsyncFalkorDB(
                        host=self.host,
                        port=self.port,
                        password=self.password,
                    )
                else:
                    if FalkorDB is None:
                        raise RuntimeError("FalkorDB client not available")
                    falkordb_cls = cast(Any, FalkorDB)
                    # Run blocking connection in thread pool
                    loop = asyncio.get_event_loop()
                    self._db = await loop.run_in_executor(
                        None,
                        lambda: falkordb_cls(
                            host=self.host,
                            port=self.port,
                            password=self.password,
                        )
                    )
                self._connected = True
                logger.info(f"Connected to FalkorDB at {self.host}:{self.port}")
                return True
            except Exception as e:
                logger.error(f"Failed to connect to FalkorDB: {e}")
                self._connected = False
                return False
    
    async def disconnect(self) -> None:
        """Close FalkorDB connection."""
        async with self._lock:
            self._db = None
            self._graphs.clear()
            self._connected = False
            logger.info("Disconnected from FalkorDB")
    
    def _get_graph(self, graph_name: str):
        """Get or create a graph reference (not async-safe, use within lock)."""
        if graph_name not in self._graphs:
            if self._db is None:
                raise RuntimeError("FalkorDB not connected")
            self._graphs[graph_name] = self._db.select_graph(graph_name)
        return self._graphs[graph_name]
    
    def _build_graph_name(self, app_id: Optional[str] = None) -> str:
        """
        Build graph name for multi-tenant isolation.
        
        Args:
            app_id: Optional app ID for tenant isolation
            
        Returns:
            Graph name (e.g., "mozaiks_app123" or "mozaiks")
        """
        if app_id:
            # Sanitize app_id for graph name
            safe_id = "".join(c if c.isalnum() or c == "_" else "_" for c in app_id)
            return f"mozaiks_{safe_id}"
        return self.default_graph
    
    async def query(
        self,
        cypher: str,
        params: Optional[Dict[str, Any]] = None,
        graph_name: Optional[str] = None,
        app_id: Optional[str] = None,
        timeout_seconds: float = 5.0
    ) -> Optional[QueryResult]:
        """
        Execute a Cypher query.
        
        Args:
            cypher: Cypher query string
            params: Query parameters
            graph_name: Explicit graph name (overrides app_id)
            app_id: App ID for multi-tenant graph selection
            timeout_seconds: Query timeout
            
        Returns:
            QueryResult or None if query fails
        """
        if not self._connected:
            if not await self.connect():
                return None
        
        effective_graph = graph_name or self._build_graph_name(app_id)
        params = params or {}
        
        try:
            loop = asyncio.get_event_loop()
            
            def execute_query_sync():
                graph = self._get_graph(effective_graph)
                result = graph.query(cypher, params)
                return QueryResult(
                    result_set=list(result.result_set) if result.result_set else [],
                    header=result.header if result.header else []
                )

            async def execute_query_async():
                graph = self._get_graph(effective_graph)
                result = await graph.query(cypher, params)
                return QueryResult(
                    result_set=list(result.result_set) if result.result_set else [],
                    header=result.header if result.header else []
                )

            if self._use_async:
                result = await asyncio.wait_for(execute_query_async(), timeout=timeout_seconds)
            else:
                result = await asyncio.wait_for(
                    loop.run_in_executor(None, execute_query_sync),
                    timeout=timeout_seconds
                )
            return result
            
        except asyncio.TimeoutError:
            logger.warning(f"FalkorDB query timed out after {timeout_seconds}s")
            return None
        except Exception as e:
            logger.error(f"FalkorDB query error: {e}")
            return None
    
    async def execute(
        self,
        cypher: str,
        params: Optional[Dict[str, Any]] = None,
        graph_name: Optional[str] = None,
        app_id: Optional[str] = None,
        timeout_seconds: float = 5.0
    ) -> bool:
        """
        Execute a Cypher mutation (write operation).
        
        Args:
            cypher: Cypher mutation string
            params: Mutation parameters
            graph_name: Explicit graph name (overrides app_id)
            app_id: App ID for multi-tenant graph selection
            timeout_seconds: Mutation timeout
            
        Returns:
            True if successful, False otherwise
        """
        result = await self.query(
            cypher, params, graph_name, app_id, timeout_seconds
        )
        return result is not None
    
    async def health_check(self) -> Dict[str, Any]:
        """
        Check FalkorDB connection health.
        
        Returns:
            Health status dict
        """
        if not FALKORDB_AVAILABLE:
            return {
                "status": "unavailable",
                "reason": "falkordb library not installed"
            }
        
        if not self._connected:
            connected = await self.connect()
            if not connected:
                return {
                    "status": "disconnected",
                    "reason": "failed to connect"
                }
        
        try:
            result = await self.query("RETURN 1 AS health", timeout_seconds=2.0)
            if result and result.scalar() == 1:
                return {
                    "status": "healthy",
                    "host": self.host,
                    "port": self.port
                }
            return {
                "status": "unhealthy",
                "reason": "health query failed"
            }
        except Exception as e:
            return {
                "status": "unhealthy",
                "reason": str(e)
            }


# Singleton instance for shared use
_client_instance: Optional[FalkorDBClient] = None


def get_falkordb_client() -> FalkorDBClient:
    """Get the singleton FalkorDB client instance."""
    global _client_instance
    if _client_instance is None:
        _client_instance = FalkorDBClient()
    return _client_instance


async def reset_falkordb_client() -> None:
    """Reset the singleton client (for testing)."""
    global _client_instance
    if _client_instance:
        await _client_instance.disconnect()
    _client_instance = None

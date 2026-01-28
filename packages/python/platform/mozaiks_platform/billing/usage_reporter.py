# core/billing/usage_reporter.py
"""
Usage Event Reporter - Reports token usage to Platform.

Batches usage events and sends them to Platform's billing endpoint.
Used for tracking token consumption for billing and analytics.
"""

import asyncio
import logging
import os
from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional, List, Dict, Any

import httpx

logger = logging.getLogger("mozaiks_core.billing.usage")


@dataclass
class UsageEvent:
    """A single usage event to report."""
    event_type: str  # "token_usage", "feature_usage", "api_call"
    app_id: str
    user_id: str
    workflow_id: Optional[str] = None
    model: Optional[str] = None
    input_tokens: int = 0
    output_tokens: int = 0
    total_tokens: int = 0
    metadata: Dict[str, Any] = field(default_factory=dict)
    timestamp: datetime = field(default_factory=datetime.utcnow)
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            "event_type": self.event_type,
            "app_id": self.app_id,
            "user_id": self.user_id,
            "timestamp": self.timestamp.isoformat() + "Z",
            "data": {
                "workflow_id": self.workflow_id,
                "model": self.model,
                "input_tokens": self.input_tokens,
                "output_tokens": self.output_tokens,
                "total_tokens": self.total_tokens,
                **self.metadata,
            },
        }


class UsageReporter:
    """
    Batched usage event reporter.
    
    Collects usage events and sends them to Platform in batches.
    Batching reduces API calls and improves performance.
    
    Configuration:
        MOZAIKS_PLATFORM_URL: Platform API base URL
        MOZAIKS_PLATFORM_CLIENT_ID: Keycloak client_id for Core service account
        MOZAIKS_PLATFORM_CLIENT_SECRET: Keycloak client_secret for Core service account
        MOZAIKS_PLATFORM_TOKEN_SCOPE: Optional OAuth2 scope string (rarely needed for Keycloak)
        MOZAIKS_USAGE_BATCH_SIZE: Max events per batch (default: 100)
        MOZAIKS_USAGE_FLUSH_INTERVAL: Seconds between flushes (default: 60)
    
    Example:
        reporter = UsageReporter()
        await reporter.start()
        
        await reporter.report(UsageEvent(
            event_type="token_usage",
            app_id="app_123",
            user_id="user_456",
            model="gpt-4",
            input_tokens=1500,
            output_tokens=800,
            total_tokens=2300,
        ))
        
        await reporter.stop()  # Flushes remaining events
    """
    
    def __init__(
        self,
        platform_url: Optional[str] = None,
        client_id: Optional[str] = None,
        client_secret: Optional[str] = None,
        token_scope: Optional[str] = None,
        batch_size: int = 100,
        flush_interval: float = 60.0,
        enabled: bool = True,
    ):
        """
        Initialize the usage reporter.
        
        Args:
            platform_url: Platform API base URL
            client_id: Keycloak client_id for Core service account
            client_secret: Keycloak client_secret for Core service account
            token_scope: Optional OAuth2 scope (provider-specific)
            batch_size: Max events per batch before auto-flush
            flush_interval: Seconds between automatic flushes
            enabled: Whether to actually send events (False for self-hosted)
        """
        self._platform_url = (
            platform_url 
            or os.getenv("MOZAIKS_PLATFORM_URL", "")
        ).rstrip("/")
        
        self._client_id = client_id or os.getenv("MOZAIKS_PLATFORM_CLIENT_ID")
        self._client_secret = client_secret or os.getenv("MOZAIKS_PLATFORM_CLIENT_SECRET")
        self._token_scope = token_scope or os.getenv("MOZAIKS_PLATFORM_TOKEN_SCOPE")
        self._batch_size = int(os.getenv("MOZAIKS_USAGE_BATCH_SIZE", str(batch_size)))
        self._flush_interval = float(os.getenv("MOZAIKS_USAGE_FLUSH_INTERVAL", str(flush_interval)))
        self._enabled = (
            enabled
            and bool(self._platform_url)
            and bool((self._client_id or "").strip())
            and bool((self._client_secret or "").strip())
        )

        self._token_provider = None
        
        # Buffer for events
        self._buffer: List[UsageEvent] = []
        self._buffer_lock = asyncio.Lock()
        
        # Flush task
        self._flush_task: Optional[asyncio.Task] = None
        self._running = False
        
        # HTTP client
        self._client: Optional[httpx.AsyncClient] = None
        
        # Stats
        self._events_sent = 0
        self._events_failed = 0
        self._last_flush: Optional[datetime] = None
    
    async def start(self) -> None:
        """Start the background flush task."""
        if not self._enabled:
            logger.info("Usage reporter disabled (no platform URL)")
            return
        
        if self._running:
            return
        
        self._running = True
        from mozaiks_ai.runtime.auth.client_credentials import ClientCredentialsTokenProvider

        self._token_provider = ClientCredentialsTokenProvider(
            client_id=self._client_id or "",
            client_secret=self._client_secret or "",
            scope=self._token_scope,
        )
        self._client = httpx.AsyncClient(
            base_url=self._platform_url,
            timeout=30.0,
        )
        self._flush_task = asyncio.create_task(self._flush_loop())
        logger.info(
            "Usage reporter started (batch_size=%d, flush_interval=%.1fs)",
            self._batch_size, self._flush_interval
        )
    
    async def stop(self) -> None:
        """Stop the reporter and flush remaining events."""
        self._running = False
        
        if self._flush_task:
            self._flush_task.cancel()
            try:
                await self._flush_task
            except asyncio.CancelledError:
                pass
        
        # Final flush
        await self._flush()
        
        if self._client:
            await self._client.aclose()
            self._client = None
        
        logger.info(
            "Usage reporter stopped (sent=%d, failed=%d)",
            self._events_sent, self._events_failed
        )
    
    async def report(self, event: UsageEvent) -> None:
        """
        Report a usage event.
        
        Event is added to buffer and may trigger immediate flush
        if buffer exceeds batch size.
        
        Args:
            event: Usage event to report
        """
        if not self._enabled:
            logger.debug("Usage event ignored (reporter disabled): %s", event.event_type)
            return
        
        async with self._buffer_lock:
            self._buffer.append(event)
            
            if len(self._buffer) >= self._batch_size:
                logger.debug("Buffer full, triggering flush")
                await self._flush_locked()
    
    async def report_token_usage(
        self,
        app_id: str,
        user_id: str,
        model: str,
        input_tokens: int,
        output_tokens: int,
        workflow_id: Optional[str] = None,
        **metadata,
    ) -> None:
        """
        Convenience method to report token usage.
        
        Args:
            app_id: Application identifier
            user_id: User identifier
            model: Model used (e.g., "gpt-4")
            input_tokens: Number of input tokens
            output_tokens: Number of output tokens
            workflow_id: Optional workflow identifier
            **metadata: Additional metadata
        """
        await self.report(UsageEvent(
            event_type="token_usage",
            app_id=app_id,
            user_id=user_id,
            workflow_id=workflow_id,
            model=model,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
            total_tokens=input_tokens + output_tokens,
            metadata=metadata,
        ))
    
    async def _flush_loop(self) -> None:
        """Background task that flushes periodically."""
        while self._running:
            try:
                await asyncio.sleep(self._flush_interval)
                
                async with self._buffer_lock:
                    if self._buffer:
                        await self._flush_locked()
                        
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error("Flush loop error: %s", e)
    
    async def _flush(self) -> None:
        """Flush events (with lock acquisition)."""
        async with self._buffer_lock:
            await self._flush_locked()
    
    async def _flush_locked(self) -> None:
        """Flush events (must hold lock)."""
        if not self._buffer:
            return
        
        events = self._buffer.copy()
        self._buffer.clear()
        
        try:
            await self._send_events(events)
            self._events_sent += len(events)
            self._last_flush = datetime.utcnow()
            logger.debug("Flushed %d events", len(events))
            
        except Exception as e:
            logger.error("Failed to send %d events: %s", len(events), e)
            self._events_failed += len(events)
            
            # Re-add events to buffer for retry (up to limit)
            remaining_capacity = self._batch_size * 2 - len(self._buffer)
            if remaining_capacity > 0:
                self._buffer.extend(events[:remaining_capacity])
                logger.debug("Re-queued %d events for retry", min(len(events), remaining_capacity))
    
    async def _send_events(self, events: List[UsageEvent]) -> None:
        """Send events to Platform using Keycloak client-credentials JWT."""
        if not self._client:
            raise RuntimeError("Reporter not started")

        if not self._token_provider or not self._token_provider.is_configured():
            raise RuntimeError("No client credentials configured")

        access_token = await self._token_provider.get_access_token()
        
        payload = {
            "events": [e.to_dict() for e in events],
        }
        
        response = await self._client.post(
            "/api/billing/usage-events",
            json=payload,
            headers={
                "Authorization": f"Bearer {access_token}",
                "X-Mozaiks-Service": "core",
            },
        )
        response.raise_for_status()
    
    @property
    def stats(self) -> Dict[str, Any]:
        """Get reporter statistics."""
        return {
            "enabled": self._enabled,
            "running": self._running,
            "buffer_size": len(self._buffer),
            "events_sent": self._events_sent,
            "events_failed": self._events_failed,
            "last_flush": self._last_flush.isoformat() if self._last_flush else None,
        }


# Global instance for convenience
_global_reporter: Optional[UsageReporter] = None


def get_usage_reporter() -> UsageReporter:
    """Get the global usage reporter instance."""
    global _global_reporter
    if _global_reporter is None:
        _global_reporter = UsageReporter()
    return _global_reporter


async def report_token_usage(
    app_id: str,
    user_id: str,
    model: str,
    input_tokens: int,
    output_tokens: int,
    workflow_id: Optional[str] = None,
    **metadata,
) -> None:
    """
    Report token usage via global reporter.
    
    Convenience function for quick reporting without managing reporter lifecycle.
    """
    reporter = get_usage_reporter()
    await reporter.report_token_usage(
        app_id=app_id,
        user_id=user_id,
        model=model,
        input_tokens=input_tokens,
        output_tokens=output_tokens,
        workflow_id=workflow_id,
        **metadata,
    )

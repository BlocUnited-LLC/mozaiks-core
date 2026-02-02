"""
FalkorDB Startup Service
========================
Optional runtime startup service for initializing FalkorDB.

This service is designed to be used as a runtime_extension startup_service:

runtime_extensions:
  - kind: startup_service
    entrypoint: mozaiks_ai.runtime.graph.service:FalkorDBStartupService

Enable via env:
  FALKORDB_STARTUP_ENABLED=true
"""

import os
import logging
from typing import Optional

from .client import get_falkordb_client, FalkorDBClient

logger = logging.getLogger(__name__)


class FalkorDBStartupService:
    """Runtime startup service to initialize FalkorDB connections."""

    def __init__(self, client: Optional[FalkorDBClient] = None):
        self._client = client or get_falkordb_client()
        self._enabled = os.getenv("FALKORDB_STARTUP_ENABLED", "false").lower() in (
            "1",
            "true",
            "yes",
            "on",
        )
        self._connected = False

    async def start(self) -> None:
        """Start service by connecting to FalkorDB if enabled."""
        if not self._enabled:
            logger.info("FalkorDBStartupService disabled (FALKORDB_STARTUP_ENABLED=false)")
            return

        if not self._client.available:
            logger.warning("FalkorDBStartupService: falkordb library not installed")
            return

        try:
            self._connected = await self._client.connect()
            if self._connected:
                logger.info("FalkorDBStartupService connected successfully")
            else:
                logger.warning("FalkorDBStartupService failed to connect")
        except Exception as exc:
            logger.error(f"FalkorDBStartupService error: {exc}")

    async def stop(self) -> None:
        """Stop service by disconnecting from FalkorDB."""
        if self._connected:
            try:
                await self._client.disconnect()
            except Exception as exc:
                logger.debug(f"FalkorDBStartupService disconnect failed: {exc}")
            finally:
                self._connected = False

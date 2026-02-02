"""
Entitlement Security
====================
Security hardening for the entitlement system:
- Signature verification for entitlement manifests
- Audit logging for all capability checks
- Tenant isolation validation
- Anomaly detection for suspicious patterns
"""

import hashlib
import hmac
import json
import logging
import os
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Union

logger = logging.getLogger(__name__)

# Dedicated security audit logger
audit_logger = logging.getLogger("mozaiks.security.audit")


# ============================================================================
# Signature Verification
# ============================================================================

class EntitlementSignatureError(Exception):
    """Raised when entitlement signature verification fails."""
    
    def __init__(self, message: str, manifest_id: Optional[str] = None):
        self.manifest_id = manifest_id
        super().__init__(message)


class EntitlementSigner:
    """
    Signs and verifies entitlement manifests using HMAC-SHA256.
    
    Platform signs manifests before sending to Core.
    Core verifies signatures before trusting entitlements.
    
    In production, the signing key should come from a secure vault.
    In development/OSS, signature verification can be disabled.
    """
    
    # Environment variable for signing key
    SIGNING_KEY_ENV = "MOZAIKS_ENTITLEMENT_SIGNING_KEY"
    
    def __init__(self, signing_key: Optional[str] = None):
        """
        Initialize signer with key.
        
        Args:
            signing_key: HMAC signing key. Falls back to env var.
        """
        self._key = signing_key or os.getenv(self.SIGNING_KEY_ENV)
        self._enabled = self._key is not None
        
        if not self._enabled:
            logger.warning(
                "[SECURITY] Entitlement signature verification DISABLED. "
                f"Set {self.SIGNING_KEY_ENV} to enable."
            )
    
    @property
    def enabled(self) -> bool:
        """Whether signature verification is enabled."""
        return self._enabled
    
    def sign(self, manifest: Dict[str, Any]) -> str:
        """
        Generate signature for an entitlement manifest.
        
        Args:
            manifest: Entitlement manifest dict
            
        Returns:
            Base64-encoded HMAC-SHA256 signature
        """
        if not self._enabled or self._key is None:
            return ""
        
        # Canonicalize JSON (sorted keys, no whitespace)
        canonical = json.dumps(manifest, sort_keys=True, separators=(",", ":"))
        
        # HMAC-SHA256
        signature = hmac.new(
            self._key.encode("utf-8"),
            canonical.encode("utf-8"),
            hashlib.sha256
        ).hexdigest()
        
        return signature
    
    def verify(self, manifest: Dict[str, Any], signature: str) -> bool:
        """
        Verify signature on an entitlement manifest.
        
        Args:
            manifest: Entitlement manifest dict (without signature field)
            signature: Expected signature
            
        Returns:
            True if valid
            
        Raises:
            EntitlementSignatureError: If verification fails and strict mode
        """
        if not self._enabled:
            return True  # Skip verification if disabled
        
        # Remove signature from manifest copy for verification
        manifest_copy = {k: v for k, v in manifest.items() if k != "_signature"}
        
        expected = self.sign(manifest_copy)
        
        # Constant-time comparison to prevent timing attacks
        return hmac.compare_digest(expected, signature)
    
    def sign_manifest(self, manifest: Dict[str, Any]) -> Dict[str, Any]:
        """
        Add signature to manifest.
        
        Args:
            manifest: Entitlement manifest
            
        Returns:
            Manifest with _signature field
        """
        if not self._enabled:
            return manifest
        
        manifest_copy = dict(manifest)
        manifest_copy["_signature"] = self.sign(manifest)
        return manifest_copy
    
    def verify_manifest(
        self,
        manifest: Dict[str, Any],
        strict: bool = True
    ) -> bool:
        """
        Verify a signed manifest.
        
        Args:
            manifest: Manifest with _signature field
            strict: If True, raise on failure
            
        Returns:
            True if valid
            
        Raises:
            EntitlementSignatureError: If invalid and strict=True
        """
        if not self._enabled:
            return True
        
        signature = manifest.get("_signature")
        
        if not signature:
            if strict:
                raise EntitlementSignatureError(
                    "Missing signature on entitlement manifest",
                    manifest_id=manifest.get("app_id")
                )
            logger.warning("[SECURITY] Unsigned entitlement manifest")
            return False
        
        if not self.verify(manifest, signature):
            if strict:
                raise EntitlementSignatureError(
                    "Invalid signature on entitlement manifest",
                    manifest_id=manifest.get("app_id")
                )
            logger.error("[SECURITY] INVALID entitlement signature!")
            return False
        
        return True


# ============================================================================
# Audit Logging
# ============================================================================

@dataclass
class AuditEvent:
    """Structured audit event for security logging."""
    
    timestamp: str
    event_type: str
    app_id: str
    user_id: Optional[str]
    capability_id: Optional[str]
    result: str  # "allowed", "denied", "error"
    context: Dict[str, Any] = field(default_factory=dict)
    
    def to_dict(self) -> Dict[str, Any]:
        return {
            "timestamp": self.timestamp,
            "event_type": self.event_type,
            "app_id": self.app_id,
            "user_id": self.user_id,
            "capability_id": self.capability_id,
            "result": self.result,
            "context": self.context,
        }
    
    def to_json(self) -> str:
        return json.dumps(self.to_dict())


class SecurityAuditLogger:
    """
    Logs all capability checks for security audit trail.
    
    Logs include:
    - All capability evaluations (allowed/denied)
    - Signature verification results
    - Tenant isolation violations
    - Anomalous patterns
    """
    
    def __init__(self, app_id: str, user_id: Optional[str] = None):
        self.app_id = app_id
        self.user_id = user_id
        self._events: List[AuditEvent] = []
    
    def _log(self, event: AuditEvent) -> None:
        """Log an audit event."""
        self._events.append(event)
        
        # Log to dedicated audit logger
        log_line = f"[AUDIT] {event.to_json()}"
        
        if event.result == "denied":
            audit_logger.warning(log_line)
        elif event.result == "error":
            audit_logger.error(log_line)
        else:
            audit_logger.info(log_line)
    
    def log_capability_check(
        self,
        capability_id: str,
        allowed: bool,
        context: Optional[Dict[str, Any]] = None
    ) -> None:
        """Log a capability check."""
        self._log(AuditEvent(
            timestamp=datetime.now(timezone.utc).isoformat(),
            event_type="capability_check",
            app_id=self.app_id,
            user_id=self.user_id,
            capability_id=capability_id,
            result="allowed" if allowed else "denied",
            context=context or {},
        ))
    
    def log_limit_check(
        self,
        limit_id: str,
        allowed: bool,
        current_usage: Union[int, float],
        limit_value: Union[int, float]
    ) -> None:
        """Log a limit check."""
        self._log(AuditEvent(
            timestamp=datetime.now(timezone.utc).isoformat(),
            event_type="limit_check",
            app_id=self.app_id,
            user_id=self.user_id,
            capability_id=limit_id,
            result="allowed" if allowed else "denied",
            context={
                "current_usage": current_usage,
                "limit_value": limit_value,
                "utilization_pct": (current_usage / limit_value * 100) if limit_value else 0,
            },
        ))
    
    def log_signature_verification(
        self,
        valid: bool,
        manifest_source: str
    ) -> None:
        """Log signature verification result."""
        self._log(AuditEvent(
            timestamp=datetime.now(timezone.utc).isoformat(),
            event_type="signature_verification",
            app_id=self.app_id,
            user_id=self.user_id,
            capability_id=None,
            result="allowed" if valid else "denied",
            context={"manifest_source": manifest_source},
        ))
    
    def log_tenant_isolation_check(
        self,
        requested_app_id: str,
        allowed: bool
    ) -> None:
        """Log tenant isolation check."""
        self._log(AuditEvent(
            timestamp=datetime.now(timezone.utc).isoformat(),
            event_type="tenant_isolation",
            app_id=self.app_id,
            user_id=self.user_id,
            capability_id=None,
            result="allowed" if allowed else "denied",
            context={
                "requested_app_id": requested_app_id,
                "actual_app_id": self.app_id,
            },
        ))
    
    def log_anomaly(
        self,
        anomaly_type: str,
        details: Dict[str, Any]
    ) -> None:
        """Log an anomalous pattern."""
        self._log(AuditEvent(
            timestamp=datetime.now(timezone.utc).isoformat(),
            event_type="anomaly_detected",
            app_id=self.app_id,
            user_id=self.user_id,
            capability_id=None,
            result="error",
            context={
                "anomaly_type": anomaly_type,
                **details,
            },
        ))
    
    @property
    def events(self) -> List[AuditEvent]:
        """Get all audit events (for testing/inspection)."""
        return list(self._events)
    
    def get_denial_count(self) -> int:
        """Count denied events (for anomaly detection)."""
        return sum(1 for e in self._events if e.result == "denied")


# ============================================================================
# Tenant Isolation
# ============================================================================

class TenantIsolationError(Exception):
    """Raised when tenant isolation is violated."""
    
    def __init__(
        self,
        requested_app_id: str,
        actual_app_id: str,
        message: Optional[str] = None
    ):
        self.requested_app_id = requested_app_id
        self.actual_app_id = actual_app_id
        
        default_msg = (
            f"Tenant isolation violation: "
            f"requested={requested_app_id}, actual={actual_app_id}"
        )
        super().__init__(message or default_msg)


class TenantIsolationValidator:
    """
    Validates tenant isolation for all operations.
    
    Ensures:
    - Requests only access their own app's data
    - Cross-tenant access is blocked
    - Audit trail for isolation checks
    """
    
    def __init__(self, app_id: str, audit_logger: Optional[SecurityAuditLogger] = None):
        self.app_id = app_id
        self._audit = audit_logger
    
    def validate(
        self,
        requested_app_id: str,
        strict: bool = True
    ) -> bool:
        """
        Validate that requested app matches actual app.
        
        Args:
            requested_app_id: App ID in the request
            strict: If True, raise on violation
            
        Returns:
            True if valid
            
        Raises:
            TenantIsolationError: If violated and strict=True
        """
        allowed = requested_app_id == self.app_id
        
        if self._audit:
            self._audit.log_tenant_isolation_check(requested_app_id, allowed)
        
        if not allowed:
            logger.error(
                f"[SECURITY] TENANT ISOLATION VIOLATION: "
                f"requested={requested_app_id}, actual={self.app_id}"
            )
            
            if strict:
                raise TenantIsolationError(requested_app_id, self.app_id)
        
        return allowed
    
    def validate_resource(
        self,
        resource_app_id: str,
        resource_type: str,
        resource_id: str,
        strict: bool = True
    ) -> bool:
        """
        Validate access to a specific resource.
        
        Args:
            resource_app_id: App ID that owns the resource
            resource_type: Type of resource (workflow, tool, etc.)
            resource_id: Resource identifier
            strict: If True, raise on violation
            
        Returns:
            True if valid
        """
        allowed = resource_app_id == self.app_id
        
        if self._audit:
            self._audit.log_tenant_isolation_check(
                requested_app_id=resource_app_id,
                allowed=allowed
            )
        
        if not allowed:
            logger.error(
                f"[SECURITY] RESOURCE ACCESS VIOLATION: "
                f"app={self.app_id} tried to access {resource_type}:{resource_id} "
                f"owned by {resource_app_id}"
            )
            
            if strict:
                raise TenantIsolationError(
                    resource_app_id,
                    self.app_id,
                    f"Cannot access {resource_type}:{resource_id} from another tenant"
                )
        
        return allowed


# ============================================================================
# Anomaly Detection
# ============================================================================

class AnomalyDetector:
    """
    Detects suspicious patterns in capability usage.
    
    Monitors for:
    - Rapid repeated denials (brute force)
    - Unusual capability requests
    - Limit exhaustion patterns
    """
    
    # Thresholds
    MAX_DENIALS_PER_MINUTE = 10
    MAX_DENIALS_PER_SESSION = 50
    
    def __init__(self, audit_logger: SecurityAuditLogger):
        self._audit = audit_logger
        self._denial_times: List[float] = []
    
    def record_denial(self) -> None:
        """Record a capability denial."""
        self._denial_times.append(time.time())
        self._check_anomalies()
    
    def _check_anomalies(self) -> None:
        """Check for anomalous patterns."""
        now = time.time()
        
        # Check denials per minute
        recent_denials = [t for t in self._denial_times if now - t < 60]
        if len(recent_denials) > self.MAX_DENIALS_PER_MINUTE:
            self._audit.log_anomaly(
                anomaly_type="rapid_denials",
                details={
                    "denials_per_minute": len(recent_denials),
                    "threshold": self.MAX_DENIALS_PER_MINUTE,
                }
            )
        
        # Check total session denials
        if len(self._denial_times) > self.MAX_DENIALS_PER_SESSION:
            self._audit.log_anomaly(
                anomaly_type="excessive_session_denials",
                details={
                    "total_denials": len(self._denial_times),
                    "threshold": self.MAX_DENIALS_PER_SESSION,
                }
            )


# ============================================================================
# Secure Entitlement Context
# ============================================================================

class SecureEntitlementContext:
    """
    Secure wrapper combining all security features.
    
    Usage:
        ctx = SecureEntitlementContext(
            app_id="myapp",
            user_id="user_123",
            entitlements=manifest_dict,
            strict=True
        )
        
        # Verify signature
        ctx.verify_signature()
        
        # Check capability with audit
        if ctx.has_capability("cap.workflow.analytics"):
            ...
        
        # Validate tenant isolation
        ctx.validate_tenant(resource_app_id)
    """
    
    def __init__(
        self,
        app_id: str,
        entitlements: Dict[str, Any],
        user_id: Optional[str] = None,
        strict: bool = True,
        signing_key: Optional[str] = None
    ):
        self.app_id = app_id
        self.user_id = user_id
        self.entitlements = entitlements
        self.strict = strict
        
        # Initialize security components
        self._signer = EntitlementSigner(signing_key)
        self._audit = SecurityAuditLogger(app_id, user_id)
        self._tenant = TenantIsolationValidator(app_id, self._audit)
        self._anomaly = AnomalyDetector(self._audit)
        
        # Extract capabilities
        self._capabilities = self._extract_capabilities()
        self._limits = self._extract_limits()
    
    def _extract_capabilities(self) -> set:
        """Extract capabilities from entitlements."""
        caps = set()
        metadata = self.entitlements.get("metadata", {})
        if isinstance(metadata, dict):
            caps.update(metadata.get("capabilities", []))
        caps.update(self.entitlements.get("capabilities", []))
        return caps
    
    def _extract_limits(self) -> Dict[str, Union[int, float]]:
        """Extract limits from entitlements."""
        limits = {}
        metadata = self.entitlements.get("metadata", {})
        if isinstance(metadata, dict):
            limits.update(metadata.get("limits", {}))
        limits.update(self.entitlements.get("limits", {}))
        return limits
    
    def verify_signature(self) -> bool:
        """
        Verify entitlement signature.
        
        Returns:
            True if valid (or verification disabled)
            
        Raises:
            EntitlementSignatureError: If invalid and strict=True
        """
        valid = self._signer.verify_manifest(self.entitlements, strict=self.strict)
        self._audit.log_signature_verification(valid, self.entitlements.get("source", "unknown"))
        return valid
    
    def has_capability(self, capability_id: str) -> bool:
        """Check capability with audit logging."""
        allowed = capability_id in self._capabilities
        self._audit.log_capability_check(capability_id, allowed)
        
        if not allowed:
            self._anomaly.record_denial()
        
        return allowed
    
    def check_limit(
        self,
        limit_id: str,
        current_usage: Union[int, float]
    ) -> bool:
        """Check limit with audit logging."""
        limit_value = self._limits.get(limit_id)
        
        if limit_value is None:
            # No limit = unlimited
            self._audit.log_limit_check(limit_id, True, current_usage, float("inf"))
            return True
        
        allowed = current_usage < limit_value
        self._audit.log_limit_check(limit_id, allowed, current_usage, limit_value)
        
        if not allowed:
            self._anomaly.record_denial()
        
        return allowed
    
    def validate_tenant(self, requested_app_id: str) -> bool:
        """Validate tenant isolation."""
        return self._tenant.validate(requested_app_id, strict=self.strict)
    
    @property
    def audit_events(self) -> List[AuditEvent]:
        """Get audit events for this context."""
        return self._audit.events


# ============================================================================
# Module-level singletons
# ============================================================================

_default_signer: Optional[EntitlementSigner] = None


def get_entitlement_signer() -> EntitlementSigner:
    """Get the default entitlement signer."""
    global _default_signer
    if _default_signer is None:
        _default_signer = EntitlementSigner()
    return _default_signer

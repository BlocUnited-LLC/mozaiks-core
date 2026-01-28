# backend/tests/test_subscription_sync_endpoint.py
import os
import sys
import unittest
from pathlib import Path

from fastapi import FastAPI
from fastapi.testclient import TestClient

BACKEND_DIR = Path(__file__).resolve().parents[1]
REPO_ROOT = BACKEND_DIR.parent
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))

from core.routes.subscription_sync import router as subscription_sync_router  # noqa: E402
import core.routes.subscription_sync as sync_routes  # noqa: E402
import core.ai_runtime.auth.dependencies as auth_deps  # noqa: E402
from core.ai_runtime.auth.jwt_validator import TokenClaims  # noqa: E402


class StubSubscriptionManager:
    def __init__(self) -> None:
        self.called = None

    async def sync_subscription_from_control_plane(self, user_id, subscription_data, *, _internal_call=False):
        self.called = {
            "user_id": user_id,
            "subscription_data": subscription_data,
            "_internal_call": _internal_call,
        }
        return {
            "success": True,
            "user_id": user_id,
            "plan": subscription_data.get("plan"),
            "status": subscription_data.get("status"),
        }


class SubscriptionSyncEndpointTests(unittest.TestCase):
    def setUp(self) -> None:
        self._original_auth_enabled = os.environ.get("AUTH_ENABLED")
        os.environ["AUTH_ENABLED"] = "true"

        self.stub = StubSubscriptionManager()
        self._original_manager = sync_routes.subscription_manager
        sync_routes.subscription_manager = self.stub

        # Stub JWT validator so tests don't depend on a live Keycloak.
        self._original_get_jwt_validator = auth_deps.get_jwt_validator

        class _StubValidator:
            def __init__(self, roles):
                self._roles = roles

            async def validate_token(self, token: str, require_scope: bool = True):
                return TokenClaims(
                    user_id="service",
                    email=None,
                    roles=list(self._roles),
                    scopes=[],
                    raw_claims={"azp": "platform-service"},
                )

        self._validator_with_role = _StubValidator(["internal_service"])
        self._validator_without_role = _StubValidator([])
        auth_deps.get_jwt_validator = lambda: self._validator_with_role

        app = FastAPI()
        app.include_router(subscription_sync_router)
        self.client = TestClient(app)

    def tearDown(self) -> None:
        sync_routes.subscription_manager = self._original_manager
        auth_deps.get_jwt_validator = self._original_get_jwt_validator
        if self._original_auth_enabled is None:
            os.environ.pop("AUTH_ENABLED", None)
        else:
            os.environ["AUTH_ENABLED"] = self._original_auth_enabled

    def _payload(self) -> dict:
        return {
            "user_id": "user_1",
            "plan": "premium",
            "status": "active",
        }

    def test_requires_auth(self) -> None:
        resp = self.client.post("/api/internal/subscription/sync", json=self._payload())
        self.assertEqual(resp.status_code, 401)

    def test_rejects_missing_role(self) -> None:
        auth_deps.get_jwt_validator = lambda: self._validator_without_role
        headers = {"Authorization": "Bearer test-token"}
        resp = self.client.post("/api/internal/subscription/sync", headers=headers, json=self._payload())
        self.assertEqual(resp.status_code, 403)

    def test_sync_calls_manager(self) -> None:
        headers = {"Authorization": "Bearer test-token"}
        payload = {
            "user_id": "user_1",
            "plan": "premium",
            "status": "active",
            "app_id": "app_123",
            "billing_cycle": "monthly",
        }
        resp = self.client.post("/api/internal/subscription/sync", headers=headers, json=payload)
        self.assertEqual(resp.status_code, 200)
        self.assertIsNotNone(self.stub.called)
        self.assertTrue(self.stub.called["_internal_call"])
        self.assertEqual(self.stub.called["user_id"], "user_1")
        self.assertEqual(self.stub.called["subscription_data"]["app_id"], "app_123")


if __name__ == "__main__":
    unittest.main()

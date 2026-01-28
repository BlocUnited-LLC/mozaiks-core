# backend/tests/test_pay_proxy_contract.py
import sys
from pathlib import Path
import unittest

from fastapi import FastAPI
from fastapi.testclient import TestClient

BACKEND_DIR = Path(__file__).resolve().parents[1]
REPO_ROOT = BACKEND_DIR.parent
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))


from app.routes.mozaiks import router as mozaiks_router  # noqa: E402
from app.connectors.base import CheckoutResponse  # noqa: E402
from core.ai_runtime.auth.dependencies import get_current_user  # noqa: E402


class PayProxyContractTests(unittest.TestCase):
    def test_pay_routes_require_authorization(self) -> None:
        app = FastAPI()
        app.include_router(mozaiks_router, prefix="/api/mozaiks")
        client = TestClient(app)

        resp = client.get("/api/mozaiks/pay/subscription-status?scope=platform")
        self.assertEqual(resp.status_code, 401)

    def test_correlation_id_is_propagated(self) -> None:
        import app.routes.mozaiks as mozaiks_routes  # noqa: E402

        class StubPayment:
            def __init__(self) -> None:
                self.last = None

            async def checkout(self, *, payload, correlation_id: str, user_jwt: str | None = None):
                self.last = {"payload": payload, "correlation_id": correlation_id, "user_jwt": user_jwt}
                return CheckoutResponse(checkoutUrl="https://example.test/checkout", sessionId="sess_test")

            async def subscription_status(self, *, scope, appId, correlation_id: str, user_jwt: str | None = None):
                raise AssertionError("not used")

            async def cancel(self, *, payload, correlation_id: str, user_jwt: str | None = None):
                raise AssertionError("not used")

        stub_payment = StubPayment()
        original_connectors = mozaiks_routes._connectors
        try:
            mozaiks_routes._connectors = lambda: type("B", (), {"payment": stub_payment})()

            app = FastAPI()
            app.include_router(mozaiks_router, prefix="/api/mozaiks")
            app.dependency_overrides[get_current_user] = lambda: {"username": "u", "user_id": "1"}
            client = TestClient(app)

            headers = {"Authorization": "Bearer test.jwt.token", "x-correlation-id": "corr-123"}
            resp = client.post("/api/mozaiks/pay/checkout", headers=headers, json={"scope": "platform"})

            self.assertEqual(resp.status_code, 200)
            self.assertEqual(resp.headers.get("x-correlation-id"), "corr-123")
            self.assertEqual(stub_payment.last["correlation_id"], "corr-123")
            self.assertEqual(stub_payment.last["user_jwt"], "test.jwt.token")
        finally:
            mozaiks_routes._connectors = original_connectors

    def test_ui_does_not_mention_payment_providers(self) -> None:
        forbidden = {"stripe", "paypal", "braintree", "adyen"}
        src_dir = REPO_ROOT / "src"
        self.assertTrue(src_dir.exists(), f"Missing src directory at {src_dir}")

        for path in src_dir.rglob("*"):
            if not path.is_file():
                continue
            if path.suffix.lower() not in {".js", ".jsx", ".ts", ".tsx", ".html"}:
                continue
            text = path.read_text(encoding="utf-8", errors="ignore").lower()
            for word in forbidden:
                self.assertNotIn(word, text, f"Found provider string '{word}' in UI file: {path}")


if __name__ == "__main__":
    unittest.main()

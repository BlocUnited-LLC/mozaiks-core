# backend/tests/test_mock_connectors.py
import sys
from pathlib import Path
import unittest

BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))


from app.connectors.mock import MockPaymentConnector  # noqa: E402


class MockConnectorBehaviorTests(unittest.IsolatedAsyncioTestCase):
    async def test_payment_subscription_is_active(self) -> None:
        payment = MockPaymentConnector()
        status = await payment.subscription_status(
            scope="app",
            appId="a1",
            correlation_id="c1",
            user_jwt=None,
        )
        self.assertTrue(status.active)
        self.assertEqual(status.status, "active")
        self.assertEqual(status.appId, "a1")
        self.assertEqual(status.scope, "app")


if __name__ == "__main__":
    unittest.main()

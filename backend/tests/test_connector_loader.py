# backend/tests/test_connector_loader.py
import os
import sys
from pathlib import Path
import unittest

BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))


from app.runtime.connector_loader import is_managed_mode, load_connectors  # noqa: E402
from app.connectors.managed import ManagedPaymentConnector  # noqa: E402
from app.connectors.mock import MockPaymentConnector  # noqa: E402


class ConnectorLoaderTests(unittest.TestCase):
    def setUp(self) -> None:
        self._snapshot = {
            "MOZAIKS_HOSTING_MODE": os.getenv("MOZAIKS_HOSTING_MODE"),
            "MOZAIKS_MANAGED": os.getenv("MOZAIKS_MANAGED"),
            "MOZAIKS_GATEWAY_BASE_URL": os.getenv("MOZAIKS_GATEWAY_BASE_URL"),
            "MOZAIKS_GATEWAY_API_KEY": os.getenv("MOZAIKS_GATEWAY_API_KEY"),
        }

    def tearDown(self) -> None:
        for key, value in self._snapshot.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value

    def test_defaults_to_self_hosted(self) -> None:
        os.environ.pop("MOZAIKS_HOSTING_MODE", None)
        os.environ.pop("MOZAIKS_MANAGED", None)
        os.environ.pop("MOZAIKS_GATEWAY_BASE_URL", None)
        os.environ.pop("MOZAIKS_GATEWAY_API_KEY", None)

        self.assertFalse(is_managed_mode())
        bundle = load_connectors()
        self.assertEqual(bundle.mode, "self_hosted")
        self.assertIsInstance(bundle.payment, MockPaymentConnector)

    def test_requires_all_managed_env_vars(self) -> None:
        os.environ["MOZAIKS_MANAGED"] = "true"
        os.environ.pop("MOZAIKS_GATEWAY_BASE_URL", None)

        self.assertFalse(is_managed_mode())
        self.assertEqual(load_connectors().mode, "self_hosted")

        os.environ["MOZAIKS_GATEWAY_BASE_URL"] = "https://example.invalid"
        self.assertTrue(is_managed_mode())

    def test_managed_mode_switches_to_http_connectors(self) -> None:
        os.environ["MOZAIKS_MANAGED"] = "true"
        os.environ["MOZAIKS_GATEWAY_BASE_URL"] = "https://example.invalid"

        self.assertTrue(is_managed_mode())
        bundle = load_connectors()
        self.assertEqual(bundle.mode, "managed")
        self.assertIsInstance(bundle.payment, ManagedPaymentConnector)


if __name__ == "__main__":
    unittest.main()

# backend/tests/test_insights_heartbeat_settings.py
import os
import sys
from pathlib import Path
import unittest


BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))


from mozaiks_infra.config.settings import load_settings  # noqa: E402


class InsightsHeartbeatSettingsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._snapshot = {
            "ENV": os.getenv("ENV"),
            "INSIGHTS_PUSH_INTERVAL_S": os.getenv("INSIGHTS_PUSH_INTERVAL_S"),
            "INSIGHTS_HEARTBEAT_ENABLED": os.getenv("INSIGHTS_HEARTBEAT_ENABLED"),
            "INSIGHTS_HEARTBEAT_INTERVAL_S": os.getenv("INSIGHTS_HEARTBEAT_INTERVAL_S"),
            "MOZAIKS_PLATFORM_JWKS_URL": os.getenv("MOZAIKS_PLATFORM_JWKS_URL"),
            "MOZAIKS_PLATFORM_ISSUER": os.getenv("MOZAIKS_PLATFORM_ISSUER"),
            "MOZAIKS_PLATFORM_AUDIENCE": os.getenv("MOZAIKS_PLATFORM_AUDIENCE"),
        }
        # Required by OIDC-by-default settings validation.
        os.environ.setdefault("MOZAIKS_PLATFORM_JWKS_URL", "https://example.test/.well-known/jwks.json")
        os.environ.setdefault("MOZAIKS_PLATFORM_ISSUER", "https://issuer.example.test/")
        os.environ.setdefault("MOZAIKS_PLATFORM_AUDIENCE", "example-audience")

    def tearDown(self) -> None:
        for key, value in self._snapshot.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value

    def test_defaults_heartbeat_interval_to_max_60_or_push_interval(self) -> None:
        os.environ["ENV"] = "development"
        os.environ["INSIGHTS_PUSH_INTERVAL_S"] = "10"
        os.environ.pop("INSIGHTS_HEARTBEAT_ENABLED", None)
        os.environ.pop("INSIGHTS_HEARTBEAT_INTERVAL_S", None)

        cfg = load_settings()
        self.assertTrue(cfg.insights_heartbeat_enabled)
        self.assertEqual(cfg.insights_heartbeat_interval_s, 60.0)

    def test_can_disable_heartbeat_via_env_var(self) -> None:
        os.environ["ENV"] = "development"
        os.environ["INSIGHTS_HEARTBEAT_ENABLED"] = "0"

        cfg = load_settings()
        self.assertFalse(cfg.insights_heartbeat_enabled)


if __name__ == "__main__":
    unittest.main()

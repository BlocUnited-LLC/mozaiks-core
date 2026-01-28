# backend/tests/test_insights_auth.py
import os
import sys
from pathlib import Path
import unittest


BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))


from mozaiks_platform.insights.client import InsightsClient, InsightsClientConfig  # noqa: E402
from mozaiks_infra.config.settings import load_settings  # noqa: E402


class InsightsAuthHeaderTests(unittest.TestCase):
    def test_headers_prefer_per_app_api_key(self) -> None:
        cfg = InsightsClientConfig(
            base_url="https://example.test",
            sdk_version="1.2.3",
            mozaiks_app_id="app_123",
            mozaiks_api_key="moz_live_abcdef123456",
            internal_api_key="internal_key",
        )
        client = InsightsClient(cfg)

        headers = client._headers(correlation_id="corr-1")
        self.assertEqual(headers["X-Correlation-Id"], "corr-1")
        self.assertEqual(headers["X-Mozaiks-Sdk-Version"], "1.2.3")
        self.assertEqual(headers["X-Mozaiks-App-Id"], "app_123")
        self.assertEqual(headers["X-Mozaiks-Api-Key"], "moz_live_abcdef123456")
        self.assertNotIn("X-Internal-Api-Key", headers)

    def test_headers_fall_back_to_internal_key(self) -> None:
        cfg = InsightsClientConfig(
            base_url="https://example.test",
            sdk_version="1.0.0",
            internal_api_key="internal_key",
        )
        client = InsightsClient(cfg)
        headers = client._headers(correlation_id="corr-2")

        self.assertEqual(headers["X-Correlation-Id"], "corr-2")
        self.assertEqual(headers["X-Mozaiks-Sdk-Version"], "1.0.0")
        self.assertEqual(headers["X-Internal-Api-Key"], "internal_key")
        self.assertNotIn("X-Mozaiks-Api-Key", headers)


class InsightsEnvAliasTests(unittest.TestCase):
    def test_insights_api_base_url_takes_precedence(self) -> None:
        keys = [
            "ENV",
            "INSIGHTS_API_BASE_URL",
            "INSIGHTS_BASE_URL",
            "MOZAIKS_PLATFORM_JWKS_URL",
            "MOZAIKS_PLATFORM_ISSUER",
            "MOZAIKS_PLATFORM_AUDIENCE",
        ]
        original = {k: os.environ.get(k) for k in keys}
        try:
            os.environ["ENV"] = "development"
            os.environ["INSIGHTS_API_BASE_URL"] = "https://new.example.test"
            os.environ["INSIGHTS_BASE_URL"] = "https://old.example.test"
            # Required by OIDC-by-default settings validation.
            os.environ["MOZAIKS_PLATFORM_JWKS_URL"] = "https://example.test/.well-known/jwks.json"
            os.environ["MOZAIKS_PLATFORM_ISSUER"] = "https://issuer.example.test/"
            os.environ["MOZAIKS_PLATFORM_AUDIENCE"] = "example-audience"

            cfg = load_settings()
            self.assertEqual(cfg.insights_base_url, "https://new.example.test")
        finally:
            for key, value in original.items():
                if value is None:
                    os.environ.pop(key, None)
                else:
                    os.environ[key] = value


if __name__ == "__main__":
    unittest.main()

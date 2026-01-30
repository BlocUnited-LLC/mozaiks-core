# backend/tests/test_insights_health.py
import importlib
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
import unittest


BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))


def _fresh_health_module():
    sys.modules.pop("mozaiks_platform.insights.health", None)
    import mozaiks_platform.insights.health as health  # noqa: E402

    return importlib.reload(health)


class InsightsHealthTests(unittest.TestCase):
    def test_sdk_connected_false_when_no_heartbeat(self) -> None:
        health = _fresh_health_module()
        self.assertFalse(health.sdk_connected(within_s=60, now=datetime(2025, 12, 22, tzinfo=timezone.utc)))

    def test_sdk_connected_true_with_recent_heartbeat(self) -> None:
        health = _fresh_health_module()
        now = datetime(2025, 12, 22, 0, 0, 0, tzinfo=timezone.utc)
        health.record_heartbeat(at=now)
        self.assertTrue(health.sdk_connected(within_s=60, now=now))
        self.assertFalse(health.sdk_connected(within_s=1, now=now + timedelta(seconds=2)))

    def test_snapshot_tracks_failure_and_success(self) -> None:
        health = _fresh_health_module()
        now = datetime(2025, 12, 22, 0, 0, 0, tzinfo=timezone.utc)
        health.record_failure(at=now, status_code=500, message="boom")
        snap = health.snapshot()
        self.assertEqual(snap["lastFailureStatusCode"], 500)
        self.assertEqual(snap["lastFailureMessage"], "boom")

        health.record_success(at=now + timedelta(seconds=1))
        snap2 = health.snapshot()
        self.assertIsNone(snap2["lastFailureStatusCode"])
        self.assertIsNone(snap2["lastFailureMessage"])


if __name__ == "__main__":
    unittest.main()


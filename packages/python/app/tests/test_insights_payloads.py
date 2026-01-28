# backend/tests/test_insights_payloads.py
import sys
from datetime import datetime, timezone
from pathlib import Path
import unittest

BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))

from mozaiks_platform.insights.payloads import (  # noqa: E402
    KPIValue,
    build_sdk_heartbeat_event,
    build_events_payload,
    build_kpi_payload,
    floor_to_minute,
    stable_sdk_heartbeat_event_id,
    stable_user_event_id,
    to_iso_z,
)


class InsightsPayloadTests(unittest.TestCase):
    def test_to_iso_z_naive_uses_utc(self) -> None:
        dt = datetime(2025, 12, 22, 0, 0, 0)
        self.assertEqual(to_iso_z(dt), "2025-12-22T00:00:00Z")

    def test_floor_to_minute(self) -> None:
        dt = datetime(2025, 12, 22, 0, 0, 59, 999_999, tzinfo=timezone.utc)
        self.assertEqual(floor_to_minute(dt), datetime(2025, 12, 22, 0, 0, 0, tzinfo=timezone.utc))

    def test_stable_user_event_id(self) -> None:
        self.assertEqual(
            stable_user_event_id(event_type="UserSignedUp", user_id="USER_ID", day="2025-12-22"),
            "UserSignedUp:USER_ID:2025-12-22",
        )

    def test_stable_sdk_heartbeat_event_id_is_minute_bucketed(self) -> None:
        dt = datetime(2025, 12, 22, 0, 0, 59, tzinfo=timezone.utc)
        self.assertEqual(
            stable_sdk_heartbeat_event_id(app_id="app1", env="development", point_time=dt),
            "SDKHeartbeat:app1:development:20251222T0000",
        )

    def test_build_sdk_heartbeat_event_shape(self) -> None:
        point_time = datetime(2025, 12, 22, 0, 0, 59, tzinfo=timezone.utc)
        event = build_sdk_heartbeat_event(
            app_id="app1",
            env="development",
            sdk_version="1.2.3",
            point_time=point_time,
        )

        self.assertEqual(event["type"], "SDKHeartbeat")
        self.assertEqual(event["severity"], "debug")
        self.assertEqual(event["t"], "2025-12-22T00:00:00Z")
        self.assertEqual(event["data"]["sdkVersion"], "1.2.3")

    def test_build_kpi_payload_shape(self) -> None:
        point_time = datetime(2025, 12, 22, 0, 0, 0, tzinfo=timezone.utc)
        sent_at = datetime(2025, 12, 22, 0, 0, 1, tzinfo=timezone.utc)
        payload = build_kpi_payload(
            app_id="app1",
            env="development",
            bucket="1m",
            point_time=point_time,
            sent_at=sent_at,
            tags={"source": "mozaikscore"},
            kpis=[
                KPIValue(metric="total_users", value=123, unit="count"),
                KPIValue(metric="dau", value=45, unit="count"),
                KPIValue(metric="retention_7d", value=0.31, unit="ratio"),
            ],
        )

        self.assertEqual(payload["appId"], "app1")
        self.assertEqual(payload["env"], "development")
        self.assertEqual(payload["bucket"], "1m")
        self.assertEqual(payload["sentAtUtc"], "2025-12-22T00:00:01Z")
        self.assertEqual(payload["tags"], {"source": "mozaikscore"})

        points = payload["points"]
        self.assertEqual(len(points), 3)
        for p in points:
            self.assertEqual(p["t"], "2025-12-22T00:00:00Z")

    def test_build_events_payload_shape(self) -> None:
        sent_at = datetime(2025, 12, 22, 0, 0, 1, tzinfo=timezone.utc)
        payload = build_events_payload(
            app_id="app1",
            env="development",
            sent_at=sent_at,
            events=[
                {
                    "eventId": "UserSignedUp:USER_ID:2025-12-22",
                    "t": "2025-12-22T00:00:00Z",
                    "type": "UserSignedUp",
                    "severity": "info",
                    "message": "User signed up",
                    "data": {"userId": "USER_ID"},
                }
            ],
        )

        self.assertEqual(payload["appId"], "app1")
        self.assertEqual(payload["env"], "development")
        self.assertEqual(payload["sentAtUtc"], "2025-12-22T00:00:01Z")
        self.assertEqual(len(payload["events"]), 1)


if __name__ == "__main__":
    unittest.main()

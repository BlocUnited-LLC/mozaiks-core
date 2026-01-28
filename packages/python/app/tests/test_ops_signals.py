# backend/tests/test_ops_signals.py
import importlib
import sys
from datetime import datetime, timezone
from pathlib import Path
import unittest


BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))


def _fresh_ops_module():
    sys.modules.pop("core.ops.signals", None)
    import mozaiks_infra.ops.signals as ops  # noqa: E402

    return importlib.reload(ops)


class OpsSignalsTests(unittest.TestCase):
    def test_emit_module_status_and_metric_snapshot(self) -> None:
        ops = _fresh_ops_module()

        ts = datetime(2025, 12, 28, 0, 0, 0, tzinfo=timezone.utc)
        ops.emit_module_status("example", enabled=True, last_run_utc=ts, ok=True)
        ops.inc_module_metric("example", "jobs_started", value=2, tags={"queue": "default"})

        snap = ops.snapshot()
        self.assertEqual(snap["schemaVersion"], 1)
        modules = snap["modules"]
        self.assertEqual(len(modules), 1)
        self.assertEqual(modules[0]["name"], "example")
        self.assertTrue(modules[0]["enabled"])
        self.assertTrue(modules[0]["ok"])
        self.assertEqual(modules[0]["lastRunUtc"], "2025-12-28T00:00:00Z")

        metrics = modules[0]["metrics"]
        self.assertEqual(len(metrics), 1)
        self.assertEqual(metrics[0]["metric"], "jobs_started")
        self.assertEqual(metrics[0]["value"], 2)
        self.assertEqual(metrics[0]["tags"], {"queue": "default"})

    def test_status_error_sets_ok_false_by_default(self) -> None:
        ops = _fresh_ops_module()
        ops.emit_module_status("example", enabled=True, error="boom")
        snap = ops.snapshot()
        self.assertFalse(snap["modules"][0]["ok"])
        self.assertEqual(snap["modules"][0]["error"], "boom")


if __name__ == "__main__":
    unittest.main()


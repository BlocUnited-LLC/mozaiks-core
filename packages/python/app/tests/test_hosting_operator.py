# backend/tests/test_hosting_operator.py
import sys
from pathlib import Path
import unittest


BACKEND_DIR = Path(__file__).resolve().parents[1]
REPO_ROOT = BACKEND_DIR.parent
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))


from mozaiks_platform.hosting_operator import evaluate_snapshot, load_json  # noqa: E402


class HostingOperatorTests(unittest.TestCase):
    def test_example_policy_is_valid(self) -> None:
        policy_path = REPO_ROOT / "docs" / "HostingOperatorPolicy.example.json"
        policy = load_json(policy_path)
        self.assertEqual(policy.get("schema_version"), "v1")
        self.assertIn("profiles", policy)
        self.assertIn("starter", policy["profiles"])

    def test_snapshot_triggers_upgrade_and_block(self) -> None:
        policy_path = REPO_ROOT / "docs" / "HostingOperatorPolicy.example.json"
        policy = load_json(policy_path)

        snapshot = {
            "app_id": "app_test",
            "profile": "starter",
            "metrics": {
                "active_websocket_connections": 500,
                "concurrent_workflows": 25,
            },
        }
        result = evaluate_snapshot(policy, snapshot)

        kinds = {a.get("kind") for a in result.get("actions") or []}
        self.assertIn("platform.require_upgrade", kinds)
        self.assertIn("platform.block_deploy", kinds)


if __name__ == "__main__":
    unittest.main()


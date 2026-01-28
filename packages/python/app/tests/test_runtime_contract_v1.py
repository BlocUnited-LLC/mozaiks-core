# backend/tests/test_runtime_contract_v1.py
import os
import sys
import unittest
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from uuid import UUID
from unittest.mock import AsyncMock, patch

from fastapi import FastAPI
from fastapi.testclient import TestClient

BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))

from core.routes.ai import router as ai_router  # noqa: E402
import core.routes.ai as routes_ai  # noqa: E402
from core.runtime.manager import RuntimeUiConfig  # noqa: E402
import shared_app  # noqa: E402


class StubCursor:
    def __init__(self, docs):
        self._docs = docs

    def sort(self, *args, **kwargs):
        return self

    async def to_list(self, length=None):
        return list(self._docs)


class StubCollection:
    def __init__(self, doc_by_id=None):
        self._doc_by_id = doc_by_id or {}

    async def find_one(self, query, projection=None, sort=None):
        chat_id = query.get("_id") or query.get("chat_id")
        if chat_id and chat_id in self._doc_by_id:
            return self._doc_by_id[chat_id]
        return None

    def find(self, query):
        return StubCursor([])


class StubWorkflowManager:
    def get_all_workflow_names(self):
        return ["AgentGenerator"]

    def get_config(self, workflow_name):
        return {
            "metadata": {
                "title": "Agent Generator",
                "description": "Generate agents",
                "icon": "robot",
            }
        }


class StubPerformanceManager:
    async def record_workflow_start(self, *args, **kwargs):
        return None


class CoreContractTests(unittest.TestCase):
    def setUp(self):
        self._monetization_env = os.environ.get("MONETIZATION")
        os.environ["MONETIZATION"] = "0"

        self.app = FastAPI()
        self.app.include_router(ai_router)
        self.client = TestClient(self.app)

        self.user = {
            "user_id": "user_123",
            "identity_user_id": "user_123",
            "roles": [],
            "is_superadmin": False,
        }
        self.app.dependency_overrides[routes_ai.get_current_user] = lambda: self.user

    def tearDown(self):
        self.app.dependency_overrides = {}
        if self._monetization_env is None:
            os.environ.pop("MONETIZATION", None)
        else:
            os.environ["MONETIZATION"] = self._monetization_env

    def test_list_capabilities_contract(self):
        with patch.object(
            routes_ai,
            "_load_all_capabilities",
            return_value=[{"id": "cap_1", "display_name": "Cap 1", "description": "Desc"}],
        ):
            resp = self.client.get("/api/ai/capabilities")
        self.assertEqual(resp.status_code, 200)
        data = resp.json()
        self.assertIn("capabilities", data)
        self.assertEqual(data["capabilities"][0]["id"], "cap_1")
        self.assertIn("allowed", data["capabilities"][0])

    def test_launch_capability_contract(self):
        runtime_config = RuntimeUiConfig(
            runtime_api_base_url="http://runtime",
            runtime_ui_base_url="http://runtime",
            chatui_url_template="{runtime_ui_base_url}/chat?app_id={app_id}&chat_id={chat_id}&token={token}",
        )
        with patch.object(
            routes_ai,
            "_load_all_capabilities",
            return_value=[{"id": "cap_1", "workflow_id": "AgentGenerator"}],
        ), patch.object(
            routes_ai,
            "mint_execution_token",
            return_value=("launch-token", 600),
        ), patch.object(
            routes_ai.runtime_manager,
            "new_chat_id",
            return_value="chat_test",
        ), patch.object(
            routes_ai.runtime_manager,
            "ui_config",
            return_value=runtime_config,
        ), patch.object(
            routes_ai,
            "settings",
            SimpleNamespace(mozaiks_app_id="app_test"),
        ):
            resp = self.client.post("/api/ai/launch", json={"capability_id": "cap_1"})

        self.assertEqual(resp.status_code, 200)
        payload = resp.json()
        self.assertEqual(payload["app_id"], "app_test")
        self.assertEqual(payload["capability_id"], "cap_1")
        self.assertEqual(payload["chat_id"], "chat_test")
        self.assertEqual(payload["launch_token"], "launch-token")
        self.assertIn("runtime", payload)


class RuntimeContractTests(unittest.TestCase):
    def setUp(self):
        self.app = shared_app.app
        self.client = TestClient(self.app)

        self.principal = shared_app.UserPrincipal(
            user_id="user_123",
            email=None,
            roles=[],
            scopes=[],
            raw_claims={},
        )

        self.app.dependency_overrides[shared_app.require_user_scope] = lambda: self.principal
        self.app.dependency_overrides[shared_app.require_any_auth] = lambda: self.principal

        now = datetime(2026, 1, 23, tzinfo=timezone.utc)
        self.chat_doc = {
            "_id": "chat_123",
            "workflow_name": "AgentGenerator",
            "user_id": "user_123",
            "cache_seed": 123,
            "status": 0,
            "last_sequence": 5,
            "last_artifact": {"type": "artifact"},
            "created_at": now,
            "last_updated_at": now,
        }
        stub_coll = StubCollection({"chat_123": self.chat_doc})

        self._patchers = [
            patch.object(shared_app, "_chat_coll", new=AsyncMock(return_value=stub_coll)),
            patch.object(shared_app, "_validate_pack_prereqs", new=AsyncMock(return_value=(True, None))),
            patch.object(shared_app, "get_performance_manager", new=AsyncMock(return_value=StubPerformanceManager())),
            patch.object(shared_app.persistence_manager, "create_chat_session", new=AsyncMock()),
            patch.object(shared_app.persistence_manager, "get_or_assign_cache_seed", new=AsyncMock(return_value=123)),
            patch.object(shared_app, "uuid4", return_value=UUID("00000000-0000-0000-0000-000000000001")),
            patch("core.ai_runtime.workflow.workflow_manager.workflow_manager", new=StubWorkflowManager()),
            patch(
                "core.ai_runtime.workflow.pack.gating.list_workflow_availability",
                new=AsyncMock(
                    return_value=[
                        {
                            "workflow_name": "AgentGenerator",
                            "available": True,
                            "reason": "All prerequisites met",
                        }
                    ]
                ),
            ),
            patch("core.ai_runtime.workflow.session_manager.get_workflow_session", new=AsyncMock(return_value=None)),
            patch("core.ai_runtime.workflow.session_manager.get_artifact_instance", new=AsyncMock(return_value=None)),
        ]
        for patcher in self._patchers:
            patcher.start()

    def tearDown(self):
        self.app.dependency_overrides = {}
        for patcher in self._patchers:
            patcher.stop()

    def test_list_runtime_capabilities(self):
        resp = self.client.get("/api/ai/capabilities")
        self.assertEqual(resp.status_code, 200)
        payload = resp.json()
        self.assertIn("capabilities", payload)
        self.assertEqual(payload["capabilities"][0]["id"], "AgentGenerator")
        self.assertEqual(payload["capabilities"][0]["display_name"], "Agent Generator")

    def test_list_workflows(self):
        resp = self.client.get("/api/workflows")
        self.assertEqual(resp.status_code, 200)
        payload = resp.json()
        self.assertIn("AgentGenerator", payload)

    def test_start_chat_contract(self):
        resp = self.client.post(
            "/api/chats/app_123/AgentGenerator/start",
            json={"user_id": "user_123"},
        )
        self.assertEqual(resp.status_code, 200)
        payload = resp.json()
        self.assertTrue(payload["success"])
        self.assertEqual(payload["chat_id"], "00000000-0000-0000-0000-000000000001")
        self.assertEqual(payload["app_id"], "app_123")
        self.assertEqual(payload["user_id"], "user_123")
        self.assertEqual(payload["cache_seed"], 123)
        self.assertIn("websocket_url", payload)

    def test_chat_meta_contract(self):
        resp = self.client.get("/api/chats/meta/app_123/AgentGenerator/chat_123")
        self.assertEqual(resp.status_code, 200)
        payload = resp.json()
        self.assertTrue(payload["exists"])
        self.assertEqual(payload["user_id"], "user_123")
        self.assertIsNotNone(payload.get("created_at"))
        self.assertIsNotNone(payload.get("updated_at"))
        self.assertEqual(payload.get("status"), "in_progress")
        self.assertEqual(payload.get("status_code"), 0)

    def test_available_workflows_contract(self):
        resp = self.client.get("/api/workflows/app_123/available")
        self.assertEqual(resp.status_code, 200)
        payload = resp.json()
        self.assertIn("workflows", payload)
        entry = payload["workflows"][0]
        self.assertEqual(entry["id"], "AgentGenerator")
        self.assertTrue(entry["available"])
        self.assertIsNone(entry["locked_reason"])

    def test_list_sessions_contract(self):
        resp = self.client.get("/api/sessions/list/app_123/user_123")
        self.assertEqual(resp.status_code, 200)
        payload = resp.json()
        self.assertEqual(payload["sessions"], [])
        self.assertEqual(payload["count"], 0)


if __name__ == "__main__":
    unittest.main()

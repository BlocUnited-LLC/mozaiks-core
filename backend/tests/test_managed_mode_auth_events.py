# backend/tests/test_managed_mode_auth_events.py
import sys
from pathlib import Path
from types import SimpleNamespace
import unittest

from bson import ObjectId

BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))

import security.authentication as auth  # noqa: E402


class _StubUsersCollection:
    def __init__(self) -> None:
        self.user = None
        self.insert_calls = 0
        self.update_calls = 0

    async def find_one(self, query):  # noqa: ANN001
        # Only the platform path is used in this test.
        if "platform_user_id" in query:
            return self.user
        return None

    async def insert_one(self, doc):  # noqa: ANN001
        self.insert_calls += 1
        inserted_id = ObjectId()
        self.user = {**doc, "_id": inserted_id}

        class _Result:
            def __init__(self, inserted_id: ObjectId) -> None:
                self.inserted_id = inserted_id

        return _Result(inserted_id)

    async def update_one(self, query, update):  # noqa: ANN001
        self.update_calls += 1
        return None


class ManagedModeAuthEventLoggingTests(unittest.IsolatedAsyncioTestCase):
    async def test_platform_user_creation_emits_signup_and_daily_active(self) -> None:
        original_settings = auth.settings
        original_users_collection = auth.users_collection
        original_decode = auth.decode_platform_jwt
        original_append_active = auth.append_user_active
        original_append_signup = auth.append_user_signed_up

        calls = {"active": [], "signup": []}
        auth._active_day_cache.clear()

        async def fake_decode_platform_jwt(token: str) -> dict:  # noqa: ARG001
            return {"sub": "plat-user-1", "roles": []}

        async def fake_append_user_active(*, user_id: str, **kwargs) -> None:  # noqa: ANN003
            calls["active"].append({"user_id": user_id, **kwargs})

        async def fake_append_user_signed_up(*, user_id: str, **kwargs) -> None:  # noqa: ANN003
            calls["signup"].append({"user_id": user_id, **kwargs})

        try:
            auth.settings = SimpleNamespace(
                mozaiks_managed=True,
                mozaiks_auth_mode="platform",
                token_exchange_enabled=False,
                platform_user_id_claim="sub",
                platform_roles_claim="roles",
                platform_admin_role="admin",
            )
            auth.decode_platform_jwt = fake_decode_platform_jwt
            auth.append_user_active = fake_append_user_active
            auth.append_user_signed_up = fake_append_user_signed_up
            auth.users_collection = _StubUsersCollection()

            user = await auth._resolve_user_from_token("token-1")
            self.assertEqual(user["platform_user_id"], "plat-user-1")
            self.assertEqual(len(calls["signup"]), 1)
            self.assertEqual(len(calls["active"]), 1)

            # Second request same day should not emit another daily active marker.
            user2 = await auth._resolve_user_from_token("token-1")
            self.assertEqual(user2["user_id"], user["user_id"])
            self.assertEqual(len(calls["signup"]), 1)
            self.assertEqual(len(calls["active"]), 1)
        finally:
            auth.settings = original_settings
            auth.users_collection = original_users_collection
            auth.decode_platform_jwt = original_decode
            auth.append_user_active = original_append_active
            auth.append_user_signed_up = original_append_signup


if __name__ == "__main__":
    unittest.main()

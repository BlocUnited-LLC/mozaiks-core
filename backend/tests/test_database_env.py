# backend/tests/test_database_env.py
import importlib
import os
import sys
from pathlib import Path
import unittest


BACKEND_DIR = Path(__file__).resolve().parents[1]
if str(BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(BACKEND_DIR))


def _fresh_database_module():
    sys.modules.pop("core.config.database", None)
    import core.config.database as database  # noqa: E402

    return importlib.reload(database)


class DatabaseEnvTests(unittest.TestCase):
    def setUp(self) -> None:
        self._snapshot = {
            "MONGODB_URI": os.getenv("MONGODB_URI"),
            "DATABASE_URI": os.getenv("DATABASE_URI"),
            "DATABASE_NAME": os.getenv("DATABASE_NAME"),
            "MOZAIKS_ALLOW_NO_DB": os.getenv("MOZAIKS_ALLOW_NO_DB"),
            "MOZAIKS_LOAD_DOTENV": os.getenv("MOZAIKS_LOAD_DOTENV"),
            "MOZAIKS_HOSTING_MODE": os.getenv("MOZAIKS_HOSTING_MODE"),
            "MOZAIKS_MANAGED": os.getenv("MOZAIKS_MANAGED"),
        }
        os.environ["MOZAIKS_LOAD_DOTENV"] = "0"

    def tearDown(self) -> None:
        for key, value in self._snapshot.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value

    def test_prefers_mongodb_uri_over_database_uri(self) -> None:
        os.environ["MOZAIKS_ALLOW_NO_DB"] = "1"
        os.environ.pop("MOZAIKS_HOSTING_MODE", None)
        os.environ.pop("MOZAIKS_MANAGED", None)
        os.environ.pop("DATABASE_NAME", None)

        os.environ["MONGODB_URI"] = "mongodb://localhost:27017/db_from_mongodb_uri"
        os.environ["DATABASE_URI"] = "mongodb://localhost:27017/db_from_database_uri"

        database = _fresh_database_module()
        self.assertEqual(database.MONGO_URI, "mongodb://localhost:27017/db_from_mongodb_uri")
        self.assertEqual(database.DATABASE_NAME, "db_from_mongodb_uri")

    def test_falls_back_to_database_uri_when_mongodb_uri_missing(self) -> None:
        os.environ["MOZAIKS_ALLOW_NO_DB"] = "1"
        os.environ.pop("MOZAIKS_HOSTING_MODE", None)
        os.environ.pop("MOZAIKS_MANAGED", None)
        os.environ.pop("DATABASE_NAME", None)

        os.environ.pop("MONGODB_URI", None)
        os.environ["DATABASE_URI"] = "mongodb://localhost:27017/db_from_database_uri"

        database = _fresh_database_module()
        self.assertEqual(database.MONGO_URI, "mongodb://localhost:27017/db_from_database_uri")
        self.assertEqual(database.DATABASE_NAME, "db_from_database_uri")

    def test_database_name_env_overrides_uri_database(self) -> None:
        os.environ["MOZAIKS_ALLOW_NO_DB"] = "1"
        os.environ.pop("MOZAIKS_HOSTING_MODE", None)
        os.environ.pop("MOZAIKS_MANAGED", None)

        os.environ["MONGODB_URI"] = "mongodb://localhost:27017/db_from_uri"
        os.environ["DATABASE_NAME"] = "db_override"

        database = _fresh_database_module()
        self.assertEqual(database.DATABASE_NAME, "db_override")


if __name__ == "__main__":
    unittest.main()

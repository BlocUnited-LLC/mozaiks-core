# backend/tests/test_database_env.py
"""
Tests for database environment variable handling.

Current implementation uses DATABASE_URI only (MONGODB_URI was removed).
The database name is derived from the URI path, not a separate env var.
"""
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
            "DATABASE_URI": os.getenv("DATABASE_URI"),
            "MOZAIKS_ALLOW_NO_DB": os.getenv("MOZAIKS_ALLOW_NO_DB"),
            "MOZAIKS_LOAD_DOTENV": os.getenv("MOZAIKS_LOAD_DOTENV"),
            "MOZAIKS_HOSTING_MODE": os.getenv("MOZAIKS_HOSTING_MODE"),
            "MOZAIKS_MANAGED": os.getenv("MOZAIKS_MANAGED"),
            "ENV": os.getenv("ENV"),
        }
        os.environ["MOZAIKS_LOAD_DOTENV"] = "0"

    def tearDown(self) -> None:
        for key, value in self._snapshot.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value

    def test_uses_database_uri_env_var(self) -> None:
        """DATABASE_URI env var configures the MongoDB connection."""
        os.environ["MOZAIKS_ALLOW_NO_DB"] = "1"
        os.environ.pop("MOZAIKS_HOSTING_MODE", None)
        os.environ.pop("MOZAIKS_MANAGED", None)

        os.environ["DATABASE_URI"] = "mongodb://localhost:27017/test_db"

        database = _fresh_database_module()
        self.assertEqual(database.MONGO_URI, "mongodb://localhost:27017/test_db")

    def test_defaults_to_localhost_when_no_uri(self) -> None:
        """Falls back to localhost when DATABASE_URI is not set."""
        os.environ["MOZAIKS_ALLOW_NO_DB"] = "1"
        os.environ.pop("MOZAIKS_HOSTING_MODE", None)
        os.environ.pop("MOZAIKS_MANAGED", None)
        os.environ.pop("DATABASE_URI", None)

        database = _fresh_database_module()
        self.assertEqual(database.MONGO_URI, "mongodb://localhost:27017/mozaiks")

    def test_production_env_uses_client_db(self) -> None:
        """Production environment uses 'client' database."""
        os.environ["MOZAIKS_ALLOW_NO_DB"] = "1"
        os.environ.pop("MOZAIKS_HOSTING_MODE", None)
        os.environ.pop("MOZAIKS_MANAGED", None)
        os.environ["DATABASE_URI"] = "mongodb://localhost:27017/mozaiks"
        os.environ["ENV"] = "production"

        database = _fresh_database_module()
        # Production uses 'client' database
        self.assertEqual(database.db.name, "client")

    def test_non_production_uses_mozaikscore_db(self) -> None:
        """Non-production environments use 'MozaiksCore' database."""
        os.environ["MOZAIKS_ALLOW_NO_DB"] = "1"
        os.environ.pop("MOZAIKS_HOSTING_MODE", None)
        os.environ.pop("MOZAIKS_MANAGED", None)
        os.environ["DATABASE_URI"] = "mongodb://localhost:27017/mozaiks"
        os.environ["ENV"] = "development"

        database = _fresh_database_module()
        self.assertEqual(database.db.name, "MozaiksCore")


if __name__ == "__main__":
    unittest.main()

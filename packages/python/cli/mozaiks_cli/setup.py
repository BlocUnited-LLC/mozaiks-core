#!/usr/bin/env python
"""
Mozaiks Core Setup CLI

Provides explicit database initialization and health checks for OSS developers.
The app works without running this (auto-init on startup), but some developers
prefer explicit setup steps.

Usage:
    python -m cli.setup --init-db
    python -m cli.setup --check-db
    python -m cli.setup --seed-test-data
    python -m cli.setup --list-plugins
    python -m cli.setup --version

Contract Version: 1.0
"""

import asyncio
import argparse
import sys
import os
import json
from pathlib import Path
from datetime import datetime, timezone

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

__version__ = "1.0.0"


def print_banner():
    """Print Mozaiks banner."""
    print("""
    ╔══════════════════════════════════════════╗
    ║         Mozaiks Core Setup CLI           ║
    ║              Version 1.0.0               ║
    ╚══════════════════════════════════════════╝
    """)


async def init_db() -> int:
    """
    Initialize database collections and indexes.

    Creates:
    - settings collection indexes
    - entitlement_usage collection indexes (Contract v1.0)
    - enterprise collection indexes

    Returns:
        0 on success, 1 on failure
    """
    print("🔧 Initializing Mozaiks database...\n")

    try:
        from mozaiks_infra.config.database import (
            initialize_database,
            verify_connection,
            db,
            entitlement_usage_collection,
            settings_collection,
        )

        # Step 1: Verify connection
        print("   [1/3] Verifying MongoDB connection...")
        await verify_connection()
        print("   ✅ MongoDB connection verified")
        print(f"   📍 Database: {db.name}")

        # Step 2: Run full initialization
        print("\n   [2/3] Creating collections and indexes...")
        await initialize_database()
        print("   ✅ Collections initialized")

        # Step 3: Verify indexes
        print("\n   [3/3] Verifying indexes...")

        if entitlement_usage_collection is not None:
            indexes = await entitlement_usage_collection.index_information()
            print(f"   ✅ entitlement_usage: {len(indexes)} indexes")

        if settings_collection is not None:
            indexes = await settings_collection.index_information()
            print(f"   ✅ settings: {len(indexes)} indexes")

        print("\n" + "=" * 50)
        print("✨ Database initialization complete!")
        print("=" * 50)
        return 0

    except ImportError as e:
        print(f"\n❌ Import error: {e}")
        print("   Make sure you're running from the runtime/ai directory")
        return 1
    except Exception as e:
        print(f"\n❌ Error: {e}")
        return 1


async def check_db() -> int:
    """
    Verify database connection and indexes.

    Useful for CI/CD health checks.

    Returns:
        0 if healthy, 1 if issues found
    """
    print("🔍 Checking Mozaiks database health...\n")

    issues = []

    try:
        from mozaiks_infra.config.database import (
            db,
            verify_connection,
            entitlement_usage_collection,
            settings_collection,
            subscriptions_collection,
        )

        # Check 1: Connection
        print("   [1/4] Checking MongoDB connection...")
        try:
            await verify_connection(force=True)
            print("   ✅ Connection OK")
        except Exception as e:
            print(f"   ❌ Connection FAILED: {e}")
            issues.append("MongoDB connection failed")

        # Check 2: Database exists
        print("\n   [2/4] Checking database...")
        if db is not None:
            print(f"   ✅ Database: {db.name}")
        else:
            print("   ❌ Database not initialized")
            issues.append("Database not initialized")

        # Check 3: Collections
        print("\n   [3/4] Checking collections...")
        collections = await db.list_collection_names() if db else []

        required_collections = ["subscriptions", "settings", "entitlement_usage"]
        for coll in required_collections:
            if coll in collections:
                print(f"   ✅ {coll}")
            else:
                print(f"   ⚠️  {coll} (will be created on first use)")

        # Check 4: Indexes
        print("\n   [4/4] Checking indexes...")

        if entitlement_usage_collection is not None:
            indexes = await entitlement_usage_collection.index_information()
            if len(indexes) > 1:
                print(f"   ✅ entitlement_usage: {len(indexes)} indexes")
            else:
                print("   ⚠️  entitlement_usage: indexes not created")
                issues.append("entitlement_usage indexes missing")

        if settings_collection is not None:
            indexes = await settings_collection.index_information()
            if len(indexes) > 1:
                print(f"   ✅ settings: {len(indexes)} indexes")
            else:
                print("   ⚠️  settings: indexes not created")
                issues.append("settings indexes missing")

        # Summary
        print("\n" + "=" * 50)
        if not issues:
            print("✨ Database is healthy!")
            print("=" * 50)
            return 0
        else:
            print(f"⚠️  Found {len(issues)} issue(s):")
            for issue in issues:
                print(f"   - {issue}")
            print("\nRun --init-db to fix.")
            print("=" * 50)
            return 1

    except ImportError as e:
        print(f"\n❌ Import error: {e}")
        return 1
    except Exception as e:
        print(f"\n❌ Error: {e}")
        return 1


async def seed_test_data() -> int:
    """
    Seed test data for development.

    Creates:
    - Test user subscription (user_id: test_user_001, plan: pro)
    - Test entitlement usage record

    Returns:
        0 on success, 1 on failure
    """
    print("🌱 Seeding test data for development...\n")

    try:
        from mozaiks_infra.config.database import subscriptions_collection, entitlement_usage_collection

        now = datetime.now(timezone.utc)

        # Seed 1: Test subscription
        print("   [1/2] Creating test subscription...")
        test_subscription = {
            "user_id": "test_user_001",
            "app_id": "dev_app",
            "plan": "pro",
            "status": "active",
            "billing_cycle": "monthly",
            "plugin_tiers": {
                "video_generator": "pro",
                "dream_journal": "basic",
            },
            "created_at": now.isoformat(),
            "updated_at": now.isoformat(),
            "_test_data": True,
        }

        await subscriptions_collection.update_one(
            {"user_id": "test_user_001"},
            {"$set": test_subscription},
            upsert=True
        )
        print("   ✅ Test subscription created")
        print("      user_id: test_user_001")
        print("      plan: pro")
        print("      plugin_tiers: video_generator=pro, dream_journal=basic")

        # Seed 2: Test entitlement usage
        print("\n   [2/2] Creating test entitlement usage...")
        test_usage = {
            "app_id": "dev_app",
            "user_id": "test_user_001",
            "plugin": "video_generator",
            "limit_key": "videos_per_month",
            "period": now.strftime("%Y-%m"),
            "period_type": "monthly",
            "used": 2,
            "limit": 30,
            "first_use": now.isoformat(),
            "last_use": now.isoformat(),
            "updated_at": now.isoformat(),
            "_test_data": True,
        }

        await entitlement_usage_collection.update_one(
            {
                "app_id": "dev_app",
                "user_id": "test_user_001",
                "plugin": "video_generator",
                "limit_key": "videos_per_month"
            },
            {"$set": test_usage},
            upsert=True
        )
        print("   ✅ Test entitlement usage created")
        print("      plugin: video_generator")
        print("      videos_per_month: 2/30 used")

        print("\n" + "=" * 50)
        print("✨ Test data ready!")
        print("\nTo clean up test data later:")
        print("  db.subscriptions.deleteMany({_test_data: true})")
        print("  db.entitlement_usage.deleteMany({_test_data: true})")
        print("=" * 50)
        return 0

    except ImportError as e:
        print(f"\n❌ Import error: {e}")
        return 1
    except Exception as e:
        print(f"\n❌ Error: {e}")
        return 1


async def list_plugins() -> int:
    """
    List installed plugins and their entitlement status.

    Returns:
        0 on success, 1 on failure
    """
    print("📦 Listing installed plugins...\n")

    try:
        # Try to find plugin registry
        config_paths = [
            Path("config/plugin_registry.json"),
            Path("../config/plugin_registry.json"),
            Path(__file__).parent.parent / "config" / "plugin_registry.json",
        ]

        registry = None
        registry_path = None
        for path in config_paths:
            if path.exists():
                registry_path = path
                with open(path, "r") as f:
                    registry = json.load(f)
                break

        if not registry:
            print("   ⚠️  No plugin_registry.json found")
            print("   Plugins will be discovered on app startup")
            return 0

        print(f"   Registry: {registry_path}\n")

        plugins = registry.get("plugins", [])
        if not plugins:
            print("   No plugins registered")
            return 0

        # Try to check for entitlements
        try:
            from mozaiks_platform.entitlements.loader import has_entitlements_yaml
            can_check_entitlements = True
        except ImportError:
            can_check_entitlements = False

        print(f"   {'Plugin':<25} {'Enabled':<10} {'Entitlements':<15}")
        print("   " + "-" * 50)

        for plugin in plugins:
            name = plugin.get("name", "unknown")
            enabled = "Yes" if plugin.get("enabled", True) else "No"

            if can_check_entitlements:
                has_ent = "Yes" if has_entitlements_yaml(name) else "No"
            else:
                has_ent = "?"

            print(f"   {name:<25} {enabled:<10} {has_ent:<15}")

        print(f"\n   Total: {len(plugins)} plugin(s)")
        return 0

    except Exception as e:
        print(f"\n❌ Error: {e}")
        return 1


def show_version():
    """Show version information."""
    print(f"Mozaiks Core CLI v{__version__}")
    print(f"Entitlement Contract: v1.0")
    print(f"Python: {sys.version}")


def main():
    """Main CLI entry point."""
    parser = argparse.ArgumentParser(
        description="Mozaiks Core Setup CLI",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python -m cli.setup --init-db           Initialize database
  python -m cli.setup --check-db          Verify database health
  python -m cli.setup --seed-test-data    Create test data for development
  python -m cli.setup --list-plugins      List installed plugins

For CI/CD pipelines:
  python -m cli.setup --check-db && echo "DB OK" || echo "DB FAIL"

Environment Variables:
  DATABASE_URI    MongoDB connection string (default: mongodb://localhost:27017/mozaiks)
  MOZAIKS_APP_ID  App identifier (default: dev_app)
        """
    )

    parser.add_argument(
        "--init-db",
        action="store_true",
        help="Initialize database collections and indexes"
    )
    parser.add_argument(
        "--check-db",
        action="store_true",
        help="Check database connection and indexes (for health checks)"
    )
    parser.add_argument(
        "--seed-test-data",
        action="store_true",
        help="Create test subscription and usage data for development"
    )
    parser.add_argument(
        "--list-plugins",
        action="store_true",
        help="List installed plugins and their entitlement status"
    )
    parser.add_argument(
        "--version", "-v",
        action="store_true",
        help="Show version information"
    )

    args = parser.parse_args()

    # Handle version flag
    if args.version:
        show_version()
        return 0

    # If no arguments, show help
    if not any([args.init_db, args.check_db, args.seed_test_data, args.list_plugins]):
        print_banner()
        parser.print_help()
        return 0

    # Run the requested command
    if args.init_db:
        return asyncio.run(init_db())
    elif args.check_db:
        return asyncio.run(check_db())
    elif args.seed_test_data:
        return asyncio.run(seed_test_data())
    elif args.list_plugins:
        return asyncio.run(list_plugins())


if __name__ == "__main__":
    sys.exit(main())

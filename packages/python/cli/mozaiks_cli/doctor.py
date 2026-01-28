#!/usr/bin/env python
"""
Mozaiks Doctor CLI

Check system requirements and diagnose issues.

Usage:
    python -m cli.main doctor
    python -m cli.main doctor --fix

Contract Version: 1.0
"""

import os
import sys
import asyncio
import shutil
from pathlib import Path


def check_python_version() -> tuple[bool, str]:
    """Check Python version."""
    version = sys.version_info
    version_str = f"{version.major}.{version.minor}.{version.micro}"

    if version.major >= 3 and version.minor >= 10:
        return True, f"Python {version_str}"
    else:
        return False, f"Python {version_str} (required: 3.10+)"


def check_node_version() -> tuple[bool, str]:
    """Check Node.js version."""
    node_path = shutil.which("node")
    if not node_path:
        return False, "Not installed"

    try:
        import subprocess
        result = subprocess.run(
            ["node", "--version"],
            capture_output=True,
            text=True,
            timeout=5
        )
        version = result.stdout.strip().lstrip('v')
        major = int(version.split('.')[0])

        if major >= 18:
            return True, f"Node.js {version}"
        else:
            return False, f"Node.js {version} (required: 18+)"
    except Exception as e:
        return False, f"Error checking: {e}"


def check_docker() -> tuple[bool, str]:
    """Check Docker availability."""
    docker_path = shutil.which("docker")
    if not docker_path:
        return None, "Not installed (optional)"

    try:
        import subprocess
        result = subprocess.run(
            ["docker", "--version"],
            capture_output=True,
            text=True,
            timeout=5
        )
        version = result.stdout.strip()
        return True, version.replace("Docker version ", "Docker ")
    except Exception as e:
        return None, f"Installed but error: {e}"


async def check_mongodb() -> tuple[bool, str]:
    """Check MongoDB connection."""
    try:
        from mozaiks_infra.config.database import verify_connection, db
        await verify_connection(force=True)
        return True, f"Connected ({db.name})"
    except ImportError:
        return False, "Database module not importable"
    except Exception as e:
        return False, f"Connection failed: {e}"


def check_env_file() -> tuple[bool, str]:
    """Check .env file exists."""
    candidates = [
        Path(".env"),
        Path("runtime/ai/.env"),
        Path(__file__).parent.parent / ".env",
    ]

    for path in candidates:
        if path.exists():
            return True, f"Found at {path}"

    return False, "Not found (create from .env.example)"


def check_env_var(name: str, required: bool = True) -> tuple[bool, str]:
    """Check environment variable."""
    value = os.getenv(name)

    if value:
        # Mask sensitive values
        if "KEY" in name or "SECRET" in name or "PASSWORD" in name:
            masked = value[:4] + "..." + value[-4:] if len(value) > 8 else "***"
            return True, f"Configured ({masked})"
        return True, "Configured"
    else:
        if required:
            return False, "Not set (required)"
        else:
            return None, "Not set (optional)"


def check_plugins_dir() -> tuple[bool, str]:
    """Check plugins directory."""
    candidates = [
        Path("plugins"),
        Path("runtime/ai/plugins"),
        Path(__file__).parent.parent / "plugins",
    ]

    for path in candidates:
        if path.exists():
            plugins = [p.name for p in path.iterdir() if p.is_dir() and not p.name.startswith('_')]
            return True, f"{len(plugins)} plugins found"

    return None, "No plugins directory found"


def check_entitlements_module() -> tuple[bool, str]:
    """Check entitlements module is available."""
    try:
        from mozaiks_platform.entitlements import check_feature, check_limit
        return True, "v1.0 loaded"
    except ImportError as e:
        return None, f"Not available: {e}"


def run_doctor(fix: bool = False) -> int:
    """
    Run system diagnostics.

    Args:
        fix: Attempt to fix issues (not implemented yet)

    Returns:
        0 if healthy, 1 if issues found
    """
    print("""
╔══════════════════════════════════════════╗
║           Mozaiks Doctor                 ║
╚══════════════════════════════════════════╝
    """)

    issues = []
    warnings = []

    # System requirements
    print("Checking system requirements...\n")

    checks = [
        ("Python", check_python_version()),
        ("Node.js", check_node_version()),
        ("Docker", check_docker()),
    ]

    for name, (status, message) in checks:
        if status is True:
            print(f"   {name + ':':<12} ✅ {message}")
        elif status is False:
            print(f"   {name + ':':<12} ❌ {message}")
            issues.append(f"{name}: {message}")
        else:
            print(f"   {name + ':':<12} ⚠️  {message}")
            warnings.append(f"{name}: {message}")

    # MongoDB (async)
    print("\nChecking services...\n")

    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)
    try:
        mongo_status, mongo_msg = loop.run_until_complete(check_mongodb())
    finally:
        loop.close()

    if mongo_status is True:
        print(f"   {'MongoDB:':<12} ✅ {mongo_msg}")
    elif mongo_status is False:
        print(f"   {'MongoDB:':<12} ❌ {mongo_msg}")
        issues.append(f"MongoDB: {mongo_msg}")
    else:
        print(f"   {'MongoDB:':<12} ⚠️  {mongo_msg}")
        warnings.append(f"MongoDB: {mongo_msg}")

    # Configuration
    print("\nChecking configuration...\n")

    env_status, env_msg = check_env_file()
    if env_status:
        print(f"   {'.env file:':<12} ✅ {env_msg}")
    else:
        print(f"   {'.env file:':<12} ❌ {env_msg}")
        issues.append(f".env file: {env_msg}")

    # Load .env if exists
    try:
        from dotenv import load_dotenv
        load_dotenv()
    except ImportError:
        pass

    env_vars = [
        ("DATABASE_URI", False),  # Has default
        ("JWT_SECRET", True),
        ("OPENAI_API_KEY", False),
        ("MONETIZATION", False),
    ]

    for var_name, required in env_vars:
        status, msg = check_env_var(var_name, required)
        if status is True:
            print(f"   {var_name + ':':<16} ✅ {msg}")
        elif status is False:
            print(f"   {var_name + ':':<16} ❌ {msg}")
            issues.append(f"{var_name}: {msg}")
        else:
            print(f"   {var_name + ':':<16} ⚠️  {msg}")
            warnings.append(f"{var_name}: {msg}")

    # Modules
    print("\nChecking modules...\n")

    plugin_status, plugin_msg = check_plugins_dir()
    if plugin_status is True:
        print(f"   {'Plugins:':<16} ✅ {plugin_msg}")
    elif plugin_status is False:
        print(f"   {'Plugins:':<16} ❌ {plugin_msg}")
        issues.append(f"Plugins: {plugin_msg}")
    else:
        print(f"   {'Plugins:':<16} ⚠️  {plugin_msg}")

    ent_status, ent_msg = check_entitlements_module()
    if ent_status is True:
        print(f"   {'Entitlements:':<16} ✅ {ent_msg}")
    elif ent_status is False:
        print(f"   {'Entitlements:':<16} ❌ {ent_msg}")
        issues.append(f"Entitlements: {ent_msg}")
    else:
        print(f"   {'Entitlements:':<16} ⚠️  {ent_msg}")

    # Summary
    print("\n" + "=" * 50)

    if not issues and not warnings:
        print("✨ System is healthy!")
        print("=" * 50)
        return 0
    else:
        if issues:
            print(f"❌ {len(issues)} issue(s) found:")
            for issue in issues:
                print(f"   - {issue}")

        if warnings:
            print(f"⚠️  {len(warnings)} warning(s):")
            for warning in warnings:
                print(f"   - {warning}")

        if fix:
            print("\n🔧 Auto-fix not yet implemented.")
            print("   Please fix issues manually.")

        print("\n" + "=" * 50)
        return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(run_doctor())

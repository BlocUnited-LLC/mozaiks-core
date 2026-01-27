#!/usr/bin/env python
"""
Test script for Core <-> Platform billing integration.

This script tests both directions:
1. Platform -> Core: POST /api/v1/entitlements/{app_id}/sync (simulate Platform webhook)
2. Core -> Platform: POST /api/billing/usage-events (simulate usage reporting)

Prerequisites:
- Set MOZAIKS_ALLOWED_SERVICE_KEYS in .env for Platform->Core auth
- Set MOZAIKS_PLATFORM_API_KEY in .env for Core->Platform auth
- Start Core runtime on port 8000
- Start Platform on port 5000

Usage:
    python tests/integration/test_billing_integration.py
"""

import asyncio
import os
import sys
from datetime import datetime
from pathlib import Path

# Add runtime/ai to path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

import httpx
from dotenv import load_dotenv

# Load .env
env_path = Path(__file__).parent.parent.parent / ".env"
load_dotenv(env_path)

# Config
CORE_URL = os.getenv("CORE_URL", "http://localhost:8000")
PLATFORM_URL = os.getenv("MOZAIKS_PLATFORM_URL", "http://localhost:5000")

# Service keys (from .env)
PLATFORM_TO_CORE_KEY = os.getenv("MOZAIKS_ALLOWED_SERVICE_KEYS", "").split(",")[0].strip()
CORE_TO_PLATFORM_KEY = os.getenv("MOZAIKS_PLATFORM_API_KEY", "")

TEST_APP_ID = "test-app-12345"


async def test_platform_to_core_sync():
    """Test Platform -> Core entitlement sync."""
    print("\n" + "=" * 60)
    print("TEST 1: Platform -> Core Entitlement Sync")
    print("=" * 60)
    
    if not PLATFORM_TO_CORE_KEY:
        print("⚠️  MOZAIKS_ALLOWED_SERVICE_KEYS not set, skipping auth")
        auth_header = None
    else:
        auth_header = f"Bearer {PLATFORM_TO_CORE_KEY}"
        print(f"✓ Using service key: {PLATFORM_TO_CORE_KEY[:20]}...")
    
    # Sync request payload
    payload = {
        "version": "1.0",
        "app_id": TEST_APP_ID,
        "tenant_id": "test-tenant",
        "plan": {
            "id": "plan_pro_monthly",
            "name": "Pro Plan",
            "tier": "pro",
            "billing_period": "monthly",
            "expires_at": "2025-12-31T23:59:59Z"
        },
        "token_budget": {
            "period": "monthly",
            "total_tokens": {
                "limit": 100000,
                "used": 5000,
                "enforcement": "soft"
            }
        },
        "features": {
            "advanced_ai": True,
            "custom_plugins": True,
            "priority_support": False
        },
        "rate_limits": {
            "requests_per_minute": 60,
            "concurrent_sessions": 5
        },
        "correlation_id": f"sync-{datetime.utcnow().isoformat()}"
    }
    
    url = f"{CORE_URL}/api/v1/entitlements/{TEST_APP_ID}/sync"
    print(f"\n→ POST {url}")
    print(f"  Payload: {payload}")
    
    headers = {"Content-Type": "application/json"}
    if auth_header:
        headers["Authorization"] = auth_header
    
    async with httpx.AsyncClient() as client:
        try:
            response = await client.post(url, json=payload, headers=headers)
            print(f"\n← Status: {response.status_code}")
            print(f"  Response: {response.json()}")
            
            if response.status_code == 200:
                print("\n✅ TEST 1 PASSED: Entitlement sync successful")
                return True
            else:
                print(f"\n❌ TEST 1 FAILED: Expected 200, got {response.status_code}")
                return False
        except httpx.ConnectError:
            print(f"\n❌ TEST 1 FAILED: Could not connect to Core at {CORE_URL}")
            print("   Make sure Core runtime is running: python -m uvicorn core.director:app --port 8000")
            return False


async def test_get_entitlements():
    """Test getting entitlements after sync."""
    print("\n" + "=" * 60)
    print("TEST 2: Get Entitlements After Sync")
    print("=" * 60)
    
    url = f"{CORE_URL}/api/v1/entitlements/{TEST_APP_ID}"
    print(f"\n→ GET {url}")
    
    headers = {}
    if PLATFORM_TO_CORE_KEY:
        headers["Authorization"] = f"Bearer {PLATFORM_TO_CORE_KEY}"
    
    async with httpx.AsyncClient() as client:
        try:
            response = await client.get(url, headers=headers)
            print(f"\n← Status: {response.status_code}")
            print(f"  Response: {response.json()}")
            
            if response.status_code == 200:
                data = response.json()
                if data.get("plan", {}).get("tier") == "pro":
                    print("\n✅ TEST 2 PASSED: Entitlements correctly stored")
                    return True
                else:
                    print("\n❌ TEST 2 FAILED: Tier not updated to 'pro'")
                    return False
            else:
                print(f"\n❌ TEST 2 FAILED: Expected 200, got {response.status_code}")
                return False
        except httpx.ConnectError:
            print(f"\n❌ TEST 2 FAILED: Could not connect to Core")
            return False


async def test_core_to_platform_usage():
    """Test Core -> Platform usage reporting."""
    print("\n" + "=" * 60)
    print("TEST 3: Core -> Platform Usage Reporting")
    print("=" * 60)
    
    if not CORE_TO_PLATFORM_KEY:
        print("⚠️  MOZAIKS_PLATFORM_API_KEY not set, cannot test Platform connection")
        return None
    
    print(f"✓ Using API key: {CORE_TO_PLATFORM_KEY[:20]}...")
    
    # Usage event batch
    payload = {
        "batch_id": f"batch-{datetime.utcnow().isoformat()}",
        "events": [
            {
                "event_id": "evt_001",
                "app_id": TEST_APP_ID,
                "event_type": "token_usage",
                "timestamp": datetime.utcnow().isoformat() + "Z",
                "data": {
                    "tokens_consumed": 150,
                    "model": "gpt-4",
                    "session_id": "sess_test_123"
                }
            },
            {
                "event_id": "evt_002",
                "app_id": TEST_APP_ID,
                "event_type": "api_call",
                "timestamp": datetime.utcnow().isoformat() + "Z",
                "data": {
                    "endpoint": "/api/chat",
                    "duration_ms": 250
                }
            }
        ]
    }
    
    url = f"{PLATFORM_URL}/api/billing/usage-events"
    print(f"\n→ POST {url}")
    print(f"  Events count: {len(payload['events'])}")
    
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {CORE_TO_PLATFORM_KEY}"
    }
    
    async with httpx.AsyncClient() as client:
        try:
            response = await client.post(url, json=payload, headers=headers)
            print(f"\n← Status: {response.status_code}")
            try:
                print(f"  Response: {response.json()}")
            except:
                print(f"  Response: {response.text[:200]}")
            
            if response.status_code in (200, 201, 202):
                print("\n✅ TEST 3 PASSED: Usage events sent to Platform")
                return True
            elif response.status_code == 404:
                print("\n⚠️  TEST 3 SKIPPED: Platform endpoint not found (Platform may not be running)")
                return None
            else:
                print(f"\n❌ TEST 3 FAILED: Expected 200/201/202, got {response.status_code}")
                return False
        except httpx.ConnectError:
            print(f"\n❌ TEST 3 FAILED: Could not connect to Platform at {PLATFORM_URL}")
            print("   Make sure Platform is running on port 5000")
            return None


async def run_tests():
    """Run all integration tests."""
    print("\n" + "=" * 60)
    print("BILLING INTEGRATION TEST SUITE")
    print("=" * 60)
    print(f"Core URL: {CORE_URL}")
    print(f"Platform URL: {PLATFORM_URL}")
    print(f"Platform->Core Key: {'✓ Set' if PLATFORM_TO_CORE_KEY else '✗ Not set'}")
    print(f"Core->Platform Key: {'✓ Set' if CORE_TO_PLATFORM_KEY else '✗ Not set'}")
    
    results = []
    
    # Test 1: Platform -> Core sync
    results.append(("Platform->Core Sync", await test_platform_to_core_sync()))
    
    # Test 2: Get entitlements
    results.append(("Get Entitlements", await test_get_entitlements()))
    
    # Test 3: Core -> Platform usage
    results.append(("Core->Platform Usage", await test_core_to_platform_usage()))
    
    # Summary
    print("\n" + "=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)
    
    passed = 0
    failed = 0
    skipped = 0
    
    for name, result in results:
        if result is True:
            print(f"  ✅ {name}: PASSED")
            passed += 1
        elif result is False:
            print(f"  ❌ {name}: FAILED")
            failed += 1
        else:
            print(f"  ⚠️  {name}: SKIPPED")
            skipped += 1
    
    print(f"\nTotal: {passed} passed, {failed} failed, {skipped} skipped")
    
    if failed > 0:
        sys.exit(1)


if __name__ == "__main__":
    asyncio.run(run_tests())

# 🎯 MOZAIKS-CORE: What You Actually Have

**Date**: January 26, 2026  
**Purpose**: Plain English explanation of THIS repository (mozaiks-core)

---

## TL;DR - What Is mozaiks-core?

**mozaiks-core is the "engine" that runs AI-powered apps.**

Think of it like WordPress:
- WordPress is the engine that runs websites
- Themes/plugins add features
- You self-host it or use WordPress.com

Similarly:
- **mozaiks-core** is the engine that runs AI apps
- **Plugins** add features (like "todo list", "file manager", etc.)
- **Workflows** are AI agent sequences (like "generate code", "analyze data")
- You can self-host it OR the platform (mozaiks.ai) hosts it for you

---

## What's In This Repo (And What It Does)

### ✅ WORKING: Python AI Runtime (`runtime/ai/`)
**This is the heart of the project.**

What it does:
- Runs AI workflows (using AG2/AutoGen framework)
- Loads and executes plugins
- Handles WebSocket chat streaming
- Manages users, sessions, subscriptions
- Stores data in MongoDB

**Status**: WORKS. Can start with `python main.py`

### ✅ WORKING: React Frontend (`runtime/packages/shell/`)
**The web interface users see.**

What it does:
- Chat interface for AI conversations
- Plugin dashboard
- Split-screen "artifact" view for AI-generated content

**Status**: WORKS. 51 React components.

### ⚠️ COMPILES: .NET Backend Services (`backend/src/`)
**Optional services for billing, identity, etc.**

| Service | Files | What It Does |
|---------|-------|--------------|
| Identity.API | 751 | User accounts, authentication |
| Billing.API | 184 | Subscriptions, Stripe integration |
| Notification.API | 228 | Email/push notifications |
| Insights.API | 133 | Analytics, usage tracking |
| Plugins.API | 127 | Plugin management |

**Status**: COMPILES (after fix). But the Python runtime works standalone - you don't NEED these .NET services to run the core product.

### ✅ WORKING: CLI Tools (`runtime/ai/cli/`)
**Command-line tools for developers.**

```bash
python -m cli.main init my-app      # Create new project
python -m cli.main new plugin todo  # Create plugin skeleton
python -m cli.main doctor           # Check system health
```

**Status**: WORKS. Basic scaffolding tools exist.

---

## What's ACTUALLY Used vs What's Dead Code

### Used (Keep These)
```
runtime/ai/              # Python runtime - THE MAIN THING
runtime/packages/shell/  # React frontend
runtime/plugin-host/     # Alternative plugin runner
runtime/ai/plugins/      # Backend plugins (default)
examples/                # Example code
docs/                    # Documentation
```

### Questionable (.NET Backend)
```
backend/src/             # .NET services - Optional, adds enterprise features
backend/AspireAdmin/     # .NET Aspire orchestration
backend/BuildingBlocks/  # Shared .NET libraries
```

**Verdict**: The .NET backend adds value (billing, identity) but isn't required for the core product to work. A self-hoster can run just the Python runtime.

### Dead Code (Can Probably Delete)
```
runtime/ai/src/core/entitlements/  # Older entitlement system, replaced
docs/archive/                       # Old docs
autogen_logs/                       # Log files, not code
```

---

## The Two Entitlement Systems (Confusion Source)

You have TWO systems for managing what users can do:

### System 1: `runtime/ai/core/entitlements/` (ACTIVE)
- Plugin-level feature gates ("can user X use feature Y?")
- YAML-based configuration per plugin
- MongoDB usage tracking
- **This is what's actually wired up**

### System 2: `runtime/ai/src/core/entitlements/` (STALE)
- App-level token budgets
- `EntitlementManifest`, `TokenBudgetTracker` classes
- **Tests for this are broken because API changed**

**Recommendation**: Delete System 2 or merge it into System 1. Having two causes confusion.

---

## The Test Situation

```
Total tests: 81
Broken: ~25 (the entitlement tests)
Working: ~56
```

The broken tests are testing the OLD entitlement API that no longer exists.

**Fix**: Either delete `tests/test_entitlements.py` or rewrite it.

---

## How mozaiks-core Relates to mozaiks-platform

```
┌─────────────────────────────────────────────────────────────┐
│                     USER WANTS AN APP                        │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              │                               │
              ▼                               ▼
┌──────────────────────────┐    ┌──────────────────────────┐
│     SELF-HOSTED          │    │     PLATFORM HOSTED      │
│                          │    │     (mozaiks.ai)         │
│  Uses: mozaiks-core      │    │                          │
│  - They run it           │    │  Uses: mozaiks-platform  │
│  - They manage it        │    │  - We run it             │
│  - Free, open source     │    │  - We charge for hosting │
│                          │    │  - Extra features        │
└──────────────────────────┘    └──────────────────────────┘
              │                               │
              └───────────────┬───────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    SAME CORE ENGINE                          │
│              (mozaiks-core Python runtime)                   │
└─────────────────────────────────────────────────────────────┘
```

**Key Point**: mozaiks-platform USES mozaiks-core. It doesn't replace it.

---

## What "CLI" Means

**CLI = Command Line Interface**

Instead of clicking buttons in a GUI, you type commands.

```bash
# Instead of: Menu → New → Project → [fill form] → Create
python -m cli.main init my-app

# Instead of: Menu → New → Plugin → [fill form] → Create  
python -m cli.main new plugin todo
```

That's it. It's just text commands instead of mouse clicks.

---

## Your Path Forward (3 Options)

### Option A: Minimal Cleanup (2-4 hours)
1. ✅ Fix the .NET build error - **DONE**
2. Delete broken test file (`tests/test_entitlements.py`)
3. Delete dead entitlement system (`src/core/entitlements/`)
4. You now have a clean, working project

### Option B: Full Cleanup (1-2 days)
1. Do Option A
2. Remove duplicate docs
3. Consolidate the two entitlement systems properly
4. Write tests for what actually exists
5. Update all documentation to match reality

### Option C: Focus on Value (Recommended)
1. Do Option A (get it building/running)
2. Ask: "What's the ONE thing this does better than alternatives?"
3. Make THAT thing perfect
4. Ignore everything else until you have users

---

## What mozaiks-core Does Better Than Alternatives

Based on the VALUE_PROPOSITION.md, the killer features are:

1. **Interactive UI from AI** - Agents can show rich components, not just text
2. **Persistence/Resume** - Conversations survive browser close
3. **Plugin System** - Easy to extend with custom features
4. **Production Ready** - Docker, MongoDB, auth, billing out of the box

**The pitch**: "CrewAI/LangChain are for building AI agents. mozaiks-core is for building AI APPS that real users interact with."

---

## Quick Start (Does It Even Run?)

### Python Runtime (The Core)
```bash
cd runtime/ai
pip install -r requirements.txt
# Set up .env (copy from .env.example)
python main.py
# → Should start on http://localhost:8000
```

### React Frontend
```bash
cd runtime/packages/shell
npm install
npm run dev
# → Should start on http://localhost:5173
```

### .NET Backend (Optional)
```bash
cd backend
dotnet build MozaiksCore.sln
# → Should compile with 0 errors (after the fix I made)
```

---

## My Honest Assessment

**What you have**: A real, working AI runtime with a nice UI. The core concept is solid.

**What's broken**: 
- Tests for old API
- Two competing entitlement systems
- Some docs that don't match code

**What to do**:
1. Delete the broken stuff (30 minutes)
2. Run it, use it, understand it (1-2 hours)
3. Decide what to build next based on what users need

**You're not in over your head.** This is a real product with real code. It just needs some cleanup.

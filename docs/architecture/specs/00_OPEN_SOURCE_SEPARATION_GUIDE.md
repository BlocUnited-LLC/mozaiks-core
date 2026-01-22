# MozaiksAI Open-Source Separation Guide

**Date:** 2026-01-10  
**Purpose:** Identify what to keep in open-source `mozaiks-ai` vs private `mozaiks-workflows`

---

## TL;DR — What You Found

The **good news**: Your core/ChatUI runtime is already 95% workflow-agnostic. There are only **3 areas** with workflow-dependent references, and 2 of them are already dynamically loaded via env vars.

---

## Summary of Workflow-Dependent Logic

| Location | Type | Status | What Needs to Change |
|----------|------|--------|----------------------|
| **`core/transport/simple_transport.py`** | Hardcoded default import path | ✅ FIXED | Default now empty, no workflow reference |
| **`ChatUI/src/workflows/`** | Frontend workflow UI components | ⚠️ Your proprietary code | Contains AppGenerator, AgentGenerator, ValueEngine components |
| **`workflows/`** | Backend workflow definitions | ⚠️ Your proprietary code | All 5 workflows (ValueEngine, DesignDocs, AgentGenerator, AppGenerator, ValidationEngine) |
| **`tests/`** | Test files importing workflow tools | ⚠️ Proprietary test coverage | 6 test files import from `workflows.AppGenerator.*` |

---

## Detailed Findings

### 1. Core Runtime (`core/`) — MOSTLY CLEAN ✅

**Workflow-Agnostic (Keep as Open-Source):**
- `core/workflow/orchestration_patterns.py` — Generic orchestration engine
- `core/workflow/execution/` — Lifecycle, termination handlers (uses `workflow_name` parameter, not hardcoded)
- `core/workflow/outputs/structured.py` — Generic Pydantic output handling
- `core/transport/session_registry.py` — Generic session management
- `core/events/` — Event dispatching (workflow-agnostic)
- `core/tokens/` — Token accounting (generic)
- `core/artifacts/` — Attachment handling (workflow-agnostic)
- `core/data/` — Persistence (generic)

**One Hardcoded Reference — NOW FIXED ✅**

Previously lifecycle hooks were loaded via env var in `simple_transport.py`. This has been **refactored to be modular**.

**Old approach (removed):**
```python
# Environment variable defaulting to a specific workflow (NOT modular)
module_path = os.getenv("MOZAIKS_PLATFORM_BUILD_LIFECYCLE_MODULE", "workflows.AppGenerator...")
```

**New approach (modular, per-workflow):**
```yaml
# Each workflow declares its own lifecycle hooks in orchestrator.yaml
runtime_extensions:
  - kind: lifecycle_hooks
    entrypoint: workflows.AppGenerator.tools.platform.build_lifecycle:get_hooks
```

The runtime now loads lifecycle hooks from the workflow's `runtime_extensions` config via `get_workflow_lifecycle_hooks(workflow_name)` in `core/runtime/extensions.py`.

**Benefits:**
- Any workflow can declare lifecycle hooks (not just AppGenerator)
- Open-source users don't see dead env vars
- Platform integrations are fully declarative
- OrchestratorAgent now generates `runtime_extensions` in structured outputs

---

### 2. ChatUI Frontend — MIXED ⚠️

**Core Runtime Components (Keep):**
- `ChatUI/src/core/` — All workflow-agnostic
  - `WorkflowUIRouter.js` — Dynamic loader, NO hardcoded workflows ✅
  - `eventDispatcher.js` — Generic event handling ✅
  - `ui/` — Core reusable UI components ✅
- `ChatUI/src/workflows/index.js` — API-driven workflow registry, no hardcoding ✅
- `ChatUI/src/components/` — Generic chat components ✅
- `ChatUI/src/adapters/` — Generic transport adapters ✅

**Your Proprietary Components (Move to Private Repo):**

```
ChatUI/src/workflows/
├── AppGenerator/           ← YOUR PROPRIETARY CODE
│   ├── components/
│   │   ├── AppWorkbench.js
│   │   ├── BuildStatusArtifact.js
│   │   ├── CodeEditorArtifact.js
│   │   ├── E2BPreviewArtifact.js
│   │   └── ...
│   ├── hooks/
│   │   └── useE2BSandbox.js
│   ├── index.js
│   └── theme_config.json
│
├── AgentGenerator/         ← YOUR PROPRIETARY CODE
│   ├── components/
│   │   ├── ActionPlan.js
│   │   ├── AgentAPIKeysBundleInput.js
│   │   └── ...
│   └── theme_config.json
│
├── ValueEngine/            ← YOUR PROPRIETARY CODE
│   └── components/
│       ├── ConceptBlueprint.js
│       └── index.js
│
└── index.js               ← KEEP (API-driven registry)
```

---

### 3. Backend Workflows — ALL PROPRIETARY ⚠️

```
workflows/
├── AgentGenerator/         ← YOUR PROPRIETARY
├── AppGenerator/           ← YOUR PROPRIETARY
├── DesignDocs/            ← YOUR PROPRIETARY
├── ValueEngine/           ← YOUR PROPRIETARY
└── ValidationEngine/      ← YOUR PROPRIETARY (if exists)
```

---

### 4. Tests — Some Need Moving

**Tests That Import Proprietary Code (Move to Private):**
```
tests/test_export_app_code_gating.py        → workflows.AppGenerator.tools.export_app_code
tests/test_integration_tests_contract.py    → workflows.AppGenerator.tools.integration_tests
tests/test_platform_build_events_contract.py → workflows.AppGenerator.tools.platform.*
tests/test_update_export_pr_flow.py         → workflows.AppGenerator.tools.export_app_code
tests/test_e2b_sandbox.py                   → workflows.AppGenerator.tools.e2b_sandbox
tests/test_dynamic_import_contracts.py      → Tests env var loading (KEEP but update)
```

---

## Recommended Repository Structure

### Option 1: Monorepo with .gitignore (Simplest)

Keep everything in one repo, use `.gitignore` to exclude proprietary folders from public fork:

```
MozaiksAI/
├── .gitignore.opensource    ← Use this for public fork
├── core/                    ← Open-source
├── ChatUI/
│   ├── src/
│   │   ├── core/            ← Open-source
│   │   ├── components/      ← Open-source
│   │   ├── workflows/       ← .gitignore.opensource excludes internals
│   │   │   ├── index.js     ← Keep (API registry)
│   │   │   └── examples/    ← Add example workflow UI
│   │   └── ...
├── workflows/               ← .gitignore.opensource excludes all
│   └── _examples/           ← Add example workflow
├── tests/
│   ├── core/               ← Open-source tests
│   └── workflows/          ← .gitignore.opensource excludes
└── shared_app.py           ← Open-source
```

**.gitignore.opensource:**
```gitignore
# Proprietary workflows
workflows/AgentGenerator/
workflows/AppGenerator/
workflows/DesignDocs/
workflows/ValueEngine/
workflows/ValidationEngine/

# Proprietary UI components
ChatUI/src/workflows/AppGenerator/
ChatUI/src/workflows/AgentGenerator/
ChatUI/src/workflows/ValueEngine/

# Proprietary tests
tests/test_export_app_code_gating.py
tests/test_integration_tests_contract.py
tests/test_platform_build_events_contract.py
tests/test_update_export_pr_flow.py
tests/test_e2b_sandbox.py
```

### Option 2: Two Repos (Original Plan)

```
mozaiks-ai/                  ← PUBLIC
├── core/
├── ChatUI/src/
│   ├── core/
│   └── workflows/
│       ├── index.js
│       └── _examples/
├── workflows/
│   └── _examples/
└── shared_app.py

mozaiks-workflows/           ← PRIVATE
├── backend/
│   ├── AgentGenerator/
│   ├── AppGenerator/
│   ├── DesignDocs/
│   └── ValueEngine/
├── frontend/
│   ├── AppGenerator/
│   ├── AgentGenerator/
│   └── ValueEngine/
└── tests/
```

---

## Environment Variables for Extensibility

Your runtime supports these env var overrides for core behavior:

| Env Var | Default | Purpose |
|---------|---------|---------|
| `MOZAIKS_GENERAL_AGENT_MODULE` | `core.capabilities.simple_llm` | Non-AG2 mode |
| `MOZAIKS_WORKFLOWS_PATH` | `workflows/` | Workflow discovery path |

**Note:** `MOZAIKS_PLATFORM_BUILD_LIFECYCLE_MODULE` has been REMOVED. Lifecycle hooks are now declared per-workflow in `orchestrator.yaml` via `runtime_extensions`.

---

## Runtime Extensions — Modular Workflow Integration

Workflows can declare runtime integrations in `orchestrator.yaml`:

```yaml
runtime_extensions:
  - kind: api_router
    entrypoint: workflows.MyWorkflow.tools.api:get_router
  - kind: startup_service
    entrypoint: workflows.MyWorkflow.tools.services:MyService
  - kind: lifecycle_hooks
    entrypoint: workflows.MyWorkflow.tools.lifecycle:get_hooks
```

### Extension Kinds

| Kind | Purpose | Example Use Case |
|------|---------|------------------|
| `api_router` | Mount FastAPI routes at startup | E2B sandbox API endpoints |
| `startup_service` | Start/stop background services | Build events outbox processor |
| `lifecycle_hooks` | Workflow lifecycle notifications | Notify platform when build starts/completes/fails |

### Lifecycle Hooks Contract

The `lifecycle_hooks` entrypoint must return a dict:

```python
def get_hooks():
    return {
        "is_build_workflow": callable(workflow_name) -> bool,  # Check if this is a "build" type workflow
        "on_start": async callable(...),   # Called when workflow starts
        "on_complete": async callable(...), # Called on success
        "on_fail": async callable(...),     # Called on failure
    }
```

**For open-source users:** Don't need lifecycle hooks — they're for platform integration.
**For Mozaiks platform:** AppGenerator declares `lifecycle_hooks` to notify the .NET backend of build status.

---

## What Community Gets (Open-Source)

✅ **Full AG2-based workflow runtime**
✅ **ChatUI widget (embeddable)**
✅ **Dynamic workflow loading system**
✅ **Structured outputs framework**
✅ **Token accounting hooks**
✅ **Persistence (MongoDB)**
✅ **Event streaming (WebSocket)**
✅ **Example workflows to learn from**

❌ **NOT included:**
- Your 5 proprietary workflows
- Your proprietary UI components
- Your business-specific integrations

---

## Action Items to Open-Source

### Must Do Before Public Release

1. ~~**Fix hardcoded default in `simple_transport.py`**~~ ✅ DONE
   - Default is now empty string
   - Returns None hooks gracefully when not configured

2. **Create `workflows/_examples/` directory**
   - Add simple "Hello World" workflow
   - Add basic customer support workflow
   - Document structure in README

3. **Create `ChatUI/src/workflows/_examples/` directory**
   - Add example UI component
   - Document how to add workflow-specific UI

4. **Move proprietary tests** to private repo or exclude from public

5. **Scrub Mozaiks-specific branding** from core code comments (optional)

6. **Add LICENSE file** (MIT recommended)

7. **Update README.md** for community use

### Nice to Have

- Workflow generator CLI tool
- More pattern examples
- Video tutorials
- Discord community setup

---

## Your Development Workflow (Post-Split)

```bash
# Your setup (with proprietary workflows)
git clone mozaiks-ai          # Public runtime
cd mozaiks-ai
git clone mozaiks-workflows workflows/  # Mount private workflows

# Start with your workflows
MOZAIKS_WORKFLOWS_PATH=./workflows python run_server.py

# Community setup (without your workflows)
git clone mozaiks-ai
cd mozaiks-ai

# Start with example workflows only
python run_server.py
```

---

## Conclusion

Your architecture is already **separation-ready**. The runtime is workflow-agnostic with dynamic loading. The main work is:

1. **One code fix** — Default env var in `simple_transport.py`
2. **File organization** — Move proprietary workflows/UI to separate location
3. **Documentation** — Help community create their own workflows

You can realistically be "the first customer of mozaiks-ai" — just symlink your private workflows folder into the public runtime. No separate repos needed if you use `.gitignore`.

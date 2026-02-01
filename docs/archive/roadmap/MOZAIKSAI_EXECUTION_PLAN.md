# MozaiksAI Execution Plan
> Getting the AI Command Center Working

**Created:** 2025-01-20  
**Updated:** 2025-01-20 (Architecture Compliance Fix)
**Goal:** Natural language → Real platform actions

---

## 🔴 CRITICAL: Architecture Compliance (FIXED)

**MozaiksCore must remain APP-AGNOSTIC.** The following rules are now enforced:

### ✅ Fixed Issues:
1. **`ai_capabilities.json`** - Now empty by default (not hardcoded with Mozaiks workflows)
2. **Capability specs** - Mozaiks-specific capabilities now live in `mozaiks-platform/ai-models/capability_specs/`
3. **Plugin system** - All app-specific logic must use plugins, not core code

### How Capabilities are Loaded:
```
┌─────────────────────────────────────────────────────────────────────┐
│  mozaiks-core (GENERIC RUNTIME)                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  ai_capabilities.json = {"capabilities": []}  (EMPTY!)        │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                             +                                        │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  capability_specs/*.json (loaded from MOZAIKS_AI_...DIR)      │  │
│  └──────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  mozaiks-platform/ai-models/capability_specs/                       │
│  ├── agent-generator.json                                           │
│  ├── app-builder.json                                               │
│  ├── value-engine.json                                              │
│  ├── subscription-advisor.json                                      │
│  └── design-docs.json                                               │
└─────────────────────────────────────────────────────────────────────┘
```

### Configuration Required:
```bash
# In .env - point to platform-specific capability specs
MOZAIKS_AI_CAPABILITY_SPECS_DIR=/path/to/mozaiks-platform/ai-models/capability_specs
```

---

## 1. What We Have (Audit Summary)

### ✅ Runtime Components (mozaiks-core/runtime/ai/)
| Component | Status | Path |
|-----------|--------|------|
| FastAPI Server | ✅ Complete | `main.py`, `core/director.py` |
| JWT Auth | ✅ Complete | `security/auth.py`, `security/authentication.py` |
| WebSocket Manager | ✅ Complete | `core/websocket_manager.py` |
| Plugin System | ✅ Complete | `core/plugin_manager.py` |
| State Manager | ✅ Complete | `core/state_manager.py` |
| AI Routes | ✅ Complete | `core/routes/ai.py` |
| Runtime Manager | ✅ Complete | `core/runtime/manager.py` |

### ✅ ChatUI (mozaiks-core/runtime/ai/packages/ui/)
| Component | Status | Path |
|-----------|--------|------|
| React App | ✅ Complete | `src/` |
| Chat Interface | ✅ Complete | Uses marked, DOMPurify |
| Monaco Editor | ✅ Complete | Code display |
| Tailwind CSS | ✅ Complete | Styling |

### ✅ AI Workflows (mozaiks-platform/ai-models/workflows/)
| Workflow | Purpose |
|----------|---------|
| **AgentGenerator** | Generate multi-agent workflows via interview |
| **AppGenerator** | Build complete applications |
| **ValueEngine** | KPI analysis + recommendations |
| **LearningLoop** | Subscription plan optimization |
| **DesignDocs** | Generate design documentation |

### ⚠️ Missing Pieces
| Component | Issue | Priority |
|-----------|-------|----------|
| **Pack Loader** | Workflows exist but not loaded by runtime | 🔴 Critical |
| **OpenAI/LLM Config** | No API keys configured | 🔴 Critical |
| **Backend Tools** | Tool definitions exist in docs, not wired | 🔴 Critical |
| **ai_capabilities.json** | Capability registry not populated | 🟠 High |
| **MongoDB Connection** | Needs local/dev config | 🟠 High |

---

## 2. Architecture Understanding

```
┌─────────────────────────────────────────────────────────────────────┐
│                         USER BROWSER                                │
├─────────────────────────────────────────────────────────────────────┤
│                    ChatUI (React App)                               │
│                   http://localhost:3000                             │
└───────────────────────────┬─────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    MozaiksCore Runtime                              │
│                   http://localhost:8080                             │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                 │
│  │  /api/ai    │  │  /api/auth  │  │    /ws/     │                 │
│  │ capabilities│  │    JWT      │  │  websocket  │                 │
│  │   launch    │  │   tokens    │  │   notifs    │                 │
│  └──────┬──────┘  └─────────────┘  └─────────────┘                 │
│         │                                                           │
│         ▼                                                           │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              Pack Loader / Workflow Engine                   │   │
│  │  Loads: mozaiks-platform/ai-models/workflows/*               │   │
│  └──────┬──────────────────────────────────────────────────────┘   │
│         │                                                           │
│         ▼                                                           │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    LLM Provider (OpenAI)                     │   │
│  └─────────────────────────────────────────────────────────────┘   │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ Backend Tool Calls
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Platform Services                                │
│                   http://localhost:8010 (Gateway)                   │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐  │
│  │ Growth  │  │ Hosting │  │ Insights│  │  Auth   │  │ Billing │  │
│  │  8071   │  │  8050   │  │  8060   │  │  8020   │  │  8002   │  │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘  └─────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. Execution Plan

### Phase 1: Get Runtime Running (Today)
> Goal: ChatUI loads and connects to backend

**Step 1.1: Create dev environment file**
```bash
# mozaiks-core/runtime/ai/.env
MOZAIKS_APP_ID=dev_app
MOZAIKS_AUTH_MODE=local
SECRET_KEY=dev-secret-key-change-in-production
MONGODB_URI=mongodb://localhost:27017/mozaiks_dev
OPENAI_API_KEY=sk-your-key-here
FRONTEND_URL=http://localhost:3000
ENV=development
```

**Step 1.2: Install Python dependencies**
```bash
cd mozaiks-core/runtime/ai
pip install -r requirements.txt  # Create if missing
```

**Step 1.3: Start the backend**
```bash
cd mozaiks-core/runtime/ai
python main.py
# Should start on http://localhost:8080
```

**Step 1.4: Start ChatUI**
```bash
cd mozaiks-core/runtime/ai/packages/ui
npm install
npm start
# Should start on http://localhost:3000
```

### Phase 2: Wire Pack Loader (Day 2) - ARCHITECTURE COMPLIANT
> Goal: Workflows load and capabilities appear

**⚠️ IMPORTANT: Capability specs are NOT in mozaiks-core.**

**Step 2.1: Configure pack path**
```bash
# .env addition
MOZAIKS_PACKS_DIR=C:/path/to/mozaiks-platform/ai-models
MOZAIKS_AI_CAPABILITY_SPECS_DIR=C:/path/to/mozaiks-platform/ai-models/capability_specs
```

**Step 2.2: Capability specs live in mozaiks-platform**
```
mozaiks-platform/
└── ai-models/
    ├── workflows/               # AI workflow code
    │   ├── AgentGenerator/
    │   ├── AppGenerator/
    │   └── ...
    └── capability_specs/        # Capability UI definitions
        ├── agent-generator.json
        ├── app-builder.json
        ├── value-engine.json
        ├── subscription-advisor.json
        └── design-docs.json
```

**Step 2.3: Example capability spec (agent-generator.json)**
```json
{
  "capability": {
    "id": "agent-generator",
    "display_name": "Generate AI Agent",
    "description": "Create a multi-agent workflow through guided interview",
    "icon": "robot",
    "workflow_id": "AgentGenerator",
    "enabled": true,
    "visibility": "user",
    "allowed_plans": ["*"]
  }
}
```

**Step 2.4: Core ai_capabilities.json remains EMPTY**
```json
// mozaiks-core/runtime/ai/core/config/ai_capabilities.json
{
  "_comment": "AI capabilities are app-specific and should NOT be hardcoded here.",
  "capabilities": []
}
```

### Phase 3: Wire Backend Tools (Day 3)
> Goal: AI can call real platform APIs

**Step 3.1: Create tool executor**
```python
# mozaiks-core/runtime/ai/tools/platform_tools.py
import httpx

PLATFORM_GATEWAY = "http://localhost:8010"

async def call_platform_api(endpoint: str, method: str, data: dict, token: str):
    """Execute platform API call on behalf of AI agent."""
    async with httpx.AsyncClient() as client:
        response = await client.request(
            method=method,
            url=f"{PLATFORM_GATEWAY}{endpoint}",
            json=data,
            headers={"Authorization": f"Bearer {token}"}
        )
        return response.json()
```

**Step 3.2: Define tool schemas from docs**
Convert `GrowthEngine_MozaiksAI_Tools.md` and `ValidationEngine_MozaiksAI_Tools.md` to AG2 tool format.

### Phase 4: End-to-End Test (Day 4)
> Goal: Complete flow works

1. User logs in → gets JWT
2. ChatUI shows capabilities
3. User selects "Generate AI Agent"
4. AgentGenerator workflow runs
5. AI asks interview questions
6. User provides concept
7. Workflow generates agent code
8. (Bonus) Code deploys via Hosting.API

---

## 4. Immediate Next Actions

### YOU DO NOW:
1. **Get me your OpenAI API key** (or confirm you have one)
2. **Confirm MongoDB is running locally** (or want Docker)

### I WILL DO:
1. ✅ Create requirements.txt for Python dependencies
2. ✅ Create .env template with all needed variables
3. ✅ Create ai_capabilities.json with your workflows
4. ✅ Verify/fix pack loader path configuration
5. ✅ Test runtime startup
6. ✅ Test ChatUI connection

---

## 5. File Inventory to Create

| File | Location | Purpose |
|------|----------|---------|
| `requirements.txt` | mozaiks-core/runtime/ai/ | Python deps |
| `.env.example` | mozaiks-core/runtime/ai/ | Env template |
| `ai_capabilities.json` | mozaiks-core/runtime/ai/config/ | Capability registry |
| `platform_tools.py` | mozaiks-core/runtime/ai/tools/ | Backend API caller |

---

## 6. Success Criteria

### Phase 1 Complete When:
- [ ] `python main.py` starts without errors
- [ ] http://localhost:8080/health returns 200
- [ ] ChatUI loads at http://localhost:3000
- [ ] ChatUI connects to backend (no WebSocket errors)

### Phase 2 Complete When:
- [ ] GET /api/ai/capabilities returns workflow list
- [ ] User can click a capability to launch
- [ ] Chat session starts with initial agent message

### Phase 3 Complete When:
- [ ] AI can call at least one platform API
- [ ] Tool execution appears in chat
- [ ] Real data flows between AI and backend

### Phase 4 Complete When:
- [ ] Complete workflow runs end-to-end
- [ ] User gets tangible output (generated code, insights, etc.)

---

## Questions Before We Start

1. **OpenAI API Key**: Do you have one? Want to use a different LLM?
2. **MongoDB**: Running locally? Want Docker? Atlas?
3. **Platform Services**: Should I also spin up the .NET services, or focus on AI first?
4. **Auth Mode**: Local dev auth (simple) or wire up to AuthServer?

Let me know and I'll start Phase 1 immediately.

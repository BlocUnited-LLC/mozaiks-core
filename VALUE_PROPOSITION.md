# 🚀 mozaiks-core Value Proposition

<div align="center">

![Mozaiks Core](https://img.shields.io/badge/mozaiks--core-Open%20Source%20AI%20Runtime-blueviolet?style=for-the-badge)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=for-the-badge)](LICENSE)
[![Built on AG2](https://img.shields.io/badge/Built%20on-AG2%20(AutoGen)-blue?style=for-the-badge)](https://github.com/ag2ai/ag2)

**The Missing Layer Between AI Agents and Real Users**

*Where multi-agent workflows meet production-grade UX*

</div>

---

## 📊 The State of Agentic AI in 2026

```
┌─────────────────────────────────────────────────────────────────┐
│                   MARKET LANDSCAPE                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   LangChain ────────► 125K ⭐  │  Foundation, but complex       │
│   AutoGen (AG2) ────► 54K ⭐   │  Powerful, but raw             │
│   CrewAI ───────────► 43K ⭐   │  Simple, but limited UI        │
│   Swarm ────────────► 21K ⭐   │  Educational, not production   │
│   Letta (MemGPT) ───► 21K ⭐   │  Memory-focused, narrow scope  │
│                                                                  │
│   mozaiks-core ─────► 🆕       │  UI-Native + Production-Ready  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 😤 The Pain Points We Solve

### 🔴 Pain Point #1: **"Agents Talk, But Users Can't Interact"**

> *"My CrewAI agents do great work behind the scenes, but presenting results to users? I'm basically building a chat interface from scratch."*

**The Reality Today:**
- Most frameworks focus on agent-to-agent communication
- User-facing output is an afterthought (plain text streams)
- This isnt just your average chatbot, building interactive UIs on top requires significant custom work
- No standard way for agents to request user input mid-workflow

**❌ Other Frameworks:**
```python
# CrewAI / LangChain / AutoGen
result = crew.kickoff()  
print(result)  # Hope the user understands this wall of text 🤷
```

**✅ mozaiks-core:**
```python
# Agent emits a rich, interactive UI component
await use_ui_tool(
    tool_id="code_editor",
    payload={"language": "python", "code": generated_code, "editable": True},
    display="artifact"  # Full-screen interactive experience
)
# User edits code → Agent receives structured response → Continues workflow
```

---

### 🔴 Pain Point #2: **"Workflows Die When Users Close the Tab"**

> *"User left mid-conversation. Two hours of agent work? Gone. Context? Lost. They have to start over."*

**The Reality Today:**
- Most frameworks are stateless by design (Swarm explicitly!)
- LangChain requires manual memory configuration
- Persistence is DIY: save to Redis, Postgres, whatever
- Resume means "hope you saved the right state"

**❌ Other Frameworks:**
```
User closes browser during 20-minute research workflow
    ↓
[Session Lost] 💀
    ↓  
User returns: "Let's continue"
    ↓
Agent: "I have no memory of this conversation"
```

**✅ mozaiks-core:**
```
User closes browser during 20-minute research workflow
    ↓
[Auto-persisted to MongoDB: messages, context, artifacts]
    ↓  
User returns: "Let's continue"
    ↓
Runtime: Restores full state + re-renders last interactive artifact
    ↓
Agent: "Welcome back! Here's where we left off..." 🎯
```

---

### 🔴 Pain Point #3: **"Token Tracking is an Afterthought"**

> *"My agents burned through $200 in API calls last night. I found out this morning when my OpenAI bill came."*

**The Reality Today:**
- Frameworks focus on capability, not cost
- Usage tracking requires third-party tools (LangSmith, etc.)
- No way to budget or limit per-user/per-workflow
- Billing integration is completely DIY

**❌ Other Frameworks:**
```python
# Pray you remembered to set up monitoring
agent.run(task)  
# Surprise $500 bill incoming 💸
```

**✅ mozaiks-core:**
```python
# Built-in real-time token tracking
{
    "event": "chat.usage_delta",
    "data": {
        "agent": "research_agent",
        "model": "gpt-4",
        "prompt_tokens": 1500,
        "completion_tokens": 800,
        "cost_usd": 0.08
    }
}
# Aggregated, per-agent, per-model, streamed in real-time
```

---

### 🔴 Pain Point #4: **"Multi-Tenant? That's a 'You' Problem"**

> *"Great, I have a multi-agent system. Now I need to deploy it for 1000 customers without them seeing each other's data."*

**The Reality Today:**
- Frameworks assume single-tenant local execution
- App isolation requires custom infrastructure
- User scoping is manual (`user_id` everywhere)
- No built-in concept of "apps" or "namespaces"

**❌ Other Frameworks:**
```python
# You're on your own for isolation
crew = Crew(agents=[...])
# Manually ensure user_a doesn't see user_b's data 🙏
```

**✅ mozaiks-core:**
```python
# First-class multi-tenant execution
{
    "app_id": "acme_corp",         # Namespace isolation
    "user_id": "user_alice_456",   # User scoping
    "chat_id": "chat_abc123",      # Session tracking
    "workflow_name": "Generator"   # Workflow context
}
# Every operation scoped. Every query filtered. Built-in.
```

---

## 🎯 What mozaiks-core Actually Is

```
┌──────────────────────────────────────────────────────────────────────┐
│                       mozaiks-core                                    │
│                 "The Production Runtime Layer"                        │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│   ┌─────────────┐   ┌─────────────┐   ┌─────────────────────────┐  │
│   │  AG2 Engine │ + │  UI Tools   │ + │  Persistence & Resume   │  │
│   │  (AutoGen)  │   │  System     │   │  (MongoDB)              │  │
│   └─────────────┘   └─────────────┘   └─────────────────────────┘  │
│          │                 │                      │                  │
│          └─────────────────┴──────────────────────┘                  │
│                             │                                         │
│                    ┌────────┴────────┐                               │
│                    │ Unified Runtime │                               │
│                    │   with Events   │                               │
│                    └────────┬────────┘                               │
│                             │                                         │
│   ┌─────────────┐   ┌──────┴──────┐   ┌─────────────────────────┐  │
│   │  WebSocket  │   │  Real-time  │   │   Multi-tenant          │  │
│   │  Transport  │   │  Token      │   │   Isolation             │  │
│   │  Streaming  │   │  Tracking   │   │   (app_id/user_id)      │  │
│   └─────────────┘   └─────────────┘   └─────────────────────────┘  │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 🌟 Our Killer Features

### 1️⃣ **Dual-Mode Intelligence: Ask + Workflow** 🧠

Seamlessly switch between free-form AI chat and structured multi-agent workflows—**without losing context**.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    DUAL-MODE ARCHITECTURE                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌─────────────────────────┐       ┌─────────────────────────┐        │
│   │     🧠 ASK MODE         │◄─────►│    🤖 WORKFLOW MODE      │        │
│   │                         │ switch │                         │        │
│   │  • General questions    │       │  • Multi-agent tasks     │        │
│   │  • Quick answers        │       │  • Structured handoffs   │        │
│   │  • Platform help        │       │  • Interactive artifacts │        │
│   │  • Context-aware tips   │       │  • Human-in-the-loop     │        │
│   └─────────────────────────┘       └─────────────────────────┘        │
│              │                                    │                      │
│              └────────────┬───────────────────────┘                      │
│                           │                                              │
│                    ┌──────┴──────┐                                       │
│                    │   SHARED    │                                       │
│                    │   STATE     │                                       │
│                    ├─────────────┤                                       │
│                    │ • Messages  │  ← Each mode has its own cache       │
│                    │ • Context   │  ← Switching preserves both          │
│                    │ • Artifacts │  ← Resume either anytime             │
│                    └─────────────┘                                       │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

**❌ Other Frameworks:** One mode. Chat OR workflow. Pick one.

**✅ mozaiks-core:**
```javascript
// User building an app (workflow mode)
"Generate a landing page for my SaaS"
  → Multi-agent workflow runs (Planner → Designer → Coder)

// User has a quick question (switch to ask mode)  
🧠 Toggle → "What's the best color palette for fintech?"
  → Instant answer, workflow paused but preserved

// User returns to workflow
🤖 Toggle → "Continue where I left off"
  → Workflow resumes with full context + artifacts
```

---

### 2️⃣ **Persistent Chat Widget** 🔮

Your AI assistant follows you across pages—always accessible, always contextual.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      PERSISTENT WIDGET PATTERN                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   PAGE: /analytics                PAGE: /settings              PAGE: /chat│
│   ┌──────────────────┐           ┌──────────────────┐    ┌──────────────┐│
│   │                  │           │                  │    │              ││
│   │   Analytics      │           │   Settings       │    │  Full Chat   ││
│   │   Dashboard      │           │   Page           │    │  Interface   ││
│   │                  │           │                  │    │              ││
│   │          ┌────┐  │           │          ┌────┐  │    │              ││
│   │          │ 🧠 │  │           │          │ 🧠 │  │    │              ││
│   │          └────┘  │           │          └────┘  │    │              ││
│   └──────────────────┘           └──────────────────┘    └──────────────┘│
│          │                              │                       │        │
│          └──────────────────────────────┴───────────────────────┘        │
│                                    │                                      │
│                          SAME WebSocket connection                       │
│                          SAME conversation state                         │
│                          SAME workflow progress                          │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

**Why This Matters:**
- User starts workflow on `/chat` → browses to `/analytics` → widget shows progress
- Click 🧠 on ANY page → return to active workflow instantly
- No page refresh, no lost state, no confusion

```javascript
// Adding widget support to ANY page = 1 line
import { useWidgetMode } from '../hooks/useWidgetMode';

function AnalyticsPage() {
  useWidgetMode();  // ← That's it. Widget appears.
  return <Dashboard />;
}
```

---

### 3️⃣ **Context-Aware Assistance** 🎯

The AI knows WHERE you are and WHAT you're looking at.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     LAYERED CONTEXT AWARENESS                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │ PLATFORM LAYER (Global)                                            │  │
│  │ • Core AI capabilities    • General knowledge    • Platform docs   │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                    │                                     │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │ APP LAYER (Per-tenant)                                             │  │
│  │ • App-specific workflows  • Custom tools        • Domain knowledge │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                    │                                     │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │ PAGE LAYER (Current screen)                                        │  │
│  │ • Contextual suggestions  • Quick actions       • Relevant help    │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘

EXAMPLE:
  User on /analytics page, clicks 🧠 widget:

  ┌─────────────────────────────────────────────────────────┐
  │ 🧠 I see you're viewing Analytics. Would you like me to:│
  │                                                          │
  │  📊 Generate a custom report from this data              │
  │  📥 Export these metrics to CSV                          │
  │  💡 Explain what these numbers mean                      │
  │  🔧 Create an automated alert for this metric            │
  │                                                          │
  │  ────────────────────────────────────── [Ask anything]   │
  └─────────────────────────────────────────────────────────┘
```

**No other framework does this.**

---

### 4️⃣ **Interactive Artifacts System** 💎

The ability for AI agents to render rich, interactive UI components that users can manipulate—**mid-workflow**.

```
┌────────────────────────────────────────────────────────────┐
│          INTERACTIVE ARTIFACTS ARCHITECTURE                 │
├────────────────────────────────────────────────────────────┤
│                                                             │
│  Agent Decision                                             │
│       │                                                     │
│       ▼                                                     │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  use_ui_tool("code_editor", {code: "...", ...})     │  │
│  └─────────────────────┬───────────────────────────────┘  │
│                        │                                    │
│                        ▼                                    │
│  ┌─────────────────────────────────────────────────────┐  │
│  │           WebSocket Event Stream                     │  │
│  │  {                                                   │  │
│  │    "event": "ui.tool",                              │  │
│  │    "tool_name": "code_editor",                      │  │
│  │    "display": "artifact",     ←── Full-screen UI    │  │
│  │    "payload": {...}                                 │  │
│  │  }                                                   │  │
│  └─────────────────────┬───────────────────────────────┘  │
│                        │                                    │
│                        ▼                                    │
│  ┌─────────────────────────────────────────────────────┐  │
│  │           Frontend Renders Component                 │  │
│  │  ┌───────────────────────────────────────────────┐  │  │
│  │  │  ████████████████████████████████████████████ │  │  │
│  │  │  █                                          █ │  │  │
│  │  │  █   Interactive Code Editor                █ │  │  │
│  │  │  █   with syntax highlighting,              █ │  │  │
│  │  │  █   live editing, submit button            █ │  │  │
│  │  │  █                                          █ │  │  │
│  │  │  ████████████████████████████████████████████ │  │  │
│  │  └───────────────────────────────────────────────┘  │  │
│  └─────────────────────┬───────────────────────────────┘  │
│                        │                                    │
│                        ▼                                    │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  User edits → Submits → Agent receives response     │  │
│  │  Workflow CONTINUES with user's modifications       │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
└────────────────────────────────────────────────────────────┘
```

**Example UI Tools:**
- `code_editor` - Editable code with syntax highlighting
- `form_builder` - Dynamic forms with validation
- `approval_dialog` - Human-in-the-loop checkpoints
- `chart_viewer` - Interactive data visualizations
- `file_browser` - Browse and select files
- `Custom components` - Build your own with React

**🆕 Artifacts Can Trigger Workflows!**

Unlike read-only displays, mozaiks artifacts are **truly interactive**:

```
┌────────────────────────────────────────────────────────────┐
│  ARTIFACT: Action Plan Viewer                               │
│  ┌────────────────────────────────────────────────────────┐│
│  │ ✅ Phase 1: Design System         [Complete]           ││
│  │ ⏳ Phase 2: Database Setup        [In Progress]        ││
│  │    └─► Database Status: ⚠️ Not Connected               ││
│  │        ┌──────────────────────────────────────┐        ││
│  │        │ 🔧 Configure Database                │ ◄──────││──── USER CLICKS
│  │        └──────────────────────────────────────┘        ││
│  │ ⬚ Phase 3: API Development        [Pending]            ││
│  └────────────────────────────────────────────────────────┘│
└────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌────────────────────────────────────────────────────────────┐
│  AGENT: DatabaseConfigAgent activates                       │
│  ┌────────────────────────────────────────────────────────┐│
│  │  Configure MongoDB Connection                          ││
│  │  ┌──────────────────────────────────────────────────┐  ││
│  │  │ Connection String: [mongodb://...            ]   │  ││
│  │  │ Database Name:     [myapp_production         ]   │  ││
│  │  │ [Test Connection] [Save Configuration]           │  ││
│  │  └──────────────────────────────────────────────────┘  ││
│  └────────────────────────────────────────────────────────┘│
└────────────────────────────────────────────────────────────┘
                            │
                            ▼
        Artifact auto-updates: "✅ Database Connected"
```

---

### 5️⃣ **UI Completion State Persistence** 🔄

Inline components (approval buttons, forms, inputs) remember their state across reconnections.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                 INLINE COMPONENT PERSISTENCE                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  BEFORE (Other Frameworks):                                              │
│  ┌────────────────────────────────────┐                                 │
│  │ Agent: "Do you approve this plan?" │                                 │
│  │ [Approve] [Reject]                 │ ← User clicks Approve           │
│  └────────────────────────────────────┘                                 │
│              ↓                                                           │
│  [User disconnects and reconnects]                                       │
│              ↓                                                           │
│  ┌────────────────────────────────────┐                                 │
│  │ Agent: "Do you approve this plan?" │                                 │
│  │ [Approve] [Reject]                 │ ← Same buttons appear again! 😠 │
│  └────────────────────────────────────┘                                 │
│                                                                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  AFTER (mozaiks-core):                                                   │
│  ┌────────────────────────────────────┐                                 │
│  │ Agent: "Do you approve this plan?" │                                 │
│  │ [Approve] [Reject]                 │ ← User clicks Approve           │
│  └────────────────────────────────────┘                                 │
│              ↓                                                           │
│  [User disconnects and reconnects]                                       │
│              ↓                                                           │
│  ┌────────────────────────────────────┐                                 │
│  │ Agent: "Do you approve this plan?" │                                 │
│  │ ✅ Approved                        │ ← Shows completed state! 🎉     │
│  └────────────────────────────────────┘                                 │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

**How It Works:**
```javascript
// MongoDB stores UI tool completion state
{
  "messages": [{
    "role": "assistant",
    "content": "Do you approve this plan?",
    "metadata": {
      "ui_tool": {
        "ui_tool_id": "approval_form",
        "event_id": "evt_123",
        "ui_tool_completed": true,        // ← Persisted!
        "completed_at": "2026-01-23T...",
        "response_summary": "approved"
      }
    }
  }]
}
// On reconnect: Frontend sees completed=true → Shows "✅ Approved" chip
```

---

### 6️⃣ **Full Session Persistence & Resume** 💾

Stop losing work. Every message, every context variable, every artifact state—automatically persisted.

```javascript
// What gets persisted (automatically!)
{
  "_id": "chat_abc123def456",
  "app_id": "acme_corp",
  "workflow_name": "CodeGenerator",
  "status": "PAUSED",
  
  // Full conversation history
  "messages": [
    {"role": "user", "content": "Build a task app", "sequence": 0},
    {"role": "assistant", "name": "architect", "content": "...", "sequence": 1},
    // ... all messages preserved
  ],
  
  // Context variables for workflow state
  "context_snapshot": {
    "interview_complete": true,
    "tech_stack": "Next.js + Supabase",
    "features_confirmed": ["auth", "tasks", "teams"]
  },
  
  // Last interactive artifact (for re-rendering on resume)
  "last_artifact": {
    "ui_tool_id": "code_editor",
    "display": "artifact",
    "payload": { "code": "...", "language": "typescript" }
  }
}

// Resume flow:
// 1. User reconnects
// 2. Runtime loads session
// 3. Messages replayed to agents
// 4. Last artifact re-rendered
// 5. User sees EXACTLY where they left off
```

---

### 7️⃣ **Real-Time Token & Cost Tracking** 📊

Know what you're spending. In real-time. Per agent. Per model. Per workflow.

```
┌─────────────────────────────────────────────────────────────┐
│                TOKEN TRACKING PIPELINE                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  AG2 Agent LLM Call                                         │
│       │                                                      │
│       ▼ [tokens consumed]                                   │
│  RealtimeTokenLogger                                        │
│       │                                                      │
│       ├──► WebSocket: chat.usage_delta (real-time stream)   │
│       │    {                                                 │
│       │      "agent": "planner",                            │
│       │      "model": "gpt-4",                              │
│       │      "prompt_tokens": 2500,                         │
│       │      "completion_tokens": 800,                      │
│       │      "cost_usd": 0.11                               │
│       │    }                                                 │
│       │                                                      │
│       ├──► MongoDB: WorkflowStats (aggregated)              │
│       │    - Per-chat totals                                │
│       │    - Per-agent breakdown                            │
│       │    - Per-model usage                                │
│       │                                                      │
│       └──► chat.usage_summary (on workflow complete)        │
│            {                                                 │
│              "total_prompt_tokens": 45000,                  │
│              "total_completion_tokens": 12000,              │
│              "total_cost_usd": 1.87,                        │
│              "duration_sec": 127.4,                         │
│              "by_agent": {...},                             │
│              "by_model": {...}                              │
│            }                                                 │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

**What You Can Build:**
- Real-time cost dashboards for users
- Budget limits per user/workflow
- Usage analytics and optimization
- Billing integrations

---

### 8️⃣ **Production-Ready Architecture** 🏗️

Not a toy. Not educational. Built for real deployments.

| Feature | LangChain | AutoGen | CrewAI | Swarm | **mozaiks-core** |
|---------|-----------|---------|--------|-------|------------------|
| Multi-tenant isolation | ❌ DIY | ❌ DIY | ❌ DIY | ❌ N/A | ✅ **Built-in** |
| Session persistence | ⚠️ Plugin | ❌ DIY | ❌ DIY | ❌ Stateless | ✅ **Automatic** |
| Interactive UI tools | ❌ N/A | ❌ N/A | ❌ N/A | ❌ N/A | ✅ **Native** |
| Resume workflows | ⚠️ Manual | ❌ DIY | ❌ DIY | ❌ N/A | ✅ **Automatic** |
| Real-time streaming | ✅ Yes | ✅ Yes | ⚠️ Limited | ✅ Yes | ✅ **WebSocket** |
| Token tracking | ⚠️ LangSmith | ❌ Manual | ❌ Manual | ❌ Manual | ✅ **Built-in** |
| Dual-mode (Ask+Workflow) | ❌ N/A | ❌ N/A | ❌ N/A | ❌ N/A | ✅ **Native** |
| Persistent widget | ❌ N/A | ❌ N/A | ❌ N/A | ❌ N/A | ✅ **Built-in** |
| Context-aware UI | ❌ N/A | ❌ N/A | ❌ N/A | ❌ N/A | ✅ **3-layer** |
| UI state persistence | ❌ N/A | ❌ N/A | ❌ N/A | ❌ N/A | ✅ **Automatic** |
| Self-hostable | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ **Yes** |

---

## 🔥 The "Only mozaiks-core Can Do This" Section

These features **don't exist** in any other open-source agentic framework:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    🏆 EXCLUSIVE CAPABILITIES                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1️⃣  ARTIFACT-TRIGGERED WORKFLOWS                                       │
│      User clicks button IN artifact → Agent activates → UI updates      │
│      (Not just read-only displays!)                                      │
│                                                                          │
│  2️⃣  DUAL-MODE CONTEXT SWITCHING                                        │
│      Ask ↔ Workflow with separate caches, instant switching             │
│      (No other framework has this concept)                              │
│                                                                          │
│  3️⃣  PAGE-AWARE FLOATING ASSISTANT                                      │
│      Widget follows user, knows current page context                    │
│      (Built into the shell, works on any page)                          │
│                                                                          │
│  4️⃣  UI COMPLETION STATE SURVIVAL                                       │
│      Inline buttons/forms remember they were clicked after reconnect    │
│      (Metadata persisted in MongoDB with each message)                  │
│                                                                          │
│  5️⃣  ARTIFACT PANEL SNAPSHOT & RESTORE                                  │
│      Switch modes → panel state preserved → switch back → restored      │
│      (Full layout, open/closed state, content)                          │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🛠️ Getting Started (Self-Hosting)

### Developer Experience: Built-In Design System

Every UI component you create automatically gets:

```javascript
import { typography, components, spacing, layouts } from '../styles/artifactDesignSystem';

// Pre-built tokens for consistent, beautiful UIs
const MyComponent = () => (
  <div className={layouts.artifactContainer}>
    <h1 className={typography.display}>Dashboard</h1>
    <button className={components.button.primary}>Take Action</button>
    <span className={components.badge.success}>Active</span>
  </div>
);
```

| Token Category | What You Get |
|----------------|--------------|
| `typography.*` | Orbitron headings, Rajdhani body, preset sizes |
| `components.button.*` | Primary (cyan), secondary (slate), ghost |
| `components.badge.*` | Success, warning, neutral status indicators |
| `components.card.*` | Primary, secondary, ghost card surfaces |
| `spacing.*` | Section, subsection, padding, gap primitives |
| `layouts.*` | Container, grid, flex patterns |

**No more hand-crafting Tailwind strings.** Consistent UI out of the box.

---

### Prerequisites
- Python 3.10+
- MongoDB (local or Atlas)
- Node.js 18+ (for frontend)

### Minimal Setup

```bash
# 1. Clone the repo
git clone https://github.com/your-org/mozaiks-core.git
cd mozaiks-core

# 2. Configure environment
cp .env.example .env
# Edit .env:
#   MONGO_URI=mongodb://localhost:27017
#   OPENAI_API_KEY=sk-...

# 3. Install & run
pip install -r requirements.txt
python -m runtime.ai.main

# 4. Connect your frontend
# WebSocket: ws://localhost:8000/ws/chat/{app_id}/{chat_id}/{user_id}
```

### That's It!

**🗄️ Database Connection = 1 Environment Variable:**
```bash
# That's literally it. Set this ONE variable:
MONGO_URI=mongodb://localhost:27017
# or MongoDB Atlas:
MONGO_URI=mongodb+srv://user:pass@cluster.mongodb.net/myapp
```

**What happens automatically:**
- ✅ Collections auto-created (`ChatSessions`, `WorkflowStats`, `Wallets`)
- ✅ Indexes auto-created for optimal query performance
- ✅ Sessions persist with zero additional config
- ✅ Token tracking works out of the box
- ✅ UI tool state survives reconnections
- ✅ Resume/replay fully functional

**No migrations. No schemas to define. No ORM setup.**
---

## 📈 Comparison Summary

| | mozaiks-core | LangChain | AutoGen | CrewAI |
|--|--------------|-----------|---------|--------|
| **Focus** | Production UX | LLM Abstraction | Multi-Agent | Simple Crews |
| **UI Integration** | 🥇 Native | ❌ None | ❌ None | ❌ None |
| **Persistence** | 🥇 Automatic | ⚠️ Manual | ❌ DIY | ❌ DIY |
| **Resume** | 🥇 Built-in | ❌ DIY | ❌ DIY | ❌ DIY |
| **Multi-tenant** | 🥇 Native | ❌ DIY | ❌ DIY | ❌ DIY |
| **Token Tracking** | 🥇 Real-time | ⚠️ LangSmith | ❌ Manual | ❌ Manual |
| **Dual-Mode Chat** | 🥇 Native | ❌ None | ❌ None | ❌ None |
| **Floating Widget** | 🥇 Built-in | ❌ None | ❌ None | ❌ None |
| **Context Awareness** | 🥇 3-Layer | ❌ None | ❌ None | ❌ None |
| **UI State Persist** | 🥇 Automatic | ❌ None | ❌ None | ❌ None |
| **Self-Hostable** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| **License** | MIT | MIT | MIT/CC | MIT |

---

## 💡 Real-World Scenario: Fantasy Football App

Here's how mozaiks-core enables experiences **impossible** with other frameworks:

```
┌─────────────────────────────────────────────────────────────────────────┐
│  USER: "Build me a fantasy football draft assistant"                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  🤖 WORKFLOW MODE ACTIVATES                                              │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │ ARTIFACT PANEL: Draft Board                                        │  │
│  │ ┌─────────────────────────────────────────────────────────────────┐│  │
│  │ │  MY TEAM              │  AVAILABLE PLAYERS                      ││  │
│  │ │  ─────────────        │  ──────────────────                     ││  │
│  │ │  QB: [Empty]          │  🏈 Patrick Mahomes  [Draft]            ││  │
│  │ │  RB: [Empty]          │  🏈 Josh Allen       [Draft]  ◄───────────── │
│  │ │  WR: [Empty]          │  🏈 Lamar Jackson    [Draft]  USER CLICKS  │
│  │ │                       │                                         ││  │
│  │ │  [Ask AI for advice]  │  🔍 Filter  📊 Stats                   ││  │
│  │ └─────────────────────────────────────────────────────────────────┘│  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                            │                                             │
│                            ▼                                             │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │ AGENT: DraftAdvisorAgent                                           │  │
│  │ "Mahomes is a solid pick! With your 3rd overall pick, I'd          │  │
│  │ recommend taking him. His consistency and high floor make          │  │
│  │ him valuable in any format. Want me to project your next picks?"   │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                            │                                             │
│  USER SWITCHES TO 🧠 ASK MODE (mid-draft!)                              │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │ User: "What's Mahomes injury history?"                             │  │
│  │ AI: "Patrick Mahomes has been remarkably durable..."               │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                            │                                             │
│  USER RETURNS TO 🤖 WORKFLOW MODE                                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │ ✅ Draft board restored exactly as left                            │  │
│  │ ✅ Conversation context preserved                                  │  │
│  │ ✅ User continues drafting seamlessly                              │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

**Try this with CrewAI, AutoGen, or LangChain.** You can't.

---

## 🚀 Ready to Build Production AI Experiences?

```bash
git clone https://github.com/your-org/mozaiks-core.git
```

**Resources:**
- 📚 [Documentation](./docs/)
- 💬 [Discord Community](#) (coming soon)
- 🐛 [Issue Tracker](../../issues)
- 📝 [Contributing Guide](./CONTRIBUTING.md)

---

<div align="center">

**mozaiks-core** is open source software licensed under the MIT License.

Built with ❤️ by developers who were tired of rebuilding the same infrastructure.

*Because AI agents deserve better than `print(result)`*

</div>

# AskMozaiks Widget Architecture

> **Document Purpose**: Define whether AskMozaiks should exist, how it differs from the main chat, and how it should be architected across the Mozaiks platform.
>
> **Last Updated**: January 17, 2026

---

## Table of Contents

1. [Conceptual Role](#1-conceptual-role)
2. [Relationship to agent_console](#2-relationship-to-agent_console)
3. [Ownership & Repository Boundaries](#3-ownership--repository-boundaries)
4. [Integration with mozaiks-core UI](#4-integration-with-mozaiks-core-ui)
5. [Context-Aware Behavior](#5-context-aware-behavior)
6. [Runtime Flow](#6-runtime-flow)
7. [Data & Telemetry](#7-data--telemetry)
8. [Folder Structure](#8-folder-structure)
9. [Concrete Recommendation](#9-concrete-recommendation)

---

## 1. Conceptual Role

### 1.1 What Is AskMozaiks?

**AskMozaiks** is a **context-aware floating assistant widget** that lives inside any Mozaiks-powered app, providing:

1. **Instant AI assistance** without leaving the current screen
2. **Contextual workflow suggestions** based on what the user is viewing
3. **Quick actions** for common operations relevant to the current page
4. **Seamless transition** to full workflow execution when needed

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     AskMozaiks Conceptual Model                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ANY MOZAIKS-POWERED APP                                                │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                                                                  │   │
│   │    [Current Page Content]                                        │   │
│   │                                                                  │   │
│   │                                                                  │   │
│   │                                                                  │   │
│   │                                                        ┌─────┐  │   │
│   │                                                        │ 🧠  │  │   │
│   │                                                        └──┬──┘  │   │
│   │                                                           │     │   │
│   └───────────────────────────────────────────────────────────┼─────┘   │
│                                                               │         │
│                          ┌────────────────────────────────────┘         │
│                          │ Click / Trigger                              │
│                          ▼                                              │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                   AskMozaiks Widget Panel                        │   │
│   │  ┌───────────────────────────────────────────────────────────┐  │   │
│   │  │ 🧠 AskMozaiks                              [🤖] [↗️]      │  │   │
│   │  ├───────────────────────────────────────────────────────────┤  │   │
│   │  │                                                           │  │   │
│   │  │  "I see you're on the Analytics page.                     │  │   │
│   │  │   Would you like me to:                                   │  │   │
│   │  │   • Generate a new report                                 │  │   │
│   │  │   • Export this data                                      │  │   │
│   │  │   • Explain these metrics"                                │  │   │
│   │  │                                                           │  │   │
│   │  │  [────────────────────────────────────] [Send]            │  │   │
│   │  └───────────────────────────────────────────────────────────┘  │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Is It Global, App-Specific, or Both?

**Answer: BOTH — with layered behavior**

| Layer | Scope | Behavior |
|-------|-------|----------|
| **Platform Layer** | Global across all Mozaiks apps | Core AI assistant, general questions, platform help |
| **App Layer** | Per-app configuration | App-specific workflows, domain knowledge, custom tools |
| **Page Layer** | Current screen context | Contextual suggestions, relevant quick actions |

```yaml
# The widget has three "awareness" layers
awareness_layers:
  platform:
    # Always available regardless of app
    capabilities:
      - answer_general_questions
      - explain_mozaiks_concepts
      - navigate_between_apps
      - access_documentation
    
  app:
    # Loaded based on app_id
    capabilities:
      - run_app_specific_workflows
      - access_app_data
      - invoke_app_tools
    configuration:
      source: "app_manifest.json"
      
  page:
    # Determined by current route + screen state
    capabilities:
      - suggest_relevant_actions
      - pre_fill_context_from_screen
      - highlight_related_data
    context_provider: "usePageContext()"
```

---

## 2. Relationship to agent_console

### 2.1 Terminology Clarification

There is no existing `agent_console` in the current codebase. Based on context, you may be referring to:

| Term | What It Is | Current Implementation |
|------|------------|----------------------|
| **ChatPage** | Full-screen chat interface at `/chat` | `ChatUI/src/pages/ChatPage.js` |
| **PersistentChatWidget** | Minimized floating widget | `ChatUI/src/components/chat/PersistentChatWidget.js` |
| **AskMozaiks** | Proposed context-aware assistant | **This document defines it** |

### 2.2 AskMozaiks vs ChatPage (Full Chat)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                  SURFACE COMPARISON                                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌───────────────────────────────┐  ┌───────────────────────────────┐   │
│  │        AskMozaiks             │  │         ChatPage              │   │
│  │        (Widget)               │  │         (Full Screen)         │   │
│  ├───────────────────────────────┤  ├───────────────────────────────┤   │
│  │ • Floating overlay            │  │ • Dedicated route (/chat)     │   │
│  │ • Quick questions             │  │ • Complex workflows           │   │
│  │ • Context-aware suggestions   │  │ • Artifact panels             │   │
│  │ • Minimal UI footprint        │  │ • Full message history        │   │
│  │ • Single-turn or short conv   │  │ • Multi-agent orchestration   │   │
│  │ • Available on ANY page       │  │ • Deep interaction            │   │
│  └───────────────────────────────┘  └───────────────────────────────┘   │
│                    │                              ▲                      │
│                    │    "Expand to full chat"     │                      │
│                    └──────────────────────────────┘                      │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.3 When to Use Which

| User Intent | Surface | Rationale |
|-------------|---------|-----------|
| "What does this metric mean?" | **AskMozaiks** | Quick answer, stays on page |
| "Generate a new app" | **ChatPage** | Complex workflow, needs artifacts |
| "Export this data" | **AskMozaiks** | Context-aware action, one click |
| "Debug my deployment" | **ChatPage** | Multi-turn, may need logs/tools |
| "Suggest improvements" | **AskMozaiks → ChatPage** | Starts quick, may escalate |

### 2.4 Should They Coexist or Merge?

**Decision: COEXIST with shared state**

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     UNIFIED CHAT ARCHITECTURE                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    ChatUIContext (Shared State)                  │    │
│  │  • conversationMode: 'ask' | 'workflow'                         │    │
│  │  • activeChatId, activeWorkflowName                             │    │
│  │  • messages cache (per mode)                                    │    │
│  │  • pageContext (from current screen)                            │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                    │                           │                         │
│          ┌────────┴─────────┐       ┌─────────┴─────────┐               │
│          ▼                  │       ▼                   │               │
│  ┌───────────────┐         │   ┌───────────────┐       │               │
│  │  AskMozaiks   │◄────────┼───│   ChatPage    │───────┤               │
│  │   Widget      │  sync   │   │  (Full Chat)  │       │               │
│  └───────────────┘         │   └───────────────┘       │               │
│          │                 │           │               │               │
│          └─────────────────┴───────────┴───────────────┘               │
│                            │                                            │
│                    Same WebSocket connection                            │
│                    Same backend session                                 │
│                    Seamless context handoff                             │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

**Key Integration Points:**
1. **Single WebSocket**: Both surfaces share the same connection
2. **Session continuity**: Conversation persists when switching surfaces
3. **Context handoff**: Page context from widget carries into full chat
4. **Mode preservation**: `conversationMode` determines behavior, not surface

---

## 3. Ownership & Repository Boundaries

### 3.1 Component Ownership Matrix

| Component | Owner Repo | Rationale |
|-----------|------------|-----------|
| **Widget UI** (button + panel) | `mozaiks-core` | Part of the generated app shell |
| **Widget Hook** (`useWidgetMode`) | `mozaiks-ai/ChatUI` | Runtime behavior lives in ChatUI |
| **Chat Components** | `mozaiks-ai/ChatUI` | Shared chat primitives |
| **Backend Orchestration** | `mozaiks-ai` | AG2 runtime, workflow execution |
| **Agent Execution** | `mozaiks-ai` | AG2 agents run in runtime |
| **Workflow Routing** | `mozaiks-ai` | Workflow resolution logic |
| **Context Provider** | `mozaiks-ai/ChatUI` | Page context collection |
| **Widget SDK** | `mozaiks-core` | Embedding API for generated apps |

### 3.2 Detailed Breakdown

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     REPOSITORY BOUNDARIES                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  MOZAIKS-CORE (Generated App Templates)                                  │
│  ├── packages/                                                           │
│  │   └── widget-sdk/                    # NPM package for embedding      │
│  │       ├── src/                                                        │
│  │       │   ├── AskMozaiksButton.tsx   # Floating button component      │
│  │       │   ├── AskMozaiksPanel.tsx    # Expandable panel shell         │
│  │       │   ├── usePageContext.ts      # Context collection hook        │
│  │       │   └── index.ts               # Public exports                 │
│  │       └── package.json                                                │
│  │                                                                       │
│  └── templates/                                                          │
│      └── base-app/                                                       │
│          └── src/                                                        │
│              └── components/                                             │
│                  └── AskMozaiksWidget.tsx  # Pre-wired into app shell    │
│                                                                          │
│  ─────────────────────────────────────────────────────────────────────   │
│                                                                          │
│  MOZAIKS-AI (Runtime)                                                    │
│  ├── ChatUI/                                                             │
│  │   └── src/                                                            │
│  │       ├── components/                                                 │
│  │       │   └── chat/                                                   │
│  │       │       ├── PersistentChatWidget.js  # Existing minimized chat  │
│  │       │       └── ChatInterface.js         # Shared chat renderer     │
│  │       ├── context/                                                    │
│  │       │   └── ChatUIContext.js             # Shared state             │
│  │       └── hooks/                                                      │
│  │           ├── useWidgetMode.js             # Widget mode management   │
│  │           └── useContextInjection.js       # NEW: page context hook   │
│  │                                                                       │
│  ├── core/                                                               │
│  │   ├── transport/                                                      │
│  │   │   └── websocket.py                     # WebSocket handler        │
│  │   └── workflow/                                                       │
│  │       └── context_router.py                # NEW: context-based route │
│  │                                                                       │
│  └── workflows/                                                          │
│      └── AskMozaiks/                          # NEW: Ask mode workflow   │
│          ├── workflow.yaml                                               │
│          ├── agents.yaml                                                 │
│          └── tools.yaml                                                  │
│                                                                          │
│  ─────────────────────────────────────────────────────────────────────   │
│                                                                          │
│  CONTROL-PLANE (Platform Hub)                                            │
│  └── api/                                                                │
│      └── widget/                                                         │
│          └── config.py                        # Widget feature flags     │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Integration with mozaiks-core UI

### 4.1 Three Integration Strategies

| Strategy | Description | Pros | Cons |
|----------|-------------|------|------|
| **Core UI Component** | Ship as part of every generated app's base template | Always available, consistent | Heavier app bundle |
| **System Plugin** | Load as optional plugin via plugin registry | Opt-in, lighter base | Requires plugin system |
| **Runtime Injection** | Inject via script tag from CDN | Zero app changes | CORS, versioning complexity |

### 4.2 Recommended Approach: Core UI Component + Lazy Loading

```javascript
// In every generated app's root layout (from mozaiks-core template)

// src/layouts/RootLayout.tsx
import { Suspense, lazy } from 'react';

// Lazy load the widget to avoid blocking initial render
const AskMozaiksWidget = lazy(() => import('@mozaiks/widget-sdk'));

export function RootLayout({ children }) {
  return (
    <>
      {children}
      
      {/* Widget renders after main content */}
      <Suspense fallback={null}>
        <AskMozaiksWidget
          appId={process.env.MOZAIKS_APP_ID}
          position="bottom-right"
          theme="auto"
        />
      </Suspense>
    </>
  );
}
```

### 4.3 Widget SDK API

```typescript
// @mozaiks/widget-sdk API

interface AskMozaiksWidgetProps {
  // Required
  appId: string;                    // From Control-Plane
  
  // Optional - Appearance
  position?: 'bottom-right' | 'bottom-left';
  theme?: 'light' | 'dark' | 'auto';
  primaryColor?: string;            // Override --color-primary
  
  // Optional - Behavior
  defaultOpen?: boolean;
  contextProvider?: () => PageContext;
  onWorkflowTrigger?: (workflowId: string) => void;
  
  // Optional - Feature flags
  enableSuggestions?: boolean;      // Show contextual suggestions
  enableVoice?: boolean;            // Voice input (future)
}

interface PageContext {
  route: string;                    // Current URL path
  pageType: string;                 // e.g., 'analytics', 'settings', 'dashboard'
  entityId?: string;                // If viewing a specific record
  entityType?: string;              // e.g., 'user', 'order', 'product'
  metadata?: Record<string, any>;   // Additional page-specific data
}

// Imperative API for programmatic control
interface AskMozaiksAPI {
  open(): void;
  close(): void;
  toggle(): void;
  ask(question: string): Promise<void>;
  triggerWorkflow(workflowId: string, context?: object): void;
  setContext(context: PageContext): void;
}

// Exposed on window for non-React integration
declare global {
  interface Window {
    AskMozaiks: AskMozaiksAPI;
  }
}
```

---

## 5. Context-Aware Behavior

### 5.1 How the Widget Knows What Screen the User Is On

```
┌─────────────────────────────────────────────────────────────────────────┐
│                   CONTEXT COLLECTION PIPELINE                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    PAGE SIGNALS                                    │  │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐  │  │
│  │  │ URL/Route   │ │ Page Title  │ │ DOM State   │ │ App Events  │  │  │
│  │  │ /analytics  │ │ "Sales Q4"  │ │ #chart-view │ │ onDataLoad  │  │  │
│  │  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘ └──────┬──────┘  │  │
│  │         │               │               │               │          │  │
│  └─────────┼───────────────┼───────────────┼───────────────┼──────────┘  │
│            │               │               │               │             │
│            └───────────────┴───────────────┴───────────────┘             │
│                                    │                                     │
│                                    ▼                                     │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    usePageContext() Hook                          │  │
│  │  • Subscribes to route changes                                    │  │
│  │  • Reads page manifest (if available)                             │  │
│  │  • Collects visible entity IDs                                    │  │
│  │  • Debounces rapid changes                                        │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                    │                                     │
│                                    ▼                                     │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    Page Context Object                            │  │
│  │  {                                                                │  │
│  │    "route": "/analytics/sales",                                   │  │
│  │    "pageType": "analytics",                                       │  │
│  │    "pageTitle": "Sales Dashboard - Q4 2025",                      │  │
│  │    "entityType": "report",                                        │  │
│  │    "entityId": "rpt_q4_2025",                                     │  │
│  │    "visibleData": ["chart", "table", "filters"],                  │  │
│  │    "capabilities": ["export", "share", "drill-down"]              │  │
│  │  }                                                                │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                    │                                     │
│                                    ▼                                     │
│                            Sent with every message                       │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Automatic Workflow Suggestions

```yaml
# Suggestion rules configured per app or globally
suggestion_rules:
  - trigger:
      pageType: "analytics"
      conditions:
        - visibleData contains "chart"
    suggestions:
      - label: "📊 Generate report from this data"
        workflow: "report_generator"
        context_map:
          data_source: "entityId"
      - label: "📤 Export to CSV"
        action: "quick_export"
        
  - trigger:
      pageType: "settings"
      conditions:
        - route matches "/settings/users/*"
    suggestions:
      - label: "👤 Add new user"
        workflow: "user_onboarding"
      - label: "🔐 Review permissions"
        workflow: "permission_audit"
        
  - trigger:
      pageType: "error"
    suggestions:
      - label: "🔍 Diagnose this issue"
        workflow: "error_diagnosis"
        context_map:
          error_details: "metadata.error"
```

### 5.3 Context Injection into Agent Prompts

```python
# core/workflow/context_router.py

class ContextAwareRouter:
    """Injects page context into agent system prompts."""
    
    def inject_context(self, 
                       agent_config: dict, 
                       page_context: dict) -> dict:
        """
        Augment agent's system prompt with page awareness.
        """
        if not page_context:
            return agent_config
            
        context_section = f"""
## Current User Context
- **Page**: {page_context.get('pageTitle', page_context.get('route', 'Unknown'))}
- **Page Type**: {page_context.get('pageType', 'general')}
- **Entity**: {page_context.get('entityType', 'none')} ({page_context.get('entityId', 'N/A')})
- **Available Actions**: {', '.join(page_context.get('capabilities', []))}

When responding, consider that the user is currently viewing this page.
If they ask about "this data" or "this page", refer to the context above.
"""
        
        # Prepend to system prompt
        agent_config['system_message'] = (
            context_section + "\n\n" + agent_config.get('system_message', '')
        )
        
        return agent_config
```

---

## 6. Runtime Flow

### 6.1 Complete Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      ASKMOZAIKS RUNTIME FLOW                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  USER IN APP                                                             │
│       │                                                                  │
│       │ clicks 🧠 widget button                                          │
│       ▼                                                                  │
│  ┌─────────────────────┐                                                │
│  │ AskMozaiks Widget   │                                                │
│  │ (mozaiks-core SDK)  │                                                │
│  └──────────┬──────────┘                                                │
│             │                                                            │
│             │ 1. Collect page context                                    │
│             │ 2. Open chat panel                                         │
│             ▼                                                            │
│  ┌─────────────────────┐                                                │
│  │ ChatUIContext       │ ◄── Shared state between widget & full chat    │
│  │ (mozaiks-ai/ChatUI) │                                                │
│  └──────────┬──────────┘                                                │
│             │                                                            │
│             │ 3. User types question or clicks suggestion                │
│             ▼                                                            │
│  ┌─────────────────────┐                                                │
│  │ WebSocket Transport │                                                │
│  │ (mozaiks-ai)        │                                                │
│  └──────────┬──────────┘                                                │
│             │                                                            │
│             │ 4. Message + page_context sent to backend                  │
│             │    {                                                       │
│             │      "message": "What does this chart show?",              │
│             │      "mode": "ask",                                        │
│             │      "page_context": { ... }                               │
│             │    }                                                       │
│             ▼                                                            │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    MOZAIKS-AI RUNTIME                            │    │
│  │  ┌─────────────────────────────────────────────────────────┐    │    │
│  │  │ Context Router                                          │    │    │
│  │  │ • Determines if this is quick-answer or workflow        │    │    │
│  │  │ • Injects page context into agent prompt                │    │    │
│  │  └─────────────────────────────────────────────────────────┘    │    │
│  │                          │                                      │    │
│  │          ┌───────────────┴───────────────┐                      │    │
│  │          ▼                               ▼                      │    │
│  │  ┌───────────────┐              ┌───────────────┐              │    │
│  │  │ AskMozaiks    │              │ Workflow      │              │    │
│  │  │ Agent         │              │ Orchestrator  │              │    │
│  │  │ (single LLM)  │              │ (AG2 GroupChat)│              │    │
│  │  └───────────────┘              └───────────────┘              │    │
│  │          │                               │                      │    │
│  │          │ 5a. Quick answer              │ 5b. Complex workflow │    │
│  │          ▼                               ▼                      │    │
│  │  ┌─────────────────────────────────────────────────────────┐    │    │
│  │  │ Response Streamer                                        │    │    │
│  │  │ • Streams tokens back via WebSocket                      │    │    │
│  │  │ • May include tool calls (e.g., export_data)             │    │    │
│  │  └─────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│             │                                                            │
│             │ 6. Response streamed to widget                             │
│             ▼                                                            │
│  ┌─────────────────────┐                                                │
│  │ AskMozaiks Widget   │                                                │
│  │ renders response    │                                                │
│  └──────────┬──────────┘                                                │
│             │                                                            │
│             │ 7. If workflow triggered, may prompt to expand             │
│             ▼                                                            │
│  ┌─────────────────────┐                                                │
│  │ ChatPage (optional) │ ◄── Full screen for complex interactions       │
│  │ with artifact panel │                                                │
│  └─────────────────────┘                                                │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 6.2 Simplified Flow (Text)

```
User → Widget → ChatUIContext → WebSocket → mozaiks-ai (Context Router) 
    → AskMozaiks Agent OR Workflow Orchestrator 
    → Response → WebSocket → Widget → User

If workflow requires artifacts:
    Widget → "Expand to full chat" → ChatPage → Artifact Panel
```

### 6.3 Control-Plane Involvement

Control-Plane is involved for:
1. **Initial auth**: JWT validation for widget session
2. **Feature flags**: Which apps have AskMozaiks enabled
3. **Usage tracking**: Token accounting per app_id
4. **Workflow permissions**: Which workflows this user can trigger

```
Widget ──JWT─→ Control-Plane ──validate─→ mozaiks-ai
                    │
                    └── feature_flags: { ask_mozaiks: true }
```

---

## 7. Data & Telemetry

### 7.1 Data the Widget Should Send to mozaiks-ai

```typescript
interface WidgetTelemetry {
  // Session identification
  session_id: string;           // Widget session (maps to chat_id)
  app_id: string;               // From Control-Plane
  user_id: string;              // Current user
  
  // Interaction signals
  events: WidgetEvent[];
}

interface WidgetEvent {
  timestamp: string;
  event_type: 
    | 'widget_opened'
    | 'widget_closed'
    | 'message_sent'
    | 'suggestion_clicked'
    | 'workflow_triggered'
    | 'workflow_completed'
    | 'workflow_cancelled'
    | 'expanded_to_full_chat'
    | 'error_occurred';
  
  payload: {
    // For message_sent
    message_length?: number;      // Not content, just length
    had_context?: boolean;        // Was page context attached?
    
    // For suggestion_clicked
    suggestion_id?: string;
    suggestion_label?: string;
    
    // For workflow events
    workflow_id?: string;
    workflow_name?: string;
    duration_ms?: number;
    outcome?: 'success' | 'failure' | 'cancelled';
    
    // For errors
    error_code?: string;
    error_category?: string;      // 'network' | 'auth' | 'timeout' | 'agent'
    
    // Context (anonymized)
    page_type?: string;           // e.g., 'analytics', not full URL
    entity_type?: string;         // e.g., 'order', not entity ID
  };
}
```

### 7.2 Learning Data Collection

```yaml
# What mozaiks-ai learns from widget interactions

learning_signals:
  user_intent:
    # Patterns of what users ask on which pages
    - page_type → common_questions mapping
    - question_patterns → workflow_recommendations
    - time_of_day → usage_patterns
    
  workflow_outcomes:
    # Which workflows succeed from widget vs full chat
    - workflow_id × surface → completion_rate
    - context_quality → success_correlation
    - suggestion_acceptance_rate
    
  errors_and_rejections:
    # What went wrong
    - error_patterns → fix_recommendations
    - user_corrections → intent_misclassifications
    - abandonment_points → UX_improvements
    
  context_effectiveness:
    # How well page context helps
    - context_provided → answer_quality_rating
    - context_fields_used → relevance_score
```

### 7.3 Privacy Considerations

```yaml
privacy_rules:
  never_collect:
    - Actual message content (only length/patterns)
    - Entity IDs or PII
    - Full URLs (only route patterns)
    - User-entered data from forms
    
  anonymize:
    - Aggregate by app_id, not user_id for learning
    - Hash patterns before storage
    - Retention: 90 days for telemetry, patterns indefinite
    
  user_controls:
    - Opt-out of telemetry per user
    - Export personal data on request
    - Delete history on demand
```

---

## 8. Folder Structure

### 8.1 Proposed Structure in mozaiks-ai

```
MozaiksAI/
├── ChatUI/
│   └── src/
│       ├── components/
│       │   └── chat/
│       │       ├── PersistentChatWidget.js    # Existing - KEEP
│       │       ├── ChatInterface.js           # Existing - KEEP
│       │       ├── AskMozaiksPanel.js         # NEW: Widget panel renderer
│       │       └── ContextualSuggestions.js   # NEW: Suggestion chips
│       │
│       ├── context/
│       │   ├── ChatUIContext.js               # Existing - EXTEND
│       │   └── PageContextProvider.js         # NEW: Page context state
│       │
│       └── hooks/
│           ├── useWidgetMode.js               # Existing - KEEP
│           ├── usePageContext.js              # NEW: Context collection
│           └── useContextualSuggestions.js    # NEW: Suggestion logic
│
├── core/
│   ├── runtime/
│   │   └── context_injection.py               # NEW: Context → prompt injection
│   │
│   ├── transport/
│   │   └── websocket.py                       # Existing - EXTEND for context
│   │
│   └── workflow/
│       ├── workflow_manager.py                # Existing
│       └── context_router.py                  # NEW: Route based on context
│
├── workflows/
│   └── AskMozaiks/                            # NEW: Ask mode workflow
│       ├── workflow.yaml
│       ├── agents.yaml
│       ├── tools.yaml
│       └── suggestions/
│           ├── rules.yaml                     # Suggestion rules
│           └── page_patterns.yaml             # Page type → suggestions
│
└── evaluation/
    └── widget/                                # NEW: Widget analytics
        ├── telemetry_collector.py
        ├── intent_analyzer.py
        └── suggestion_optimizer.py
```

### 8.2 Proposed Structure in mozaiks-core (SDK)

```
mozaiks-core/
└── packages/
    └── widget-sdk/
        ├── package.json
        ├── tsconfig.json
        ├── src/
        │   ├── index.ts                       # Public exports
        │   ├── AskMozaiksWidget.tsx           # Main component
        │   ├── AskMozaiksButton.tsx           # Floating button
        │   ├── AskMozaiksPanel.tsx            # Chat panel shell
        │   ├── hooks/
        │   │   ├── usePageContext.ts          # Context collection
        │   │   └── useWidgetState.ts          # Open/close state
        │   ├── context/
        │   │   └── WidgetContext.tsx          # Widget-specific state
        │   └── types/
        │       └── index.ts                   # TypeScript definitions
        └── README.md
```

---

## 9. Concrete Recommendation

### 9.1 Decision: Core UI Component (Lazy-Loaded)

**AskMozaiks should be a CORE UI COMPONENT shipped in every Mozaiks-generated app**, with the following characteristics:

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| **Deployment** | Bundled in app, lazy-loaded | Available everywhere, doesn't block initial render |
| **State** | Shared with ChatUIContext | Seamless context handoff to full chat |
| **Backend** | Single WebSocket to mozaiks-ai | No separate backend needed |
| **Configuration** | Via app_manifest + Control-Plane | Per-app customization, feature flags |
| **Context** | Automatic via usePageContext | Zero-config for generated pages |

### 9.2 Files That Should Exist

#### In mozaiks-ai (This Repo)

| File | Purpose | Priority |
|------|---------|----------|
| `ChatUI/src/components/chat/AskMozaiksPanel.js` | Widget panel UI (reuses ChatInterface) | High |
| `ChatUI/src/components/chat/ContextualSuggestions.js` | Render suggestion chips | High |
| `ChatUI/src/hooks/usePageContext.js` | Collect page context | High |
| `ChatUI/src/hooks/useContextualSuggestions.js` | Compute suggestions from context | Medium |
| `ChatUI/src/context/PageContextProvider.js` | Store page context in React | High |
| `core/workflow/context_router.py` | Route messages based on context | Medium |
| `core/runtime/context_injection.py` | Inject context into prompts | Medium |
| `workflows/AskMozaiks/workflow.yaml` | Ask mode workflow definition | High |
| `workflows/AskMozaiks/agents.yaml` | Ask agent config | High |
| `workflows/AskMozaiks/suggestions/rules.yaml` | Suggestion rules config | Medium |
| `evaluation/widget/telemetry_collector.py` | Collect widget analytics | Low |

#### In mozaiks-core (SDK Package)

| File | Purpose | Priority |
|------|---------|----------|
| `packages/widget-sdk/src/AskMozaiksWidget.tsx` | Main embeddable component | High |
| `packages/widget-sdk/src/AskMozaiksButton.tsx` | Floating button | High |
| `packages/widget-sdk/src/hooks/usePageContext.ts` | Context collection for SDK | High |
| `packages/widget-sdk/src/types/index.ts` | TypeScript definitions | High |
| `packages/widget-sdk/package.json` | NPM package config | High |

#### Updates to Existing Files

| File | Change | Priority |
|------|--------|----------|
| `ChatUI/src/context/ChatUIContext.js` | Add `pageContext` state | High |
| `ChatUI/src/components/chat/PersistentChatWidget.js` | Wire to AskMozaiksPanel | High |
| `core/transport/websocket.py` | Accept `page_context` in messages | Medium |
| Generated app templates | Include `<AskMozaiksWidget />` in root layout | High |

### 9.3 Implementation Priority

```
Phase 1 (Foundation)
├── usePageContext hook
├── ChatUIContext extension
├── AskMozaiksPanel component
└── WebSocket context support

Phase 2 (Intelligence)
├── context_router.py
├── AskMozaiks workflow
├── Suggestion rules
└── Context injection

Phase 3 (SDK & Distribution)
├── widget-sdk package
├── Template integration
└── Documentation

Phase 4 (Learning)
├── Telemetry collection
├── Suggestion optimization
└── Intent analysis
```

---

## Appendix A: Message Protocol Extension

```typescript
// Extended message format for widget context

interface WidgetMessage {
  // Existing fields
  chat_id: string;
  message: string;
  mode: 'ask' | 'workflow';
  
  // NEW: Page context
  page_context?: {
    route: string;
    pageType: string;
    pageTitle?: string;
    entityType?: string;
    entityId?: string;
    capabilities?: string[];
    metadata?: Record<string, any>;
  };
  
  // NEW: Suggestion tracking
  triggered_by?: {
    type: 'suggestion' | 'user_input' | 'workflow_auto';
    suggestion_id?: string;
  };
  
  // NEW: Surface indicator
  surface: 'widget' | 'full_chat' | 'api';
}
```

---

## Appendix B: Suggestion Rule Schema

```yaml
# Schema for suggestion rules

suggestion_rule:
  type: object
  required: [trigger, suggestions]
  properties:
    trigger:
      type: object
      properties:
        pageType:
          type: string
        route:
          type: string
          description: Regex pattern for route matching
        conditions:
          type: array
          items:
            type: string
            description: "Expression like 'visibleData contains chart'"
    
    suggestions:
      type: array
      items:
        type: object
        required: [label]
        properties:
          label:
            type: string
            description: Display text with optional emoji
          workflow:
            type: string
            description: Workflow ID to trigger
          action:
            type: string
            description: Quick action ID (non-workflow)
          context_map:
            type: object
            description: Map page context fields to workflow inputs
          priority:
            type: integer
            default: 0
            description: Higher = shown first
```

---

## Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-01-17 | Engineering Agent | Initial architecture definition |

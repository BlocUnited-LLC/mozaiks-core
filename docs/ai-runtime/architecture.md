# 🤖 AI Runtime Architecture
> **Doc Status:** reference (not contract-critical)

> The AI Runtime is the **agent orchestration engine** — built on AG2 (Autogen), it manages workflows, agents, tools, and real-time streaming.

---

## 🎯 What is the AI Runtime?

```mermaid
graph TB
    subgraph "Frontend"
        CHAT[ChatUI]
    end
    
    subgraph "AI Runtime"
        WM[Workflow Manager]
        ORCH[Orchestration Engine]
        TRANS[Transport Layer]
        PERSIST[Persistence]
        
        subgraph "AG2"
            A1[Agent 1]
            A2[Agent 2]
            A3[Agent 3]
            TOOLS[Tools]
        end
    end
    
    subgraph "External"
        LLM[OpenAI/LLMs]
        DB[(MongoDB)]
    end
    
    CHAT <-->|WebSocket| TRANS
    TRANS <--> ORCH
    ORCH <--> WM
    WM --> A1
    WM --> A2
    WM --> A3
    A1 <--> TOOLS
    A1 <--> LLM
    ORCH --> PERSIST
    PERSIST --> DB
    
    style ORCH fill:#9c27b0,color:#fff
    style TRANS fill:#2196f3,color:#fff
```

**The AI Runtime handles:**
- ✅ Loading workflow configurations
- ✅ Creating and configuring AG2 agents
- ✅ Executing multi-agent orchestration
- ✅ Streaming responses to the frontend
- ✅ Persisting chat history
- ✅ Observability and metrics

---

## 🧱 Core Components

### 1️⃣ Workflow Manager

Discovers and loads workflow configurations from JSON manifests.

```mermaid
graph LR
    subgraph "workflows/"
        WF1[Generator/]
        WF2[Analyzer/]
        WF3[Assistant/]
    end
    
    WM[Workflow Manager]
    
    WF1 --> WM
    WF2 --> WM
    WF3 --> WM
    
    WM -->|Load| CONFIG[Workflow Config]
    CONFIG -->|Create| AGENTS[Agents]
    
    style WM fill:#4caf50,color:#fff
```

**Workflow structure:**
```
workflows/
└── Generator/
    ├── workflow.json       # Main config
    ├── agents.json         # Agent definitions
    ├── tools.json          # Tool registrations
    ├── context_variables.json
    └── prompts/
        ├── orchestrator.md
        └── generator.md
```

### 2️⃣ Orchestration Engine

Executes AG2 multi-agent patterns.

```mermaid
sequenceDiagram
    participant User
    participant Orchestrator
    participant Agent1
    participant Agent2
    participant Tool
    participant LLM
    
    User->>Orchestrator: "Create a landing page"
    Orchestrator->>Agent1: Analyze request
    Agent1->>LLM: Generate response
    LLM-->>Agent1: Analysis result
    Agent1-->>Orchestrator: Hand off to Agent2
    Orchestrator->>Agent2: Generate content
    Agent2->>Tool: Execute tool
    Tool-->>Agent2: Tool result
    Agent2->>LLM: Final generation
    LLM-->>Agent2: Content
    Agent2-->>Orchestrator: Complete
    Orchestrator-->>User: Final result
```

**Orchestration Patterns:**
| Pattern | Description |
|---------|-------------|
| `Default` | Sequential handoffs |
| `Auto` | Automatic tool execution |
| `RoundRobin` | Rotate through agents |
| `Random` | Random agent selection |

### 3️⃣ Transport Layer

Real-time WebSocket streaming between runtime and ChatUI.

```mermaid
graph LR
    subgraph "Runtime"
        AGENT[Agent]
        EVT[Event Dispatcher]
        TRANS[Transport]
    end
    
    subgraph "Frontend"
        CHAT[ChatUI]
    end
    
    AGENT -->|AG2 Event| EVT
    EVT -->|Filter & Format| TRANS
    TRANS <-->|WebSocket| CHAT
    
    style TRANS fill:#2196f3,color:#fff
```

**Key features:**
- Agent visibility filtering (hide internal agents)
- Event envelope construction
- Pre-connection buffering
- Heartbeat management

### 4️⃣ Persistence Layer

Stores chat sessions and messages in MongoDB.

```javascript
// chat_sessions collection
{
    "_id": ObjectId("..."),
    "chat_id": "uuid",
    "app_id": "my_app",
    "user_id": "user_123",
    "workflow": "Generator",
    "created_at": "...",
    "updated_at": "...",
    "message_count": 15,
    "messages": [
        {
            "role": "user",
            "content": "Create a landing page",
            "timestamp": "..."
        },
        {
            "role": "assistant",
            "agent": "Generator",
            "content": "I'll help you create...",
            "timestamp": "..."
        }
    ]
}
```

---

## 📂 Directory Structure

```
runtime/ai/
├── main.py                     # FastAPI entry point
├── shared_app.py               # AI runtime FastAPI app
├── core/
│   ├── workflow/
│   │   ├── workflow_manager.py     # Load workflows
│   │   ├── orchestration_patterns.py
│   │   └── agent_factory.py        # Create agents
│   ├── transport/
│   │   └── simple_transport.py     # WebSocket streaming
│   ├── events/
│   │   └── unified_event_dispatcher.py
│   ├── persistence/
│   │   └── ag2_persistence_manager.py
│   └── observability/
│       └── performance_manager.py
├── workflows/                  # Workflow definitions
│   ├── Generator/
│   ├── Analyzer/
│   └── ...
└── tools/                      # Tool implementations
    ├── web_tools.py
    ├── file_tools.py
    └── ...
```

---

## 🔄 Request Flow

```mermaid
sequenceDiagram
    participant ChatUI
    participant Transport
    participant Orchestration
    participant AG2
    participant LLM
    participant Persistence
    
    ChatUI->>Transport: WebSocket connect
    Transport-->>ChatUI: Connection ACK
    
    ChatUI->>Transport: User message
    Transport->>Orchestration: Start workflow
    Orchestration->>AG2: Run agents
    
    loop Agent Turns
        AG2->>LLM: Generate
        LLM-->>AG2: Response
        AG2->>Transport: Stream event
        Transport-->>ChatUI: Display message
    end
    
    Orchestration->>Persistence: Save chat
    Orchestration-->>Transport: Complete
    Transport-->>ChatUI: Done
```

---

## ⚙️ Configuration

### Environment Variables

```env
# AI Runtime
OPENAI_API_KEY=sk-...
OPENAI_MODEL=gpt-4o
MOZAIKS_AI_ENABLED=true

# Workflow paths
MOZAIKS_WORKFLOWS_PATH=/app/workflows

# Persistence
DATABASE_URI=mongodb://...
```

### Workflow Configuration

**`workflow.json`**
```json
{
    "name": "Generator",
    "description": "Content generation workflow",
    "orchestration_pattern": "Auto",
    "max_turns": 20,
    "visual_agents": ["Generator", "Reviewer"],
    "entry_agent": "Orchestrator"
}
```

**`agents.json`**
```json
{
    "agents": [
        {
            "name": "Orchestrator",
            "type": "orchestrator",
            "llm_config": {
                "model": "gpt-4o",
                "temperature": 0.7
            },
            "system_message_file": "prompts/orchestrator.md"
        },
        {
            "name": "Generator",
            "type": "assistant",
            "tools": ["web_search", "generate_content"],
            "system_message_file": "prompts/generator.md"
        }
    ]
}
```

---

## 📊 Observability

### Metrics Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /metrics/perf/aggregate` | Overall AI metrics |
| `GET /metrics/perf/chats` | Per-chat metrics |
| `GET /metrics/perf/chat/{id}` | Single chat metrics |

### Available Metrics

- Agent turns count
- Tool calls count
- Token usage
- Cost tracking
- Response latency

---

## 🔗 Related

- 📖 [Core Architecture](../core/architecture.md) — Core system overview
- 📡 [WebSockets](../core/websockets.md) — WebSocket streaming
- 🗄️ [Database](../core/database.md) — MongoDB persistence


# Event Bus Architecture Audit

## Executive Summary

mozaiks-core has **three distinct event systems** operating at different scopes:

| Layer | Tech | Scope | Transport |
|-------|------|-------|-----------|
| **Python In-Process** | `EventBus` (singleton) | Within Python runtime | In-memory callbacks |
| **AI Runtime Events** | `UnifiedEventDispatcher` | AI workflow ↔ UI | WebSocket (SimpleTransport) |
| **Cross-Service** | MassTransit + RabbitMQ | .NET APIs | AMQP broker |

Currently, **Python runtime → .NET APIs** communication uses **direct HTTP calls** (aiohttp/httpx), not a message broker.

---

## Current Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              FRONTEND (Browser)                              │
└────────────────────────────────┬────────────────────────────────────────────┘
                                 │ WebSocket
                                 ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Python AI Runtime                                    │
│                                                                              │
│  ┌──────────────────┐     ┌────────────────────────┐                        │
│  │   EventBus       │     │  UnifiedEventDispatcher │                        │
│  │  (in-process)    │     │    (AI events → UI)     │                        │
│  │                  │     │                         │                        │
│  │ • settings_*     │     │ • chat.usage_delta      │                        │
│  │ • notification_* │     │ • chat.run_complete     │                        │
│  │ • subscription_* │     │ • chat.structured_*     │                        │
│  └────────┬─────────┘     └──────────┬──────────────┘                        │
│           │                          │                                       │
│           ▼                          ▼                                       │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                      SimpleTransport                                  │    │
│  │  • WebSocket connection management                                    │    │
│  │  • AG2 event serialization (event_serialization.py)                   │    │
│  │  • Message filtering & forwarding                                     │    │
│  └──────────────────────────────────┬───────────────────────────────────┘    │
│                                     │                                        │
└─────────────────────────────────────┼────────────────────────────────────────┘
                                      │
         ┌───────────────────HTTP (aiohttp)────────────────┐
         │                                                  │
         ▼                                                  ▼
┌─────────────────────┐                        ┌─────────────────────┐
│  Email Service      │                        │   Other APIs        │
│  (external)         │                        │   (HTTP calls)      │
└─────────────────────┘                        └─────────────────────┘

                    ════════════════════════════════════════
                         .NET Service Boundary (Separate)
                    ════════════════════════════════════════

┌─────────────────────────────────────────────────────────────────────────────┐
│                         .NET Backend Services                                │
│                                                                              │
│  ┌───────────────────┐    RabbitMQ     ┌───────────────────────┐            │
│  │  Notification.API │◄───(AMQP)───────│ (Future Publishers)   │            │
│  │                   │                 │                        │            │
│  │ DirectMessage     │                 │ HostingProvisioning*   │            │
│  │ Consumer          │                 │ (events defined        │            │
│  └───────────────────┘                 │  but no publishers)    │            │
│                                        └───────────────────────┘            │
│  ┌───────────────────┐                                                      │
│  │  Plugins.API      │  ← No event bus integration                          │
│  └───────────────────┘                                                      │
│  ┌───────────────────┐                                                      │
│  │  Identity.API     │  ← No event bus integration                          │
│  └───────────────────┘                                                      │
│  ┌───────────────────┐                                                      │
│  │  Insights.API     │  ← No event bus integration                          │
│  └───────────────────┘                                                      │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Layer 1: Python In-Process EventBus

**Location:** [core/event_bus.py](runtime/ai/core/event_bus.py)

### Pattern
```python
from core.event_bus import event_bus

# Publisher
event_bus.publish("settings_updated", {"user_id": "...", "changes": {...}})

# Subscriber
event_bus.subscribe("settings_updated", my_handler_callback)
```

### Current Events Published

| Event Type | Publisher | Subscribers |
|------------|-----------|-------------|
| `settings_updated` | `settings_manager.py` | Unknown |
| `notification_preferences_updated` | `settings_manager.py`, `notifications.py`, `notifications_manager.py` | Unknown |
| `all_notifications_read` | `notifications.py` | Unknown |
| `subscription_updated` | External trigger | `notifications_manager.py` |
| `subscription_canceled` | External trigger | `notifications_manager.py` |
| `plugin_settings_updated` | External trigger | `notifications_manager.py` |
| `ops_shutdown_request` | `signals.py` | App lifecycle |
| `ops_status_update` | `signals.py` | Health monitors |

### Where Broker Could Plug In

**Adapter Location:** Wrap `event_bus.publish()` to optionally forward to broker:

```python
# core/event_bus.py - potential adapter point

async def publish(self, event: str, data: dict):
    # 1. Local delivery (current behavior)
    self._deliver_locally(event, data)
    
    # 2. Optional broker forwarding (new)
    if self._broker_adapter:
        await self._broker_adapter.publish(event, data)
```

---

## Layer 2: AI Runtime UnifiedEventDispatcher

**Location:** [core/ai_runtime/events/unified_event_dispatcher.py](runtime/ai/core/ai_runtime/events/unified_event_dispatcher.py)

### Purpose
Handles AI workflow events (business/UI tool events) and forwards to registered handlers.

### Key Events

| Event Type | Description | Handler |
|------------|-------------|---------|
| `chat.structured_output_ready` | AG2 produces structured artifact | `AutoToolEventHandler`, `WorkflowPackCoordinator` |
| `chat.run_complete` | Workflow execution finished | `WorkflowPackCoordinator`, `JourneyOrchestrator` |
| `chat.usage_delta` | Token usage increment | `SimpleTransport._handle_usage_delta_event` |
| `chat.usage_summary` | Final token usage | `UsageIngestClient.handle_usage_summary` |

### Where Broker Could Plug In

**Adapter Location:** Add broker handler alongside existing handlers:

```python
# unified_event_dispatcher.py - potential adapter point

class UnifiedEventDispatcher:
    def __init__(self):
        # ... existing setup ...
        
        # Optional broker integration
        if broker := get_optional_broker():
            self.register_handler("chat.usage_summary", broker.publish_usage)
            self.register_handler("chat.run_complete", broker.publish_run_complete)
```

---

## Layer 3: WebSocket Transport (SimpleTransport)

**Location:** [core/ai_runtime/transport/simple_transport.py](runtime/ai/core/ai_runtime/transport/simple_transport.py)

### Purpose
Real-time bidirectional communication between AI runtime and browser UI.

### Key Methods

| Method | Purpose |
|--------|---------|
| `send_event_to_ui(event, chat_id)` | Forward AG2/workflow events to browser |
| `submit_user_input(request_id, input)` | Receive user input from browser |
| `register_input_request(chat_id, request_id, callback)` | Set up input collection |

### Event Serialization

**Location:** [core/ai_runtime/events/event_serialization.py](runtime/ai/core/ai_runtime/events/event_serialization.py)

Converts raw AG2 events to UI-friendly payloads:

```python
# Event kinds produced:
# - "message" (text content)
# - "tool_call" (function invocation)  
# - "tool_result" (function response)
# - "handoff" (agent transitions - NEW)
# - "input_request" (needs user input)
# - "complete" / "error" / "cancelled"
```

### Where Broker Could Plug In

**Not recommended for real-time UI events.** WebSocket latency (ms) vs broker latency (100ms+) would degrade UX.

However, for **cross-service event replay** or **durable event storage**:

```python
# simple_transport.py - potential audit/replay point

async def send_event_to_ui(self, event: dict, chat_id: str):
    # 1. Real-time delivery (current - keep fast)
    await self._ws_send(event, chat_id)
    
    # 2. Optional async broker publish for durability/cross-service
    if self._event_auditor:
        asyncio.create_task(self._event_auditor.record(event, chat_id))
```

---

## Layer 4: .NET Cross-Service (MassTransit)

**Location:** [Notification.API/Program.cs](backend/src/Notification.API/Notification.API/Program.cs)

### Current Setup

```csharp
// Only configured if EventBusSettings:HostAddress is set
if (!string.IsNullOrWhiteSpace(eventBusHostAddress))
{
    builder.Services.AddMassTransit(config =>
    {
        config.AddConsumer<DirectMessageSentConsumer>();
        
        config.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(eventBusHostAddress);
            cfg.ReceiveEndpoint(EventBusConstants.DIRECT_MESSAGE_SENT_QUEUE, e =>
            {
                e.ConfigureConsumer<DirectMessageSentConsumer>(ctx);
            });
        });
    });
}
```

### Defined Events (EventBus.Messages)

| Event | Queue | Consumer | Publisher |
|-------|-------|----------|-----------|
| `DirectMessageSentEvent` | `direct-message-sent-queue` | `DirectMessageSentConsumer` | **None (unused)** |
| `HostingProvisioningRequestedEvent` | `hosting-provisioning-request-queue` | **None** | **None** |
| `HostingProvisioningCompletedEvent` | N/A | **None** | **None** |
| `HostingProvisioningFailedEvent` | N/A | **None** | **None** |

### Gap Analysis

The .NET event contracts exist but:
1. **No publishers** - Python runtime uses HTTP calls, not broker
2. **Hosting events unused** - Defined but no implementation
3. **Optional in dev** - Broker only required in production

---

## Where FastStream Would Plug In

If adopting FastStream for Python → .NET communication:

### Option A: Adapter Pattern (Recommended for OSS)

```python
# runtime/ai/core/broker_adapter.py (new file)

from typing import Protocol, Optional
import os

class BrokerAdapter(Protocol):
    """Optional broker integration - not required for self-hosting."""
    
    async def publish(self, topic: str, payload: dict) -> None: ...
    async def close(self) -> None: ...


class NoOpBroker:
    """Default: no broker, events stay in-process."""
    async def publish(self, topic: str, payload: dict) -> None:
        pass
    async def close(self) -> None:
        pass


def get_broker_adapter() -> BrokerAdapter:
    """Factory that returns appropriate broker based on config."""
    broker_url = os.getenv("MOZAIKS_BROKER_URL")
    
    if not broker_url:
        return NoOpBroker()  # Self-hosted: no broker required
    
    # Optional FastStream integration
    if broker_url.startswith("amqp://"):
        from .faststream_adapter import RabbitMQBrokerAdapter
        return RabbitMQBrokerAdapter(broker_url)
    
    return NoOpBroker()
```

### Option B: Integration Points

| Python Source | Event | .NET Consumer | Integration Point |
|---------------|-------|---------------|-------------------|
| `notifications_manager.create_notification()` | `notification.created` | `Notification.API` | After DB insert |
| `SimpleTransport._handle_usage_summary_event()` | `chat.usage.summary` | `Billing.API` (future) | After run complete |
| Plugin execution complete | `plugin.execution.complete` | `Insights.API` | After tool call |

---

## Recommendations

### 1. Keep Event Buses Separate (Current State)
- In-process for low-latency Python coordination
- WebSocket for real-time UI
- MassTransit for .NET-to-.NET (when needed)

### 2. Add Optional Broker Adapter
- Don't require broker for self-hosting
- Provide adapter interface for platform integration
- FastStream is a good choice if/when needed

### 3. Define Clear Boundaries
- **In-process:** Settings changes, subscription updates, signals
- **WebSocket:** AG2 events, UI tools, input requests
- **Broker:** Cross-service notifications, usage tracking, audit logs

### 4. Prioritize These Integration Points
1. `chat.usage_summary` → Billing (high value for platform)
2. `notification.created` → Cross-service notifications
3. `plugin.execution.*` → Analytics/insights

---

## Files Modified in This Session

| File | Change |
|------|--------|
| [event_serialization.py](runtime/ai/core/ai_runtime/events/event_serialization.py) | Added native AG2 transition event handling |
| [events/__init__.py](runtime/ai/core/ai_runtime/events/__init__.py) | Removed legacy handoff exports |
| [handoff_events.py](runtime/ai/core/ai_runtime/events/handoff_events.py) | Marked for deletion (exports removed) |

---

## Next Steps

1. **Delete** `handoff_events.py` (legacy placeholder)
2. **Consider** adding `broker_adapter.py` interface for optional cross-service
3. **Document** which events should cross service boundaries
4. **Test** native AG2 transition events flow through serialization

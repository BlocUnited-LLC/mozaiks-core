# MozaiksCore Event Bus (In-Process)

MozaiksCore uses a simple **in-process** event bus for internal orchestration (plugins, settings, subscriptions, notifications). All publish/subscribe happens inside a single running application instance.

## Where it lives

- Bus implementation: `../backend/core/events/bus_base.py` (`InMemoryEventBus`)
- Singleton instance + helpers: `../backend/core/event_bus.py` (`event_bus`, `on_event`)
- Startup wiring: `../backend/core/director.py` (connect/close lifecycle hooks)

## How to use it

Publish an event:

- `event_bus.publish("event_name", {"app_id": "...", "user_id": "...", ...})`

Subscribe a handler:

- `event_bus.subscribe("event_name", handler)`

Decorator convenience:

- `@on_event("event_name")`

Guidelines:

- Keep payloads minimal and scoped (include `app_id`/`user_id` where relevant to preserve tenant isolation).
- Do not log secrets from event payloads.

## Relationship to runtime integration

Runtime execution/workflow integration is via the existing HTTP + WebSocket surface and remains independent of the in-process event bus. The runtime bridge logic is implemented in `../backend/core/runtime/manager.py`.

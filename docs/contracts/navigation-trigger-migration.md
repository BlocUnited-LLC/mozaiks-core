> **Doc Status:** guide

# Navigation Trigger Migration Guide

This guide explains how to migrate from route-based navigation to workflow-triggered navigation items.

## 1) Keep existing routes

Route-based navigation remains supported. Items with `path` continue to work as before:

```json
{
  "label": "Dashboard",
  "path": "/dashboard",
  "icon": "home"
}
```

## 2) Add a workflow trigger

To start a workflow instead of routing, add a `trigger` object:

```json
{
  "label": "Discovery",
  "icon": "compass",
  "trigger": {
    "type": "workflow",
    "workflow": "DiscoveryDashboard",
    "input": { "view": "marketplace" },
    "mode": "view",
    "cache_ttl": 300
  }
}
```

### Trigger fields
- `type`: must be `"workflow"`.
- `workflow`: workflow name to start.
- `input`: optional structured input payload (forward-compatible).
- `mode`: `view | workflow | ask` (drives layout mode).
- `cache_ttl`: optional TTL in seconds for artifact caching.

## 3) Prefer trigger when both are present

If both `path` and `trigger` are provided, clients should prefer `trigger` for click handling.

## 4) Optional caching

If `cache_ttl` is set, clients may:
- read cached artifacts using key `artifact:{workflow}:{input_hash}`
- render cached artifact immediately
- refresh by starting the workflow in the background

## 5) Testing checklist

- Navigation item starts the workflow via `POST /api/chats/{app_id}/{workflow}/start`.
- UI connects to the workflow WebSocket.
- Layout mode switches based on `trigger.mode`.
- Optional cache hits render immediately and refresh as expected.

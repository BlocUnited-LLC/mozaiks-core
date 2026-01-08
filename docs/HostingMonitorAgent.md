# Mozaiks Hosting Monitor Agent (LLM Ops Agent)

This document defines the **prompts** and **tooling contract** for an agent whose job is to:

- monitor platform/app metrics (per `app_id`)
- evaluate them against the Hosting Operator policy
- take **safe automated actions** (scale/throttle/notify) when allowed
- produce **human-readable next steps** when automation is not allowed

This agent lives in the **Mozaiks Platform (control plane)**, not inside every customer app runtime.

Related docs:
- `docs/Mozaiks_OpsPlaybook.md`
- `docs/Mozaiks_ChangeSets_and_HostingOperator.md`
- `docs/HostingOperatorPolicy.example.json`
- `backend/core/hosting_operator.py`

---

## 1) Design Principle (important)

Use the LLM to **explain and coordinate**, not to invent actions.

Recommended operating model:

1) A deterministic evaluator (like `backend/core/hosting_operator.py`) turns metrics -> allowed actions.
2) The agent:
   - executes the allowed auto-actions via tools
   - writes a summary + recommendations for anything manual/approval-required

This keeps ops behavior predictable and auditable.

---

## 2) Inputs and Outputs

### Inputs

- `app_id` + current profile (`starter|scale|enterprise`)
- metric snapshot (the 5 dashboards):
  - `active_websocket_connections`
  - `concurrent_workflows`
  - `http_5xx_rate`
  - `p95_start_session_ms` and `p95_mongo_ms`
  - `token_spend_usd_1h` and `storage_gb`
- Hosting Operator policy (per profile thresholds + actions)

### Outputs

- actions to execute (tool calls) when allowed
- a short operator report:
  - what fired (metric -> severity)
  - what changed automatically (and why)
  - what needs human approval (and why)
  - what to do next (upgrade/isolate/index/rollback)

---

## 3) Required Tools (tooling contract)

The agent should have **read tools** (safe) and **write tools** (scoped, auditable).

### 3.1 Read tools (required)

1) `platform.list_apps() -> [{ app_id, profile, env, azure_resource_id, repo_url }]`
2) `platform.get_app_state(app_id) -> { profile, env, is_deploy_blocked, last_actions[] }`
3) `metrics.get_snapshot(app_id, window_s) -> { metrics: {...}, window_s, sampled_at }`
4) `policy.get_policy(policy_id|profile) -> { ... }`
5) `operator.evaluate(policy, snapshot) -> { decisions[], actions[] }`
   - Implementation can be a direct call into `backend/core/hosting_operator.py:evaluate_snapshot`.

### 3.2 Write tools (required)

These tools execute the action kinds used in `docs/HostingOperatorPolicy.example.json`:

**Platform actions**

- `platform.notify(app_id, severity, title, message, metadata?)`
- `platform.block_deploy(app_id, reason)`
- `platform.unblock_deploy(app_id)`
- `platform.require_upgrade(app_id, to_profile, reason)` (creates an upgrade prompt/task; does not auto-bill)
- `platform.record_action(app_id, kind, payload, outcome, correlation_id)`

**Runtime config actions (Azure App Settings)**

- `runtime.set_env(app_id, env: { KEY: VALUE }, reason)`  
  (updates Azure App Settings; triggers restart if needed)

**Azure actions**

- `azure.scale_out(app_id, target_instances, reason)`
- `azure.scale_up(app_id, target_sku, reason)`
- `azure.move_to_dedicated_plan(app_id, target_plan_id, reason)`
- `azure.rollback(app_id, target_version|last_known_good, reason)` *(should be approval-gated)*

### 3.3 Guardrails the tool layer MUST enforce

Even if the LLM “asks”, the tool layer must block unsafe actions:

- **cooldowns** (per action kind) + “last action” state per `app_id`
- **max limits** per profile (e.g., max instances)
- **budget caps** (token spend, infra spend)
- **approval-required** actions cannot execute without a human approval flag (rollback, large scale-up, plan move)
- **idempotency**: repeated calls should not flap scaling up/down

---

## 4) Prompt Pack

You can implement this agent as a single prompt with tool use, or a small multi-step loop.

### 4.1 System Prompt (agent identity)

```
You are the Mozaiks Hosting Monitor Agent.

Your job is to monitor app/platform health per app_id, evaluate a pre-defined Hosting Operator policy, and take only policy-allowed actions using tools.

Hard rules:
- Never invent actions. Only execute actions returned by the policy evaluator.
- If an action is marked approval_required, you must not execute it. Create a human-facing recommendation instead.
- Prefer stability: avoid flapping. Respect cooldowns and max limits.
- Never suggest editing Dockerfiles or CI/CD for an alert unless the playbook explicitly says it is required.
- Always produce a short operator report: what happened, what changed, what needs approval, and next steps.
```

### 4.2 Developer Prompt (workflow)

```
Workflow:
1) Determine scope: Platform vs a specific customer app_id.
2) Fetch current app state (profile, deploy gate, last actions).
3) Fetch a metrics snapshot for the last N seconds/minutes (window provided).
4) Evaluate policy against the snapshot.
5) For each action:
   - If execution=auto: call the matching tool.
   - If execution=manual or approval_required: do not execute; add to recommendations.
6) Record actions taken with correlation_id.
7) Output a concise report and list any blocked items.
```

### 4.3 User Prompt Template (per monitoring cycle)

```
Monitor these apps: {app_ids or "all"}.
Environment: {env}.
Metrics window: {window_s}.
Policy: {policy_id or "default"}.

If anything is critical, prioritize stability and user impact.
Return a report and take allowed auto-actions.
```

---

## 5) Action Mapping (policy kind -> tool)

This is the “translation layer” between policy and reality:

- `platform.notify` -> `platform.notify(...)`
- `platform.block_deploy` -> `platform.block_deploy(...)`
- `platform.require_upgrade` -> `platform.require_upgrade(...)`
- `runtime.set_env` -> `runtime.set_env(...)`
- `azure.scale_out` -> `azure.scale_out(...)`
- `azure.scale_up` -> `azure.scale_up(...)`
- `azure.move_to_dedicated_plan` -> `azure.move_to_dedicated_plan(...)`
- `azure.rollback` -> recommendation only unless approved -> `azure.rollback(...)`

---

## 6) Example “monitor loop” (high level)

1) `apps = platform.list_apps()`
2) For each `app`:
   - `state = platform.get_app_state(app.app_id)`
   - `snapshot = metrics.get_snapshot(app.app_id, window_s=60)`
   - `result = operator.evaluate(policy, { app_id, profile: state.profile, metrics: snapshot.metrics })`
   - execute `result.actions` where `execution=auto`
   - notify + recommend for manual/approval-required actions

---

## 7) Where does this touch MozaiksCore?

Usually: it doesn’t.

The monitor agent typically changes:
- **Azure scale/app settings** (instances, plan size, env vars), and
- **platform state** (profile, deploy gates, upgrade prompts)

MozaiksCore code is only changed when you add new runtime capabilities (e.g. new endpoints or internal limits), not for day-to-day ops alerts.

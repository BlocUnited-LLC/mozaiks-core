# Unified UI Tool & Design Guide

This single document supersedes and replaces the following prior files (now deprecated and slated for removal):

- UI_COMPONENT_DESIGN_GUIDE.md
- app_THEME_MANAGEMENT.md
- AUTO_TOOL_COMPLETE_FLOW.md
- AUTO_TOOL_PAYLOAD_FLATTENING.md
- AUTO_TOOL_WORKFLOW_DISCOVERY_FIX.md
- interactive_ui_tools/AI_AGENT_UI_TOOL_CREATION_GUIDE.md
- interactive_ui_tools/MASTER_UI_TOOL_AGENT_PROMPT.md

> All prior docs are consolidated here to give the UIFileGenerator / Tool Generator agent ONE authoritative prompt context.

---
## 1. Mission & Scope

Author interactive UI tools (Python async tool + React component) that integrate with MozaiksAI runtime:
- Emit UI events through `emit_ui_tool_event` / await `wait_for_ui_tool_response`.
- Render components using the shared design system.
- Support auto-tool structured output flow (flattening + discovery) without extra per-tool logic.
- Respect app theming without breaking multi-tenant isolation.

---
## 2. High-Level Lifecycle
```
Agent (auto_tool_mode?)
  → Structured output (JSON / model)
      → Orchestration intercept (extract message + structured blob)
          → structured_output_ready event
              → auto_tool_handler validates + emits observability tool_call (no display)
                  → Invokes bound tool function (if defined)
                      → Tool uses emit_ui_tool_event(display=inline|artifact)
                          → Frontend receives chat.tool_call (with display)
                              → ChatPage flattens tool_args (if auto_tool) & filters
                                  → dynamicUIHandler decides render vs skip
                                      → WorkflowUIRouter loads component by workflow_name
                                          → Component renders, user interacts, calls onResponse
                                              → Backend resumes awaiting tool response
```

---
## 3. Hard Non-Negotiable Rules
1. Exactly two files per tool: `{tool_name}.py`, `{tool_name}.js`.
2. Python imports only what it needs plus:
   `from core.workflow.ui_tools import emit_ui_tool_event, wait_for_ui_tool_response`
3. Python emits event then awaits response; must handle cancellation.
4. `tool_id` passed to `emit_ui_tool_event` == React component name (PascalCase allowed; registry maps snake_case id via metadata).
5. React component exports both the component and `componentMetadata` with: `name` (snake_case id), `type` (inline|artifact), `pythonTool` (dotted path), optional capabilities.
6. No secrets echoed. Mask sensitive input fields.
7. Deterministic output—no TODOs, placeholders, commented-out dead code.
8. Use design system tokens (see Section 6) – no duplicated Tailwind utility strings for standard patterns.
9. One final `onResponse` invocation (unless cancelled). Cancel path must exist for multi-step or blocking flows.
10. Tool names must include a UI trigger keyword (input, form, confirm, upload, editor, viewer, artifact, document, select, table, chart).

---
## 4. Component Type Decision
Use `inline` for single-step, low-complexity, ephemeral interactions (≤ ~1 screen). Use `artifact` for multi-phase, high information density, revisitable outputs (plans, editors, visualization dashboards, code generation review, data tables, complex forms). When unsure default to `inline`.

Decision cheat sheet:
| Need | Type |
|------|------|
| Single text / password / API key | inline |
| Confirmation / binary decision | inline |
| ≤5 simple form fields | inline |
| Multi-step wizard | artifact |
| Code or large JSON editing | artifact |
| Data table / chart / visualization | artifact |
| Hierarchical workflow plan | artifact |

---
## 5. Python Tool Template (Authoritative)
```python
from typing import Optional, Dict, Any
from core.workflow.ui_tools import emit_ui_tool_event, wait_for_ui_tool_response

TOOL_NAME = "api_key_input"  # snake_case id

async def api_key_input(chat_id: Optional[str] = None, workflow_name: str = "unknown", service: str = "openai") -> Dict[str, Any]:
    """Collect an API key for a target external service (e.g., OpenAI)."""
    payload = {
        "title": f"Connect {service.title()} API",
        "description": f"Enter your {service.title()} credentials.",
        "fields": [
            {"name": "api_key", "label": f"{service.upper()} API Key", "type": "password", "required": True, "placeholder": "sk-..."},
        ],
        "metadata": {"tool_name": TOOL_NAME, "service": service},
    }

    event_id = await emit_ui_tool_event(
        tool_id="ApiKeyInput",  # React component name
        payload=payload,
        display="inline",
        chat_id=chat_id,
        workflow_name=workflow_name,
    )

    response = await wait_for_ui_tool_response(event_id)
    if response.get("cancelled"):
        raise ValueError("User cancelled API key input")

    key = (response.get("data") or {}).get("api_key") or response.get("api_key")
    if not key:
        raise ValueError("Missing api_key in response")

    return {"service": service, "api_key": key, "status": "success"}
```

---
## 6. React Component Template (Authoritative)
```javascript
import React, { useState } from 'react';
import { typography, components, spacing, layouts } from '../../../styles/artifactDesignSystem';
import { createToolsLogger } from '../../../core/toolsLogger';

const ApiKeyInput = ({ payload = {}, onResponse, onCancel, ui_tool_id, eventId, workflowName }) => {
  const tlog = createToolsLogger({ tool: ui_tool_id, eventId, workflowName });
  const fields = payload.fields || [];
  const [form, setForm] = useState(() => Object.fromEntries(fields.map(f => [f.name, ''])));
  const [submitting, setSubmitting] = useState(false);
  const disabled = submitting || fields.some(f => f.required && !form[f.name].trim());

  const submit = async () => {
    if (disabled) return; setSubmitting(true); tlog.event('submit','start');
    try {
      await onResponse({ status: 'success', data: { ...form }, ui_tool_id, eventId });
      tlog.event('submit','done');
    } catch (e) { tlog.error('submit_fail',{ err: e?.message }); }
    finally { setSubmitting(false); }
  };

  return (
    <div className={layouts.artifactContainer} data-agent-message-id={payload.agent_message_id || undefined}>
      <header className={components.card.primary}>
        <div className="flex items-center justify-between">
          <h2 className={typography.heading.lg}>{payload.title || 'Enter Credentials'}</h2>
        </div>
        {payload.description && <p className={typography.body.sm + ' mt-2'}>{payload.description}</p>}
      </header>
      <div className={spacing.section}>
        <div className={components.card.secondary + ' space-y-4'}>
          {fields.map(field => (
            <div key={field.name} className={spacing.group}>
              <label className={typography.label.md}>{field.label}</label>
              <input
                type={field.type === 'password' ? 'password' : 'text'}
                value={form[field.name]}
                placeholder={field.placeholder}
                onChange={e => setForm(f => ({ ...f, [field.name]: e.target.value }))}
                className="w-full rounded-md bg-slate-800 border border-slate-600 px-3 py-2 text-slate-100 focus:outline-none focus:ring-2 focus:ring-cyan-400"
              />
            </div>
          ))}
          <div className="flex gap-3 pt-2">
            <button disabled={disabled} onClick={submit} className={components.button.primary}>{submitting ? 'Saving…' : 'Save Key'}</button>
            <button onClick={() => { tlog.event('cancel','user'); onCancel?.(); }} className={components.button.ghost}>Cancel</button>
          </div>
        </div>
      </div>
    </div>
  );
};

export const componentMetadata = {
  name: 'api_key_input',
  type: 'inline',
  pythonTool: 'tools.ui_tools.api_key_input.api_key_input',
  version: '1.0.0'
};

export default ApiKeyInput;
```

---
## 7. Design System Essentials
(Condensed from prior design guide)

| Category | Use | Tokens |
|----------|-----|--------|
| Typography | Headings | `typography.heading.*` |
| Typography | Display/Hero | `typography.display.*` |
| Typography | Body | `typography.body.*` |
| Typography | Labels/Badges | `typography.label.*` |
| Layout | Artifact Container | `layouts.artifactContainer` |
| Cards | Primary / Secondary / Ghost | `components.card.primary|secondary|ghost` |
| Buttons | Primary / Secondary / Ghost | `components.button.*` |
| Badges | Status / Primary | `components.badge.*` |
| Icons | Context ring | `components.iconContainer.*` |
| Spacing | Vertical rhythm | `spacing.section|subsection|group` |
| Padding | Inner padding | `spacing.padding.*` |
| Gaps | Flex/grid spacing | `spacing.gap.*` |

Rules:
- NEVER inline raw font families; rely on tokens.
- Extend the design system file if a pattern is missing.
- Keep React DOM minimal; let CSS classes handle shape/spacing.

---
## 8. Auto-Tool Structured Output Flattening (Frontend)
Auto-tool events with `interaction_type: "auto_tool"` and a nested model key are transformed:

Backend example:
```json
{
  "interaction_type": "auto_tool",
  "tool_args": {
    "ActionPlan": { "workflow": { "name": "Marketing", "phases": [] }, "description": "..." },
    "agent_message": "Review this plan"
  },
  "component_type": "ActionPlan"
}
```
Flattening logic (ChatPage.js):
1. Detect nested key matching `component_type`.
2. Promote its object contents to top level.
3. Merge sibling fields (`agent_message`, etc.).
4. Set `payload.tool_name`, `component_type`, preserve `workflow_name`.

Resulting payload delivered to component:
```json
{
  "workflow": { "name": "Marketing", "phases": [] },
  "description": "...",
  "agent_message": "Review this plan",
  "component_type": "ActionPlan"
}
```

If no nested key → shallow flatten of `tool_args` only.

---
## 9. Dual Event Emission (Why Two Events?)
**Event 1 (Observability)**: auto_tool_handler emits a `chat.tool_call` WITHOUT `display` for logging / audit. Skipped by UI.
**Event 2 (UI Tool)**: Tool function emits `chat.tool_call` WITH `display` (inline|artifact) – this renders.
Skip condition in frontend:
```javascript
if (payload?.interaction_type === 'auto_tool' && !display) return true; // skip
```

---
## 10. Workflow Component Discovery
Use `payload.workflow_name` (source workflow) for module resolution. Do NOT use `payload.workflow.name` (user-generated metadata). Fix included in WorkflowUIRouter to avoid module lookup failures.

---
## 11. App Theming Integration (Runtime Summary)
- Themes stored in `Themes` collection, keyed by app id.
- GET `/api/themes/{app_id}` returns merged theme (custom overrides + defaults).
- Frontend `themeProvider.js` loads and injects fonts + CSS vars.
- Missing sections fallback automatically; never assume full override.
- Theme metadata: fonts, colors.primary/background, shadows, branding.
- Safe update contract: PUT partial `{ theme: { colors: { primary: { main: "#0ea5e9" } } } }` – server merges & validates.

Agent-generated components: rely on design tokens, not direct theme colors, so theming remains stable.

---
## 12. Naming & Trigger Keywords
Include at least one trigger for detection:
- Inline: input, confirm, select, upload, download, form
- Artifact: editor, viewer, artifact, document, table, chart, planner

Examples: `file_upload_input`, `code_editor_artifact`, `workflow_plan_viewer_artifact`.

---
## 13. Response & Validation Patterns
Minimal success response:
```json
{ "status": "success", "data": { /* fields */ }, "ui_tool_id": "api_key_input", "eventId": "evt_123" }
```
Error:
```json
{ "status": "error", "error": "Reason", "data": { } }
```
Cancelled:
```json
{ "status": "cancelled", "reason": "user_cancel" }
```

---
## 14. Observability Hooks
Use `createToolsLogger({ tool: ui_tool_id, eventId, workflowName })` and log lifecycle:
- `tlog.event('submit','start')`
- `tlog.event('submit','done')`
- `tlog.error('submit_fail', { err })`
Ensure correlation fields (tool, eventId, workflowName) present.

---
## 15. Low Balance / Paused Session Awareness
If a session is paused (low balance) backend will not dispatch new tool events. Components should remain idle; no polling. Resume occurs via chat restart.

---
## 16. Testing Checklist (Generation Self-Audit)
| Check | Pass Criteria |
|-------|---------------|
| Tool name triggers UI | Contains keyword |
| Python emits & awaits | Uses emit / wait pattern |
| Cancellation path | Cancel button or error branch |
| Single onResponse | Exactly one success path |
| Design tokens used | Imports typography/components/spacing/layouts |
| No raw Tailwind clones | Standard tokens only |
| Flattening safe | No dependency on nested model key existence |
| workflow_name usage | Module load uses source workflow_name |
| Metadata export | componentMetadata complete |
| Mask secrets | Password fields not echoed |

---
## 17. Deprecation Map
| Old File | Status | Integrated Section |
|----------|--------|--------------------|
| UI_COMPONENT_DESIGN_GUIDE.md | deprecated | §7 Design System Essentials |
| app_THEME_MANAGEMENT.md | deprecated | §11 Theming Integration |
| AUTO_TOOL_COMPLETE_FLOW.md | deprecated | §§8–10 Flow/Events/Discovery |
| AUTO_TOOL_PAYLOAD_FLATTENING.md | deprecated | §8 Flattening |
| AUTO_TOOL_WORKFLOW_DISCOVERY_FIX.md | deprecated | §10 Discovery |
| AI_AGENT_UI_TOOL_CREATION_GUIDE.md | deprecated | §§3–6, 12, 16 |
| MASTER_UI_TOOL_AGENT_PROMPT.md | deprecated | §§3–6, 12–16 |

---
## 18. Summary
One doc. One contract. All prior fragmentation removed. Agents: load only this file for full context when generating UI tool pairs.

"""

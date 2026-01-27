# Pattern Context Contract (Simplified Checklist)

Purpose: a minimal contract focusing on the exact fields you requested. Producers should supply only these fields; generators may enrich them (fill `description` and `dependencies`) as needed.

Required producer inputs (minimal):

- `app_id` (string)
- `user_id` (string)
- `workflows` (object): mapping of workflow name → object with:
  - `chat_id` (string|null)
  - `status` (string): one of `not_started`, `in_progress`, `completed`

Optional generator-enriched fields (generators/runtime may fill):

- `description` (string): short human-friendly summary of the workflow's purpose
- `dependencies` (array[string]): list of workflow names this workflow depends on

Minimal live example (producer supplies only the minimal shape):

```json
{
  "app_id": "ent_1",
  "user_id": "u_2",
  "workflows": {
    "Onboarding": { "chat_id": "c_10", "status": "completed" },
    "Reporting": { "chat_id": null, "status": "not_started" }
  }
}
```

Generator-enriched example (AI attaches descriptions + dependencies):

```json
{
  "app_id": "ent_1",
  "user_id": "u_2",
  "workflows": {
    "Onboarding": { "chat_id": "c_10", "status": "completed", "description": "User onboarding sequence", "dependencies": [] },
    "Reporting": { "chat_id": null, "status": "not_started", "description": "Nightly analytics report", "dependencies": ["Onboarding"] }
  }
}
```

Notes:
- This file is intentionally narrow: provide the minimal shape above and allow the generator/AI to infer `description` and `dependencies` as part of its output.
- For richer orchestration (edge gating, conditional start criteria), use `workflow_graph.json` in a pack's `_pack/` folder.
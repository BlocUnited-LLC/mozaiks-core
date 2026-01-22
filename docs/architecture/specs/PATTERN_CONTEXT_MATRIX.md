# Pattern Session-Status Matrix (Minimal)

Keep this intentionally small and runtime-focused: the matrix is based on each workflow's per-user session `status` (e.g., `not_started`, `in_progress`, `completed`) and simple gating rules. It is NOT decision logic for agents — it documents the minimal context producers should supply and the basic routing semantics generators/runtimes can use.

Core shape (per user):

```json
{
	"user_id": "u_123",
	"workflows": {
		"SalesWorkflow": { "chat_id": "c_1", "status": "completed" },
		"SupportWorkflow": { "chat_id": null, "status": "not_started" },
		"AnalyticsWorkflow": { "chat_id": "c_3", "status": "in_progress" }
	}
}
```

Bare rules (apply in order):

- If `X.status == "completed"` and `Y` is gated on `X` → `Y` is eligible to start.
- If `X.status == "in_progress"` and `Y` requires `X` (gating required) → `Y` must wait.
- If `X.status == "in_progress"` and `Y` is optional/independent → `Y` may start in parallel.
- If `X.status == "not_started"` → `X` can be scheduled/started unless upstream `required` gating prevents it.

Quick example (text):

- SalesWorkflow completed → AnalyticsWorkflow (dependent on Sales) may start.
- SupportWorkflow not_started → can be started anytime if independent.
- AnalyticsWorkflow in_progress → do not start AppGenerator if it requires Analytics `completed`.

Producer guidance
- Supply `user_id`, `workflows.{name}.status` and `workflows.{name}.chat_id` when available.
- Keep statuses limited to `not_started`, `in_progress`, `completed` (simple canonical set).

Maintenance
- Use this file for the minimal routing contract. If you later need more complex gating (e.g., partial results), move to a richer pack graph with explicit edge gating metadata in `workflow_graph.json`.
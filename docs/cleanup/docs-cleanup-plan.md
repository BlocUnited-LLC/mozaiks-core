# Docs Cleanup Plan (Guardrailed by Platform Must-Keep)

## Purpose

Make mozaiks-core documentation consistent, accurate, and easy to navigate while preserving the platform's sources of truth.

## Guardrails (Must-Keep / Authoritative)

These paths are authoritative for platform behavior and must remain stable. If any path changes, notify platform immediately.

- docs/ai-runtime/mozaikscore-integration.md
- docs/ai-runtime/mozaikscore-build-deploy-runtime-contract.md
- docs/ai-runtime/runtime-overview.md
- docs/ai-runtime/event-pipeline.md
- docs/ai-runtime/transport-and-streaming.md
- docs/ai-runtime/persistence-and-resume.md
- docs/ai-runtime/token-management.md
- docs/ai-runtime/configuration-reference.md
- docs/frontend/workflow-integration.md
- docs/frontend/ui-components.md
- docs/contracts/runtime-platform-contract-v1.md
- docs/contracts/WORKFLOW_LIFECYCLE_MARKERS_RESPONSE.md
- docs/core/plugins.md

Reference-only (keep, not contract-critical):

- docs/ai-runtime/lifecycle-tools.md
- docs/ai-runtime/synthetic-events.md
- docs/ai-runtime/observability.md
- docs/ai-runtime/architecture.md

Roadmap watchlist (keep discoverable):

- docs/roadmap/validationengine-specification.md

## Classification Goals

Every doc should clearly belong to one of:

1. Authoritative (contracts + runtime behavior)
2. Reference (supporting detail, non-contract)
3. Guides (how-to, onboarding)
4. Roadmap (future work)
5. Archive (historical / deprecated)

## Phased Plan

### Phase 0: Inventory + Index (short)
- Create a single docs index mapping topics -> canonical files.
- Tag docs with status: authoritative / reference / guide / roadmap / archive.
- Identify duplicates and stale content (no edits yet).

### Phase 1: Accuracy Pass (medium)
- Align core paths in guides (plugins/workflows, runtime layout).
- Remove or update stale paths and commands in onboarding guides.
- Normalize CLI references and paths.

### Phase 2: Consolidation (medium)
- Merge overlapping docs into a single canonical guide per topic.
- Move superseded docs to docs/archive with a short pointer.
- Keep "must-keep" docs stable; update content carefully if needed.

### Phase 3: Maintenance Policy (long)
- Add doc ownership and update cadence.
- Require a short doc impact note in PRs that change runtime behavior.

## Known Inconsistencies (to fix in Phase 1)

- Plugin directory references vary (plugins/ vs runtime/ai/plugins).
- Quickstart references runtime/backend paths that may be legacy.
- CLI docs and contract docs show different output paths.

## Output

- docs/index.md (canonical entrypoint for navigation)
- Updated guides with consistent runtime paths
- Archived legacy docs with clear pointers

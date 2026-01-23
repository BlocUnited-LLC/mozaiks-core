# Strategic Architecture Index

> **Purpose:** Master index linking all strategic architecture documents for the Mozaiks platform restructuring.
> **Date:** 2026-01-19
> **Status:** Living Document

---

## Overview

This folder contains the strategic documentation for restructuring Mozaiks from a monolithic control-plane into a two-layer architecture:

1. **mozaiks-core** - Open source runtime and core services
2. **mozaiks-platform** - Proprietary platform features

---

## Document Index

### Architecture Documents

| Document | Purpose | Location |
|----------|---------|----------|
| **Plugin Architecture** | Defines "Plugins as Feature Modules" (Vertical Slices) | [PLUGIN_ARCHITECTURE.md](architecture/PLUGIN_ARCHITECTURE.md) |
| **Control Plane Separation** | Defines Core vs Platform service split | [CONTROL_PLANE_SEPARATION.md](architecture/CONTROL_PLANE_SEPARATION.md) |
| **Optimization Loop** | A/B testing, canary releases, experiment tracking | [OPTIMIZATION_LOOP_SPECIFICATION.md](OPTIMIZATION_LOOP_SPECIFICATION.md) |

### Strategy Documents

| Document | Purpose | Location |
|----------|---------|----------|
| **Value Proposition & Flywheel** | Business strategy, moat, and competitive positioning | [VALUE_PROPOSITION_FLYWHEEL.md](strategy/VALUE_PROPOSITION_FLYWHEEL.md) |

### Migration Documents

| Document | Purpose | Location |
|----------|---------|----------|
| **Technical Gaps & Resolutions** | ⚠️ CRITICAL: Pre-migration blockers and solutions | [TECHNICAL_GAPS_RESOLUTIONS.md](migration/TECHNICAL_GAPS_RESOLUTIONS.md) |
| **Core Migration Plan** | Detailed execution plan for repo restructuring | [CORE_MIGRATION_PLAN.md](migration/CORE_MIGRATION_PLAN.md) |
| **MOZ-UI Migration Plan** | UI migration from monolith to plugin-based architecture | [MOZ_UI_MIGRATION_PLAN.md](migration/MOZ_UI_MIGRATION_PLAN.md) |

---

## Quick Reference

### The Two Repositories

```
┌─────────────────────────────────────┐    ┌─────────────────────────────────────┐
│         mozaiks-core                 │    │       mozaiks-platform              │
│         (Open Source)               │    │       (Proprietary)                 │
├─────────────────────────────────────┤    ├─────────────────────────────────────┤
│                                     │    │                                     │
│  runtime/                           │    │  Hosting.API                        │
│    ├── shell (UI)                   │    │  Funding.API                        │
│    ├── sdk                          │    │  Growth.API                         │
│    ├── server                       │    │  Discovery.API                      │
│    └── cli                          │    │  Community.API                      │
│                                     │    │                                     │
│  ai/                                │    │  ai-models/                         │
│    ├── core (agent)                 │    │    ├── trained models               │
│    ├── tools                        │    │    ├── curated packs                │
│    └── ui                           │    │    └── quality validators           │
│                                     │    │                                     │
│  backend/                           │    │  plugins/                           │
│    ├── Identity.API                 │    │    ├── moz.platform.hosting         │
│    ├── Billing.API                  │    │    ├── moz.platform.funding         │
│    ├── Insights.API                 │    │    ├── moz.platform.growth          │
│    └── Plugins.API                  │    │    └── moz.platform.discovery       │
│                                     │    │                                     │
└─────────────────────────────────────┘    └─────────────────────────────────────┘
```

### Service Classification

| Service | Layer | License | Self-Hostable |
|---------|-------|---------|---------------|
| Identity.API | Core | MIT | ✅ Yes |
| Billing.API | Core | MIT | ✅ Yes |
| Insights.API | Core | MIT | ✅ Yes |
| Plugins.API | Core | MIT | ✅ Yes |
| AI Runtime | Core | MIT | ✅ Yes (BYOLLM) |
| Hosting.API | Platform | Proprietary | ❌ No |
| Provisioning.Agent | Platform | Proprietary | ❌ No |
| Funding.API | Platform | Proprietary | ❌ No |
| Growth.API | Platform | Proprietary | ❌ No |
| Discovery.API | Platform | Proprietary | ❌ No |
| Community.API | Platform | Proprietary | ❌ No |
| Trained Models/Packs | Platform | Proprietary | ❌ No |

### The Value Prop Stack

| Layer | Type | Examples |
|-------|------|----------|
| **Commodity** | Open Source | Auth, Billing, Telemetry, Plugins, AI Runtime |
| **Moat** | Proprietary | Trained Models/Packs, Hosting, Funding, Discovery, MozaiksPay |

### The Flywheel

```
AI generates apps → More apps in marketplace → More users discover apps
                                             → More investors join
                                             → More funding available
                                             → Founders attracted to build
                                             → AI generates more apps (repeat)
```

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-01-19 | Plugin = Feature Module | Pages alone are too limited; need backend + UI + nav |
| 2026-01-19 | Two-layer architecture | Enables self-hosting + keeps moat proprietary |
| 2026-01-19 | Combined OSS repo | Better DX than separate runtime/backend repos |
| 2026-01-19 | MIT license for core | Maximizes adoption and community contribution |
| 2026-01-19 | Optimization Loop | A/B testing + experiments enable evolving app generator |
| 2026-01-19 | AI runtime is OSS | Everyone gets AI capabilities; differentiator is trained models/packs |
| 2026-01-19 | MOZ-UI → Plugin Architecture | Migrate monolith UI to plugin-based shell + platform plugins |

---

## Next Steps

### Backend Migration
1. [ ] Review all documents with stakeholders
2. [ ] Create `mozaiks-core` repository
3. [ ] Create `mozaiks-platform` repository
4. [ ] Begin Phase 1 of backend migration (Week 1)
5. [ ] Update CI/CD pipelines

### UI Migration
6. [ ] Create shell + SDK in mozaiks-core/runtime/
7. [ ] Migrate shared components to SDK
8. [ ] Create platform plugins from MOZ-UI pages
9. [ ] Test plugin loading in shell
10. [ ] Remove legacy MOZ-UI code

### Launch
11. [ ] Announce to community (after all phases complete)

---

## Related Documents

- [AGENTS.md](../AGENTS.md) - AI agent context for the control-plane
- [current_architecture.md](current_architecture.md) - Current system state
- [source_of_truth/CONTROL_PLANE_AUTHORITY.md](source_of_truth/CONTROL_PLANE_AUTHORITY.md) - Authority boundaries

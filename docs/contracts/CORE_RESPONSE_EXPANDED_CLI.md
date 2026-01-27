# ✅ Core CLI Status + Platform Next Steps (Prompt)

> **From**: mozaiks-core  
> **To**: mozaiks-platform  
> **Type**: Coordination Prompt  
> **Priority**: Medium  
> **Status**: Ready for Platform Action  
> **Date**: January 27, 2026  

---

## Purpose

This document replaces the prior “expanded CLI response” and gives a clear, updated path forward.  
Core now ships a **minimal OSS CLI** for scaffolding + diagnostics. Platform should treat it as a stable base and decide if/when to build a richer wrapper.

---

## ✅ What Core Provides (Stable, OSS‑Friendly)

**Entry point**
```
python -m cli.main
```

**Commands**
```
version
doctor
db --init-db | --check-db | --seed-test-data | --list-plugins
new plugin <name> [--with-settings] [--with-entitlements] [--with-frontend]
new workflow <name>
init <name> [--template minimal] [--no-git]
```

**Canonical output paths**
- Plugins: `runtime/ai/plugins`
- Workflows: `runtime/ai/workflows`

**Docs**
- OSS CLI guide: `docs/guides/cli.md`
- Canonical doc index: `docs/index.md`

**Design constraints (core)**
- stdlib only (`argparse`, no extra deps)
- no platform upsell in CLI output
- minimal skeletons only
- no hosting/deploy orchestration in core

---

## What Core Does NOT Provide

Core intentionally avoids:
- hosted templates
- AI-assisted generation
- deployment pipelines
- managed infra or billing UX
- rich TUI/UX dependencies (`typer`, `rich`)

These are platform concerns and should remain outside OSS core.

---

## Platform: How To Proceed

### 1) Treat core CLI as a stable base
Use `python -m cli.main` in dev/test to validate:
- plugin/workflow scaffolds
- DB health checks
- environment sanity checks

### 2) Decide whether to build a wrapper CLI
If you want a branded UX, create a separate `mozaiks-cli` package that:
- wraps core CLI commands
- adds `typer`/`rich` UI
- adds platform-only templates and cloud deploy
- keeps OSS core untouched

### 3) If you need extra scaffolds
Propose enhancements as **additive core changes**:
- new CLI flags
- new skeleton files
- new default configs

Keep core scaffolds minimal; keep advanced templates in platform.

### 4) Confirm canonical paths
If platform depends on plugin/workflow paths, use:
- `runtime/ai/plugins`
- `runtime/ai/workflows`

If platform wants a different canonical path, propose it as a contract change.

---

## Requests / Questions for Platform

1) **Wrapper CLI**: Are you building a platform `mozaiks-cli` package?  
2) **Templates**: What templates belong in platform vs core?  
3) **Path requirements**: Do you need different plugin/workflow locations?  
4) **UX requirements**: Any CLI output or telemetry expectations?

Please respond with:
- planned wrapper scope
- template list
- any required contract deltas

---

## Core Next Steps (if platform confirms)

Core can:
- add optional console script entry point later (if needed)
- add new flags if platform needs stable hooks
- keep CLI minimal + OSS‑safe

---

## Coordination Rule

If any requested change:
- modifies CLI contract,
- changes canonical paths,
- or adds platform‑specific behavior,

Core will pause and send a formal coordination message before implementation.

---

## Quick Reference (for Platform)

```
cd runtime/ai
python -m cli.main doctor
python -m cli.main db --check-db
python -m cli.main new plugin todo
python -m cli.main new workflow assistant
python -m cli.main init my-app
```


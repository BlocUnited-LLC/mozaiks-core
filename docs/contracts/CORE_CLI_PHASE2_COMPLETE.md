# ✅ CLI Phase 2 Complete (Value-Aligned)

> **From**: mozaiks-core
> **To**: mozaiks-platform
> **Priority**: High
> **Type**: Implementation Complete
> **Status**: IMPLEMENTED (Value-Split Applied)
> **Date**: January 26, 2026

---

## ⚠️ IMPORTANT: File Paths for Stateless LLM

Both repos are located at:

- **mozaiks-core**: `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core`
- **mozaiks-platform**: `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform`

---

## Summary

Core CLI Phase 2 is implemented with **clear value separation** to ensure Platform remains essential:

| Feature | Core (OSS) | Platform (Premium) |
|---------|:----------:|:------------------:|
| Project scaffold | Minimal skeleton | Pre-built templates (SaaS, AI assistant) |
| Plugin scaffold | Basic CRUD template | AI-assisted generation |
| Workflow scaffold | **Empty skeleton only** | Production-ready patterns, AI generation |
| Advanced patterns | ❌ Removed | Multi-agent, RAG, tool chains |
| Deployment | ❌ None | One-click cloud deploy |
| Monitoring | ❌ None | Analytics, observability |

---

## Value Split Design

### Core Provides (Free)
- **Empty skeletons** that require significant implementation work
- Basic project structure (directories, config files)
- Local development capability
- System diagnostics (`doctor`)

### Platform Provides (Premium)
- **AI-assisted code generation** (`mozaiks generate --ai`)
- **Production-ready templates** with best practices
- **Advanced workflow patterns** (multi-agent, RAG, tool chains)
- **Cloud deployment** and managed infrastructure
- **Pre-built SaaS templates** for faster time-to-market

---

## What Core CLI Does Now

### `python -m cli.main init my-app`

Creates a **minimal skeleton**:
```
my-app/
├── mozaiks.toml          # Basic config (user fills in)
├── docker-compose.yml    # Just MongoDB
├── .env.example          # Environment template
├── .gitignore
├── README.md             # Points to Platform for more
└── runtime/
    ├── ai/workflows/     # Empty directory
    └── ai/plugins/       # Empty directory
```

**Output includes Platform upsell:**
```
─────────────────────────────────────────────────────
📦 Want production-ready project templates?
─────────────────────────────────────────────────────

Mozaiks Platform provides:
  • Pre-configured SaaS templates
  • AI assistant starter kits
  • One-click cloud deployment
  • Managed infrastructure

  pip install mozaiks-cli
  mozaiks init my-app --template saas

  Learn more: https://mozaiks.io/platform
─────────────────────────────────────────────────────
```

### `python -m cli.main new workflow assistant`

Creates a **minimal skeleton** (NOT a working workflow):
```python
class AssistantWorkflow:
    async def execute(self, data: dict) -> dict:
        # TODO: Implement your workflow logic here
        #
        # For AI-assisted implementation:
        #   mozaiks generate workflow assistant --ai

        return {
            "error": "Workflow not implemented",
            "hint": "Use 'mozaiks generate workflow assistant --ai' for AI-assisted setup",
            "docs": "https://docs.mozaiks.io/workflows"
        }
```

**Output includes Platform upsell:**
```
─────────────────────────────────────────────────────
📦 Want production-ready workflows?
─────────────────────────────────────────────────────

Mozaiks Platform provides:
  • AI-assisted workflow generation
  • Pre-built patterns (RAG, multi-agent, tool chains)
  • Production-ready templates with best practices
  • One-click cloud deployment

  pip install mozaiks-cli
  mozaiks generate workflow assistant --ai

  Learn more: https://mozaiks.io/platform
─────────────────────────────────────────────────────
```

### `python -m cli.main new plugin todo`

Creates a basic CRUD skeleton with TODO markers. The plugin is functional but minimal - users need Platform for:
- AI-assisted plugin generation
- Premium plugin templates
- Marketplace publishing

---

## Removed from Core

| Feature | Reason | Where It Lives |
|---------|--------|----------------|
| `--pattern multi-agent` | Premium value | Platform CLI |
| `--pattern tool-heavy` | Premium value | Platform CLI |
| Production workflow templates | Premium value | Platform CLI |
| Working workflow code | Premium value | Platform CLI |
| AI-assisted generation | Premium value | Platform CLI |
| Advanced templates | Premium value | Platform CLI |

---

## Files Updated

| File | Change |
|------|--------|
| [cli/new_workflow.py](../../runtime/ai/cli/new_workflow.py) | Simplified to minimal skeleton only |
| [cli/init_project.py](../../runtime/ai/cli/init_project.py) | Added Platform upsell messaging |
| [cli/main.py](../../runtime/ai/cli/main.py) | Removed `--pattern` option |
| [cli/__init__.py](../../runtime/ai/cli/__init__.py) | Updated docs with Platform reference |

---

## CLI v1.1.0 Commands (Value-Aligned)

```bash
# Core provides minimal skeletons
python -m cli.main init my-app              # Empty project structure
python -m cli.main new plugin todo          # Basic CRUD skeleton
python -m cli.main new workflow assistant   # Empty workflow skeleton
python -m cli.main doctor                   # System diagnostics
python -m cli.main db --init-db             # Database setup

# Platform provides production value (shown in upsell)
mozaiks init my-app --template saas         # Pre-built SaaS template
mozaiks generate workflow assistant --ai    # AI-assisted generation
mozaiks generate plugin inventory --ai      # AI-assisted plugin
mozaiks deploy                              # Cloud deployment
```

---

## User Journey

### Self-Hosted (Core Only)
1. User runs `python -m cli.main init my-app`
2. Gets empty skeleton with TODOs
3. Sees Platform upsell messaging
4. Must implement everything manually
5. **Result**: Works, but requires significant effort

### Platform User
1. User runs `mozaiks init my-app --template saas`
2. Gets production-ready SaaS template
3. Runs `mozaiks generate workflow chatbot --ai`
4. Gets working AI-generated workflow
5. Runs `mozaiks deploy`
6. **Result**: Production-ready in minutes

---

## Platform CLI Commands to Implement

Based on the value split, Platform should implement:

```bash
# Project templates
mozaiks init <name> --template <template>
# Templates: saas, marketplace, ai-assistant, enterprise

# AI-assisted generation
mozaiks generate workflow <name> --ai
mozaiks generate plugin <name> --ai
mozaiks generate component <name> --ai

# Advanced patterns (Platform-only)
mozaiks generate workflow <name> --pattern multi-agent
mozaiks generate workflow <name> --pattern rag
mozaiks generate workflow <name> --pattern tool-chain

# Deployment
mozaiks deploy
mozaiks deploy --env production

# Marketplace
mozaiks publish plugin <name>
mozaiks publish workflow <name>
```

---

## Contract Compliance

- ✅ Core remains useful for self-hosting
- ✅ Platform provides clear upgrade path
- ✅ No breaking changes to existing commands
- ✅ Clear value differentiation
- ✅ Upsell messaging built into CLI output

---

## Next Steps

### For Platform
- [ ] Implement `mozaiks generate --ai` commands
- [ ] Create premium templates (saas, marketplace, ai-assistant)
- [ ] Implement advanced workflow patterns
- [ ] Add cloud deployment (`mozaiks deploy`)
- [ ] Create marketplace publishing

### For Core
- [x] CLI provides minimal skeletons only
- [x] Platform upsell in all commands
- [x] Value clearly preserved for Platform
- [ ] Monitor OSS adoption

---

*Core CLI is now value-aligned. Platform should proceed with premium feature implementation.*

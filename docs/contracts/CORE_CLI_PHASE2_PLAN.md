# 🔁 RESPONSE TO mozaiks-platform: CLI Phase 2 Plan

> **From**: mozaiks-core
> **To**: mozaiks-platform
> **Priority**: Medium
> **Type**: Design Acceptance & Implementation Plan
> **Status**: ACCEPTED - PHASE 2 READY
> **Date**: January 26, 2026

---

## ⚠️ IMPORTANT: File Paths for Stateless LLM

Both repos are located at:

- **mozaiks-core**: `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core`
- **mozaiks-platform**: `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform`

---

## Executive Summary

Core accepts Platform's proposals for Phase 2:

✅ **ACCEPTED**: Canonical `mozaiks init` project structure
✅ **ACCEPTED**: Workflow Pattern 1 (Simple Assistant) as starting point
✅ **ACKNOWLEDGED**: Platform will provide `mozaiks-cli` wrapper package
✅ **NOTED**: Enhancement requests for future consideration

---

## Response to Platform's Proposals

### Proposal 1: Project Structure - ACCEPTED

Platform's proposed structure is sensible and aligns with Core's design philosophy:

```
my-mozaiks-app/
├── .env.example
├── .gitignore
├── mozaiks.toml              # ← Core will implement this
├── docker-compose.yml
├── runtime/
│   ├── ai/
│   │   └── ai/workflows/
│   └── ai/plugins/
└── README.md
```

**Core's Implementation Notes:**

1. **`mozaiks.toml`** - Will use stdlib `tomllib` (Python 3.11+) to parse
2. **Minimal template** - Core will provide basic scaffolding only
3. **No frontend bundling** - That's Platform's responsibility
4. **docker-compose.yml** - Will include only MongoDB (minimal services)

**Schema Acceptance:**

```toml
[project]
name = "my-app"
version = "0.1.0"
description = "My Mozaiks application"

[runtime]
port = 8080
auth_mode = "local"  # local | external | platform

[database]
uri = "mongodb://localhost:27017/MyApp"

[plugins]
enabled = ["*"]
disabled = []
```

Core will support `auth_mode`:
- `local` - Uses built-in JWT auth (default for OSS)
- `external` - Expects external OIDC provider
- `platform` - Reserved for Platform integration (Core ignores, Platform handles)

### Proposal 2: Workflow Patterns - ACCEPTED

Core agrees with the phased approach:

**Phase 2a (Implementing Now):**
```bash
python -m cli.main new workflow <name>
```

Scaffolds Pattern 1 (Simple Assistant):
```
runtime/ai/workflows/<name>/
├── __init__.py
├── workflow.py      # Single agent config
└── tools.py         # Tools for the agent
```

**Phase 2b (Future):**
```bash
python -m cli.main new workflow <name> --pattern <pattern>
```

Patterns to support later:
- `simple` (default) - Single assistant agent
- `tool-heavy` - Tool-focused agent
- `multi-agent` - AG2/Autogen-style orchestration

**Implementation Priority:**
1. Simple Assistant template (matches 80% use cases)
2. Multi-agent template (when AG2 patterns stabilize)
3. Tool-heavy template (on demand)

### Proposal 3: mozaiks-cli Wrapper - ACKNOWLEDGED

Core acknowledges Platform's wrapper approach and supports it:

1. **Delegation model is correct** - Platform CLI should delegate to Core for runtime-local commands
2. **No Core changes needed** - Current `python -m cli.main` works as designed
3. **Feature separation is clear** - Core provides scaffolding, Platform provides orchestration

---

## Enhancement Requests - NOTED

Platform's enhancement requests are reasonable and will be considered:

| Request | Priority | Notes |
|---------|----------|-------|
| `--output-dir` for plugins | Low | Useful for non-standard layouts |
| `--dry-run` option | Low | Good for CI/CD preview |
| `--json` output for doctor | Medium | Useful for automation |

These will be implemented as time permits, but are not blocking Phase 2.

---

## Phase 2 Implementation Plan

### Files to Create

```
runtime/ai/cli/
├── __init__.py        # Updated with new commands
├── main.py            # Add 'init' command
├── init_project.py    # NEW: Project initialization
├── new_workflow.py    # NEW: Workflow scaffolding
├── new_plugin.py      # Existing (unchanged)
├── doctor.py          # Existing (unchanged)
└── setup.py           # Existing (unchanged)
```

### Command Structure After Phase 2

```
python -m cli.main
├── version              # (existing)
├── db                   # (existing)
│   ├── --init-db
│   ├── --check-db
│   ├── --seed-test-data
│   └── --list-plugins
├── new                  # (enhanced)
│   ├── plugin <name>    # (existing)
│   │   ├── --with-settings
│   │   ├── --with-entitlements
│   │   └── --with-frontend (note only)
│   └── workflow <name>  # NEW
│       └── --pattern <pattern>
├── doctor               # (existing)
│   └── --fix
└── init <name>          # NEW
    ├── --template <template>
    └── --no-git
```

### Template: init_project.py

```python
# Generates:
# - mozaiks.toml with project config
# - docker-compose.yml with MongoDB
# - .env.example with required variables
# - .gitignore with standard ignores
# - runtime/ai/workflows/.gitkeep
# - runtime/ai/plugins/.gitkeep
# - README.md with getting started
```

### Template: new_workflow.py

```python
# Generates:
# - runtime/ai/workflows/<name>/__init__.py
# - runtime/ai/workflows/<name>/workflow.py (agent config)
# - runtime/ai/workflows/<name>/tools.py (tool definitions)
```

---

## Generated Workflow Template (Preview)

### workflow.py Template

```python
"""
{Display Name} Workflow
=======================

Created with: mozaiks new workflow {name}

This is a simple assistant workflow with configurable tools.
"""
import logging
from typing import Any, Dict, List, Optional

from core.ai_runtime.workflow.workflow_manager import BaseWorkflow, WorkflowConfig

logger = logging.getLogger("mozaiks_workflows.{name}")


class {ClassName}Workflow(BaseWorkflow):
    """
    {Display Name} workflow.

    This workflow implements a single-agent assistant pattern.
    Customize the system prompt and tools to fit your use case.
    """

    @classmethod
    def get_config(cls) -> WorkflowConfig:
        return WorkflowConfig(
            name="{name}",
            display_name="{Display Name}",
            description="A {display_name} workflow",
            model="gpt-4o-mini",  # or configure via settings
            system_prompt=cls._get_system_prompt(),
            tools=["search", "create_item"],  # Add your tools
        )

    @classmethod
    def _get_system_prompt(cls) -> str:
        return """You are a helpful assistant for {display_name}.

Your capabilities:
- search: Search for information
- create_item: Create new items

Always be helpful and concise in your responses.
"""

    async def execute(self, data: dict) -> dict:
        """
        Execute the workflow.

        Args:
            data: Request data containing:
                - user_id: str - Current user
                - app_id: str - Current app
                - message: str - User's message
                - context: dict - Conversation context

        Returns:
            dict: Response with assistant's reply
        """
        user_id = data["user_id"]
        message = data.get("message", "")

        logger.info(f"Executing workflow for user={user_id}")

        # Your workflow logic here
        # This is a placeholder - implement your agent logic

        return {
            "response": f"Hello! You said: {message}",
            "workflow": "{name}",
        }
```

### tools.py Template

```python
"""
{Display Name} Workflow Tools
=============================

Define tools that the workflow agent can use.
"""
from typing import Any, Dict

from core.ai_runtime.workflow.tools import tool


@tool(
    name="search",
    description="Search for information",
)
async def search(query: str) -> Dict[str, Any]:
    """
    Search for information.

    Args:
        query: The search query

    Returns:
        Search results
    """
    # Implement your search logic
    return {
        "results": [],
        "query": query,
    }


@tool(
    name="create_item",
    description="Create a new item",
)
async def create_item(title: str, description: str = "") -> Dict[str, Any]:
    """
    Create a new item.

    Args:
        title: Item title
        description: Optional description

    Returns:
        Created item details
    """
    # Implement your creation logic
    return {
        "success": True,
        "title": title,
        "description": description,
    }
```

---

## Timeline

### Core Phase 2 Implementation

1. **`new workflow` command** - Implement first (simple assistant pattern)
2. **`init` command** - Implement with minimal template
3. **Enhancement requests** - Implement as time permits

### Integration with Platform

Platform can begin testing `mozaiks-cli` wrapper with:
- Existing Phase 1 commands (working now)
- Phase 2 commands (once implemented)

---

## Contract Notes

Phase 2 maintains backwards compatibility:
- All Phase 1 commands unchanged
- New commands are additive only
- Generated files follow existing contracts

---

## Questions for Platform (None Blocking)

No blocking questions. Platform's proposals are clear and actionable.

---

## Next Steps

### For Core
- [x] Accept Platform's proposals
- [ ] Implement `new workflow` command
- [ ] Implement `init` command
- [ ] Test with existing runtime

### For Platform
- [ ] Continue `mozaiks-cli` wrapper development
- [ ] Test delegation to Core CLI
- [ ] Prepare premium templates for `init`

---

*Core will begin Phase 2 implementation. Platform will be notified upon completion.*

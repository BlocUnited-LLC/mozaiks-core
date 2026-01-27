#!/usr/bin/env python
"""
Workflow Scaffolding CLI

Creates a minimal workflow skeleton for mozaiks-core.

Usage:
    python -m cli.main new workflow <name>

Contract Version: 1.0
"""

import re
from pathlib import Path


def to_snake_case(name: str) -> str:
    """Convert name to snake_case."""
    s1 = re.sub('(.)([A-Z][a-z]+)', r'\\1_\\2', name)
    s2 = re.sub('([a-z0-9])([A-Z])', r'\\1_\\2', s1)
    s3 = s2.replace('-', '_').replace(' ', '_')
    return s3.lower()


def to_title_case(name: str) -> str:
    """Convert snake_case to Title Case."""
    return ' '.join(word.capitalize() for word in name.split('_'))


def to_class_name(name: str) -> str:
    """Convert snake_case to PascalCase class name."""
    return ''.join(word.capitalize() for word in name.split('_'))


def get_workflows_dir() -> Path:
    """Get the canonical workflows directory (runtime/ai/workflows)."""
    return Path(__file__).parent.parent / "workflows"


def create_workflow(name: str, pattern: str = "simple") -> int:
    """
    Create a minimal workflow skeleton.

    Args:
        name: Workflow name
        pattern: Ignored in Core - all patterns available via Platform

    Returns:
        0 on success, 1 on failure
    """
    workflow_name = to_snake_case(name)
    display_name = to_title_case(workflow_name)
    class_name = to_class_name(workflow_name)

    print(f"\n🔧 Creating workflow skeleton: {workflow_name}\n")

    # Get workflows directory
    workflows_dir = get_workflows_dir()
    workflow_dir = workflows_dir / workflow_name

    # Check if workflow already exists
    if workflow_dir.exists():
        print(f"❌ Workflow '{workflow_name}' already exists at {workflow_dir}")
        return 1

    # Create workflow directory
    print(f"   [1/3] Creating directory: {workflow_dir}")
    workflow_dir.mkdir(parents=True, exist_ok=True)

    # Create __init__.py
    print(f"   [2/3] Creating __init__.py")
    init_content = f'''# {display_name} Workflow
# Skeleton created with: python -m cli.main new workflow {name}
#
# See docs/guides/creating-workflows.md for implementation guidance.
'''
    (workflow_dir / "__init__.py").write_text(init_content)

    # Create minimal workflow.py
    print(f"   [3/3] Creating workflow.py")
    workflow_content = generate_minimal_workflow(workflow_name, display_name, class_name)
    (workflow_dir / "workflow.py").write_text(workflow_content)

    # Success message
    print(f"\n{'=' * 55}")
    print(f"✨ Workflow skeleton '{workflow_name}' created!")
    print(f"{'=' * 55}")
    print(f"\nLocation: {workflow_dir}")
    print(f"\nFiles created:")
    print(f"  - __init__.py")
    print(f"  - workflow.py (minimal skeleton)")

    print(f"\nNext steps:")
    print(f"  1. Edit {workflow_dir / 'workflow.py'} to implement your logic")
    print("  2. See docs/guides/creating-workflows.md")

    return 0


def generate_minimal_workflow(workflow_name: str, display_name: str, class_name: str) -> str:
    """Generate a minimal workflow skeleton - implementation left to user."""
    return f'''"""
{display_name} Workflow
{'=' * len(display_name + ' Workflow')}

Minimal skeleton. Implement your workflow logic below.

Documentation: docs/guides/creating-workflows.md
"""
import logging

logger = logging.getLogger("mozaiks_workflows.{workflow_name}")


class {class_name}Workflow:
    """
    {display_name} workflow.

    TODO: Implement your workflow logic.
    """

    name = "{workflow_name}"
    display_name = "{display_name}"
    version = "0.1.0"

    async def execute(self, data: dict) -> dict:
        """
        Execute the workflow.

        Args:
            data: Request data containing:
                - user_id: str - Current user (injected by runtime)
                - app_id: str - Current app (injected by runtime)
                - message: str - User input
                - context: dict - Additional context

        Returns:
            dict: Workflow response
        """
        user_id = data.get("user_id")
        message = data.get("message", "")

        logger.info(f"Executing {workflow_name} for user={{user_id}}")

        # TODO: Implement your workflow logic here.

        return {{
            "error": "Workflow not implemented",
            "hint": "See docs/guides/creating-workflows.md for guidance",
            "docs": "docs/guides/creating-workflows.md"
        }}


# Export
workflow_class = {class_name}Workflow
'''

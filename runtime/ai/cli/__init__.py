# runtime/ai/cli/__init__.py
"""
Mozaiks Core CLI Tools

Provides minimal scaffolding and diagnostics for mozaiks-core development.

Usage:
    python -m cli.main <command> [options]

Commands:
    init            Initialize a new project (minimal skeleton)
    db              Database operations
    new plugin      Scaffold a plugin skeleton
    new workflow    Scaffold a workflow skeleton
    doctor          Check system requirements
    version         Show version information

Quick Reference:
    python -m cli.main init my-app
    python -m cli.main db --init-db
    python -m cli.main new plugin todo
    python -m cli.main new workflow assistant
    python -m cli.main doctor

Legacy Usage (still works):
    python -m cli.setup --init-db
"""

__version__ = "1.1.0"
__contract_version__ = "1.0"

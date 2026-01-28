from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Dict, Optional

from ..workflow_manager import WORKFLOWS_ROOT


def load_pack_graph(workflow_name: str) -> Optional[Dict[str, Any]]:
    """Load the workflow pack graph configuration."""
    wf = str(workflow_name or "").strip()
    if not wf:
        return None
    path = WORKFLOWS_ROOT / wf / "_pack" / "workflow_graph.json"
    if not path.exists():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None


def workflow_has_nested_chats(workflow_name: str) -> bool:
    cfg = load_pack_graph(workflow_name)
    if not cfg or not isinstance(cfg, dict):
        return False
    nested = cfg.get("nested_chats")
    return isinstance(nested, list) and bool(nested)


__all__ = ["load_pack_graph", "workflow_has_nested_chats"]

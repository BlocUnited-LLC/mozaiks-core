"""
save_concept_blueprint - Persist ValueEngine concept blueprint into Mongo and emit a Blueprint artifact.

This tool:
1) Upserts `autogen_ai_agents.Concepts` (scoped by app_id)
2) Stores `ConceptOverview` (required by AgentGenerator) and optional `ApiEndpoints` + `Blueprint`
3) Emits a non-interactive UI artifact so the user can visualize the "Concept Blueprint"
"""

import uuid
from datetime import UTC, datetime
from typing import Annotated, Any, Dict, List, Optional

from mozaiksai.core.data.persistence.persistence_manager import PersistenceManager
from mozaiksai.core.transport.simple_transport import SimpleTransport
from logs.logging_config import get_workflow_logger


def _sanitize_endpoints(raw: Any) -> List[Dict[str, Any]]:
    if not isinstance(raw, list):
        return []
    out: List[Dict[str, Any]] = []
    for item in raw:
        if not isinstance(item, dict):
            continue
        endpoint: Dict[str, Any] = {}
        for key in ("method", "path", "description", "name"):
            val = item.get(key)
            if isinstance(val, str) and val.strip():
                endpoint[key] = val.strip()
        # Preserve any extra keys (best-effort) but keep payload small
        extras = {k: v for k, v in item.items() if k not in endpoint and k not in {"method", "path", "description", "name"}}
        if extras:
            # Only include a bounded subset to avoid oversized artifacts.
            for k in list(extras.keys())[:6]:
                endpoint[k] = extras[k]
        if endpoint:
            out.append(endpoint)
        if len(out) >= 50:
            break
    return out


async def save_concept_blueprint(
    *,
    concept_overview: Annotated[str, "Concise project overview for downstream workflows"],
    api_endpoints: Annotated[Optional[List[Dict[str, Any]]], "Optional API endpoints blueprint"] = None,
    blueprint: Annotated[Optional[Dict[str, Any]], "Optional structured blueprint object"] = None,
    context_variables: Annotated[Optional[Any], "Injected runtime context (chat_id, app_id, workflow_name, user_id)."] = None,
) -> Dict[str, Any]:
    """Upsert concept blueprint and emit ConceptBlueprint artifact (non-interactive)."""

    chat_id: Optional[str] = None
    app_id: Optional[str] = None
    workflow_name: Optional[str] = None
    user_id: Optional[str] = None

    if context_variables is not None and hasattr(context_variables, "get"):
        chat_id = context_variables.get("chat_id")
        app_id = context_variables.get("app_id")
        workflow_name = context_variables.get("workflow_name")
        user_id = context_variables.get("user_id")

    wf_logger = get_workflow_logger(workflow_name=workflow_name or "ValueEngine", chat_id=chat_id, app_id=app_id)

    overview = (concept_overview or "").strip()
    if not overview:
        return {"success": False, "error": "concept_overview is required"}
    if not app_id:
        return {"success": False, "error": "app_id missing from context; cannot persist concept blueprint"}

    endpoints = _sanitize_endpoints(api_endpoints)
    blueprint_obj = blueprint if isinstance(blueprint, dict) else None

    now = datetime.now(UTC)
    pm = PersistenceManager()
    await pm._ensure_client()
    client = pm.client
    if client is None:
        return {"success": False, "error": "Mongo client not available"}

    try:
        coll = client["autogen_ai_agents"]["Concepts"]
        update: Dict[str, Any] = {
            "app_id": str(app_id),
            "ConceptOverview": overview,
            "UpdatedAt": now,
        }
        if user_id:
            update["UpdatedByUserId"] = str(user_id)
        if endpoints:
            update["ApiEndpoints"] = endpoints
        if blueprint_obj:
            update["Blueprint"] = blueprint_obj

        res = await coll.update_one(
            {"app_id": str(app_id)},
            {"$set": update, "$setOnInsert": {"CreatedAt": now}},
            upsert=True,
        )
        wf_logger.info(
            "[ValueEngine] Concept blueprint saved: app_id=%s matched=%s modified=%s upserted=%s overview_len=%s endpoints=%s",
            app_id,
            getattr(res, "matched_count", None),
            getattr(res, "modified_count", None),
            bool(getattr(res, "upserted_id", None)),
            len(overview),
            len(endpoints),
        )
    except Exception as err:
        wf_logger.error("[ValueEngine] Failed saving concept blueprint: %s", err, exc_info=True)
        return {"success": False, "error": f"Failed saving concept blueprint: {err}"}

    # Emit a Blueprint artifact for the user to visualize (display-only; no response expected).
    event_id = f"concept_blueprint_{uuid.uuid4().hex}"
    try:
        transport = await SimpleTransport.get_instance()
        await transport.send_ui_tool_event(
            event_id=event_id,
            chat_id=str(chat_id) if chat_id else None,
            tool_name="concept_blueprint",
            component_name="ConceptBlueprint",
            display_type="artifact",
            payload={
                "title": "Concept Blueprint",
                "app_id": str(app_id),
                "concept_overview": overview,
                "blueprint": blueprint_obj,
                "api_endpoints": endpoints,
                "persist_ui_state": True,
            },
            awaiting_response=False,
            agent_name="GapAnalysisAgent",
        )
    except Exception as emit_err:
        wf_logger.debug("[ValueEngine] Failed emitting ConceptBlueprint artifact: %s", emit_err)

    return {
        "success": True,
        "app_id": str(app_id),
        "event_id": event_id,
        "saved": True,
        "endpoints_count": len(endpoints),
    }


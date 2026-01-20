"""backend/tools/notification_steward.py

Notification Steward (CI helper)

Purpose:
- Run on every PR / code change.
- Ensures MozaiksCore notification declarations stay coherent and declarative.

What it checks:
- Every notification id declared in notifications_config.json is well-formed.
- Channels referenced are known (in_app/email/sms/web_push).
- Optional: warns if templates are missing (templates are NOT required).

Optional autofix (safe):
- --fix can add missing template stubs to notification_templates.json
  WITHOUT changing runtime behavior (the system already falls back to _default).

Exit codes:
- 0: OK (no errors)
- 1: Errors found (invalid schema / unknown channels / missing required fields)

Usage:
  python backend/tools/notification_steward.py --check
  python backend/tools/notification_steward.py --check --fix

Designed to be LLM-friendly:
- Emits a compact JSON report to stdout when --json is provided.
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Any, Dict, List, Optional, Set, Tuple
import re


KNOWN_CHANNELS = {"in_app", "email", "sms", "web_push"}


@dataclass
class Issue:
    level: str  # "error" | "warning"
    code: str
    message: str
    path: Optional[str] = None


@dataclass
class Report:
    errors: List[Issue]
    warnings: List[Issue]
    stats: Dict[str, Any]


def _repo_root() -> Path:
    # backend/tools/notification_steward.py -> backend -> repo
    return Path(__file__).resolve().parents[2]


def _load_json(path: Path) -> Dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        raise RuntimeError(f"Missing file: {path}")
    except json.JSONDecodeError as e:
        raise RuntimeError(f"Invalid JSON in {path}: {e}")


def _titleize_notification_id(notification_id: str) -> str:
    # notes_manager_note_created -> Notes Manager Note Created
    return " ".join([p.capitalize() for p in notification_id.split("_") if p]).strip() or notification_id


def _collect_notification_defs(cfg: Dict[str, Any]) -> List[Tuple[str, Dict[str, Any], str]]:
    """Return list of (id, def, origin_path)."""
    out: List[Tuple[str, Dict[str, Any], str]] = []

    core = cfg.get("core", {})
    for n in core.get("notifications", []) or []:
        nid = n.get("id")
        if nid:
            out.append((nid, n, "core.notifications"))

    plugins = cfg.get("plugins", {}) or {}
    if isinstance(plugins, dict):
        for plugin_name, plugin_cfg in plugins.items():
            for n in (plugin_cfg or {}).get("notifications", []) or []:
                nid = n.get("id")
                if nid:
                    out.append((nid, n, f"plugins.{plugin_name}.notifications"))

    return out


def _iter_python_files(root: Path, rel_dirs: List[str]) -> List[Path]:
    files: List[Path] = []
    for d in rel_dirs:
        base = root / d
        if not base.exists():
            continue
        files.extend([p for p in base.rglob("*.py") if p.is_file()])
    return files


def _scan_notification_types(root: Path) -> Set[str]:
    """Best-effort scan for string-literal notification_type usages."""
    pattern = re.compile(
        r"create_notification\(.*?notification_type\s*=\s*['\"]([^'\"]+)['\"]",
        re.DOTALL,
    )
    results: Set[str] = set()

    for path in _iter_python_files(root, ["backend/core", "backend/plugins", "backend/app", "backend/security"]):
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue

        for m in pattern.finditer(text):
            nid = (m.group(1) or "").strip()
            if nid:
                results.add(nid)

    return results


def _guess_plugin_for_notification(cfg: Dict[str, Any], notification_id: str) -> Optional[str]:
    """Infer plugin name by matching configured plugin keys as prefixes."""
    plugins = cfg.get("plugins", {}) or {}
    if not isinstance(plugins, dict):
        return None
    for plugin_name in plugins.keys():
        if notification_id.startswith(f"{plugin_name}_"):
            return plugin_name
    return None


def _ensure_notification_def(cfg: Dict[str, Any], notification_id: str) -> bool:
    """Add a minimal notification definition to notifications_config.json if missing."""
    existing = {nid for nid, _, _ in _collect_notification_defs(cfg)}
    if notification_id in existing:
        return False

    label = _titleize_notification_id(notification_id)
    entry = {
        "id": notification_id,
        "label": label,
        "description": f"Notification for {label}.",
        "category": "system",
        "channels": ["in_app"],
        "default_enabled": True,
    }

    plugin_name = _guess_plugin_for_notification(cfg, notification_id)
    if plugin_name:
        entry["category"] = "plugins"
        plugins = cfg.setdefault("plugins", {})
        plugin_cfg = plugins.setdefault(
            plugin_name,
            {"display_name": _titleize_notification_id(plugin_name), "notifications": []},
        )
        plugin_cfg.setdefault("display_name", _titleize_notification_id(plugin_name))
        plugin_cfg.setdefault("notifications", [])
        if isinstance(plugin_cfg["notifications"], list):
            plugin_cfg["notifications"].append(entry)
        else:
            plugin_cfg["notifications"] = [entry]
        return True

    core = cfg.setdefault("core", {"display_name": "Core System", "notifications": []})
    core.setdefault("display_name", "Core System")
    core.setdefault("notifications", [])
    if isinstance(core["notifications"], list):
        core["notifications"].append(entry)
    else:
        core["notifications"] = [entry]
    return True


def _validate_notifications(cfg: Dict[str, Any], templates: Optional[Dict[str, Any]]) -> Report:
    errors: List[Issue] = []
    warnings: List[Issue] = []

    defs = _collect_notification_defs(cfg)

    seen: Set[str] = set()
    for nid, ndef, origin in defs:
        if nid in seen:
            errors.append(Issue("error", "duplicate_id", f"Duplicate notification id: {nid}", origin))
            continue
        seen.add(nid)

        # Required fields
        if not ndef.get("label"):
            errors.append(Issue("error", "missing_label", f"Notification {nid} missing label", origin))
        if not ndef.get("description"):
            warnings.append(Issue("warning", "missing_description", f"Notification {nid} missing description", origin))
        if not ndef.get("category"):
            errors.append(Issue("error", "missing_category", f"Notification {nid} missing category", origin))

        channels = ndef.get("channels")
        if not isinstance(channels, list) or not channels:
            errors.append(Issue("error", "missing_channels", f"Notification {nid} must define channels[]", origin))
        else:
            unknown = [c for c in channels if c not in KNOWN_CHANNELS]
            if unknown:
                errors.append(Issue("error", "unknown_channel", f"Notification {nid} uses unknown channels: {unknown}", origin))

        # Templates are optional; warn if missing
        if templates is not None:
            tmpl_types = (templates.get("templates") or {}) if isinstance(templates, dict) else {}
            if nid not in tmpl_types and nid != "_default":
                warnings.append(Issue("warning", "missing_template", f"No template found for {nid} (ok; will fall back to _default)", "notification_templates.json"))

    stats = {
        "notification_count": len(defs),
        "unique_notification_count": len(seen),
        "known_channels": sorted(KNOWN_CHANNELS),
    }

    return Report(
        errors=errors,
        warnings=warnings,
        stats=stats,
    )


def _ensure_template_stub(templates: Dict[str, Any], notification_id: str) -> bool:
    """Add a minimal stub template for notification_id if missing. Returns True if modified."""
    templates.setdefault("templates", {})
    t = templates["templates"]
    if notification_id in t:
        return False

    label = _titleize_notification_id(notification_id)
    t[notification_id] = {
        "in_app": {"title": "{{title}}", "body": "{{message}}"},
        "email": {"subject": f"{label} - {{app_name}}", "text_body": "{{message}}"},
        "sms": {"body": "{{app_name}}: {{message}}"},
        "push": {"title": "{{title}}", "body": "{{message}}"},
    }
    return True


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Run validations")
    parser.add_argument("--fix", action="store_true", help="Apply safe autofixes")
    parser.add_argument(
        "--fix-config",
        action="store_true",
        help="Scan codebase for notification_type literals and add missing ids to notifications_config.json",
    )
    parser.add_argument("--json", action="store_true", help="Output JSON report")
    args = parser.parse_args()

    if not args.check:
        parser.error("Use --check (and optionally --fix)")

    root = _repo_root()
    notif_cfg_path = root / "backend" / "core" / "config" / "notifications_config.json"
    tmpl_path = root / "backend" / "core" / "config" / "notification_templates.json"

    cfg = _load_json(notif_cfg_path)

    templates: Optional[Dict[str, Any]]
    try:
        templates = _load_json(tmpl_path)
    except RuntimeError:
        # Templates file missing is allowed (templates optional)
        templates = None

    report = _validate_notifications(cfg, templates)

    modified = False
    config_modified = False

    scanned_types: Set[str] = set()
    added_ids: List[str] = []
    if args.fix_config:
        scanned_types = _scan_notification_types(root)
        # Only add ids that look like notification identifiers (simple guard)
        for nid in sorted(scanned_types):
            if not nid or len(nid) > 200:
                continue
            if _ensure_notification_def(cfg, nid):
                added_ids.append(nid)
                config_modified = True

        if config_modified:
            notif_cfg_path.write_text(json.dumps(cfg, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    if args.fix and templates is not None:
        # Add stub templates for any missing ids
        for nid, _, _ in _collect_notification_defs(cfg):
            if nid == "_default":
                continue
            modified = _ensure_template_stub(templates, nid) or modified

        if modified:
            tmpl_path.write_text(json.dumps(templates, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    if args.json:
        payload = {
            "errors": [asdict(e) for e in report.errors],
            "warnings": [asdict(w) for w in report.warnings],
            "stats": report.stats,
            "modified": (modified or config_modified),
            "templates_modified": modified,
            "config_modified": config_modified,
            "scanned_notification_types": sorted(scanned_types) if scanned_types else [],
            "added_notification_ids": added_ids,
        }
        print(json.dumps(payload, indent=2))
    else:
        for e in report.errors:
            print(f"ERROR {e.code}: {e.message} ({e.path})")
        for w in report.warnings:
            print(f"WARN  {w.code}: {w.message} ({w.path})")
        print(f"OK: {report.stats['unique_notification_count']} notification ids checked")
        if config_modified:
            print(f"Applied: added {len(added_ids)} missing notification ids to notifications_config.json")
        if modified:
            print("Applied: added missing template stubs")

    return 1 if report.errors else 0


if __name__ == "__main__":
    raise SystemExit(main())

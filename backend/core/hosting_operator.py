# backend/core/hosting_operator.py
from __future__ import annotations

import argparse
import json
import sys
from copy import deepcopy
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


def _utcnow_iso() -> str:
    return datetime.now(tz=timezone.utc).isoformat()


def load_json(path: str | Path) -> dict[str, Any]:
    path = Path(path)
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def _coerce_number(value: Any) -> float | None:
    if value is None:
        return None
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return float(value)
    if isinstance(value, str):
        stripped = value.strip()
        if not stripped:
            return None
        try:
            return float(stripped)
        except Exception:
            return None
    return None


def _severity_for(value: float, *, warn: float | None, critical: float | None) -> str | None:
    if critical is not None and value >= critical:
        return "critical"
    if warn is not None and value >= warn:
        return "warn"
    return None


def _action_key(action: dict[str, Any]) -> str:
    return json.dumps(action, sort_keys=True, separators=(",", ":"))


def _annotate_actions(
    actions: Iterable[dict[str, Any]],
    *,
    allowed_auto_kinds: set[str],
    cooldowns: dict[str, Any],
) -> list[dict[str, Any]]:
    annotated: list[dict[str, Any]] = []
    for action in actions:
        if not isinstance(action, dict):
            continue

        out = deepcopy(action)
        kind = str(out.get("kind") or "").strip()
        requires_approval = bool(out.get("requires_approval", False))

        cooldown_s = cooldowns.get(kind, 0)
        try:
            cooldown_s = int(cooldown_s)
        except Exception:
            cooldown_s = 0

        if requires_approval:
            execution = "approval_required"
        elif kind in allowed_auto_kinds:
            execution = "auto"
        else:
            execution = "manual"

        out["cooldown_s"] = cooldown_s
        out["execution"] = execution
        annotated.append(out)

    return annotated


def evaluate_snapshot(policy: dict[str, Any], snapshot: dict[str, Any]) -> dict[str, Any]:
    """Evaluate a single metrics snapshot against the Hosting Operator policy.

    This is intentionally simple: it turns metrics into a list of recommended actions.
    Your platform/control-plane would be responsible for:
      - collecting metrics (Azure Monitor/App Insights/etc.)
      - applying cooldowns/state across time
      - executing Azure actions (scale/move plans/rollback)
      - enforcing Change Set gates
    """

    schema_version = str(policy.get("schema_version") or "").strip()
    if schema_version != "v1":
        raise ValueError(f"Unsupported policy schema_version: {schema_version!r}")

    profiles = policy.get("profiles")
    if not isinstance(profiles, dict) or not profiles:
        raise ValueError("Policy is missing 'profiles'")

    profile = str(snapshot.get("profile") or "starter").strip().lower()
    profile_policy = profiles.get(profile)
    if not isinstance(profile_policy, dict):
        raise ValueError(f"Unknown profile {profile!r}. Known: {sorted(profiles.keys())}")

    metrics = snapshot.get("metrics")
    if not isinstance(metrics, dict):
        raise ValueError("Snapshot is missing 'metrics' object")

    thresholds = profile_policy.get("thresholds") or {}
    if not isinstance(thresholds, dict):
        thresholds = {}

    responses = profile_policy.get("responses") or {}
    if not isinstance(responses, dict):
        responses = {}

    allowed_auto_kinds = {
        str(k).strip()
        for k in (profile_policy.get("auto_actions_allowed") or [])
        if str(k).strip()
    }
    cooldowns = policy.get("action_cooldowns_s") or {}
    if not isinstance(cooldowns, dict):
        cooldowns = {}

    decisions: list[dict[str, Any]] = []
    unique_actions: dict[str, dict[str, Any]] = {}

    for metric_name, raw_value in metrics.items():
        metric_thresholds = thresholds.get(metric_name)
        if not isinstance(metric_thresholds, dict):
            continue

        value = _coerce_number(raw_value)
        if value is None:
            continue

        warn = _coerce_number(metric_thresholds.get("warn"))
        critical = _coerce_number(metric_thresholds.get("critical"))
        severity = _severity_for(value, warn=warn, critical=critical)
        if severity is None:
            continue

        metric_responses = responses.get(metric_name, {})
        if not isinstance(metric_responses, dict):
            metric_responses = {}

        actions = metric_responses.get(severity, [])
        if not isinstance(actions, list):
            actions = []

        annotated = _annotate_actions(actions, allowed_auto_kinds=allowed_auto_kinds, cooldowns=cooldowns)
        for action in annotated:
            unique_actions.setdefault(_action_key(action), action)

        decisions.append(
            {
                "metric": metric_name,
                "value": value,
                "severity": severity,
                "thresholds": {"warn": warn, "critical": critical},
                "actions": annotated,
            }
        )

    decisions.sort(key=lambda d: (d.get("severity") != "critical", str(d.get("metric") or "")))

    return {
        "generated_at": _utcnow_iso(),
        "app_id": str(snapshot.get("app_id") or snapshot.get("appId") or "unknown-app"),
        "profile": profile,
        "decisions": decisions,
        "actions": list(unique_actions.values()),
    }


def _render_text(result: dict[str, Any]) -> str:
    lines: list[str] = []
    lines.append(f"app_id={result.get('app_id')} profile={result.get('profile')}")

    decisions = result.get("decisions") or []
    if not decisions:
        lines.append("no actions (all metrics within thresholds)")
        return "\n".join(lines)

    for decision in decisions:
        metric = decision.get("metric")
        value = decision.get("value")
        severity = decision.get("severity")
        lines.append(f"- {severity}: {metric}={value}")
        for action in decision.get("actions") or []:
            kind = action.get("kind")
            execution = action.get("execution")
            lines.append(f"  - {kind} ({execution})")

    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Evaluate a Hosting Operator policy against a metrics snapshot.")
    parser.add_argument(
        "--policy",
        default="docs/HostingOperatorPolicy.example.json",
        help="Path to Hosting Operator policy JSON (default: docs/HostingOperatorPolicy.example.json)",
    )
    parser.add_argument("--snapshot", required=True, help="Path to a metrics snapshot JSON")
    parser.add_argument("--format", choices=["json", "text"], default="json")
    parser.add_argument("--pretty", action="store_true", help="Pretty-print JSON output")
    args = parser.parse_args(argv)

    policy = load_json(args.policy)
    snapshot = load_json(args.snapshot)
    result = evaluate_snapshot(policy, snapshot)

    if args.format == "text":
        sys.stdout.write(_render_text(result) + "\n")
        return 0

    indent = 2 if args.pretty else None
    sys.stdout.write(json.dumps(result, indent=indent, sort_keys=args.pretty) + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())


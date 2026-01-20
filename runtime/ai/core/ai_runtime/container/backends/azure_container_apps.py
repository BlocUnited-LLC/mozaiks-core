"""Azure Container Apps-backed container runner."""

from __future__ import annotations

import logging
import os
import shutil
import subprocess
import uuid
from typing import Iterator, List

from ..spec import ContainerSpec, normalize_resources

logger = logging.getLogger("mozaiks_core.container_runner.azure_container_apps")


def _env_value(name: str) -> str:
    return (os.getenv(name) or "").strip()


def _require_az() -> None:
    if shutil.which("az") is None:
        raise RuntimeError("Azure CLI not found. Install Azure CLI or select MOZAIKS_CONTAINER_BACKEND=docker.")


def _resolve_config() -> tuple[str, str, str | None, str]:
    resource_group = _env_value("MOZAIKS_CONTAINER_APPS_RESOURCE_GROUP")
    environment = _env_value("MOZAIKS_CONTAINER_APPS_ENVIRONMENT")
    subscription = _env_value("MOZAIKS_CONTAINER_APPS_SUBSCRIPTION") or None
    name_prefix = _env_value("MOZAIKS_CONTAINER_APPS_NAME_PREFIX") or "mozaiks-run"
    if not resource_group or not environment:
        raise RuntimeError(
            "MOZAIKS_CONTAINER_APPS_RESOURCE_GROUP and MOZAIKS_CONTAINER_APPS_ENVIRONMENT are required for Azure Container Apps."
        )
    return resource_group, environment, subscription, name_prefix


def _build_env_args(env: dict[str, str]) -> List[str]:
    if not env:
        return []
    args = ["--env-vars"]
    for key, value in env.items():
        args.append(f"{key}={value}")
    return args


def _build_subscription_args(subscription: str | None) -> List[str]:
    if not subscription:
        return []
    return ["--subscription", subscription]


def start_container(spec: ContainerSpec) -> str:
    """Start a container app and return its handle (app name)."""
    _require_az()
    resource_group, environment, subscription, name_prefix = _resolve_config()

    if spec.mounts:
        raise ValueError("Azure Container Apps backend does not support host_path mounts.")

    container_name = f"{name_prefix}-{uuid.uuid4().hex[:12]}"
    args = [
        "az",
        "containerapp",
        "create",
        "--name",
        container_name,
        "--resource-group",
        resource_group,
        "--environment",
        environment,
        "--image",
        spec.image,
        "--target-port",
        str(spec.port),
        "--ingress",
        "external",
    ]

    if spec.enable_websocket:
        args.extend(["--transport", "http"])

    args.extend(_build_env_args(spec.env or {}))

    resources = normalize_resources(spec.resources)
    if resources:
        if resources.cpu is not None:
            args.extend(["--cpu", str(resources.cpu)])
        if resources.memory:
            args.extend(["--memory", str(resources.memory)])

    args.extend(_build_subscription_args(subscription))

    logger.info("Starting Azure Container App: %s", container_name)
    result = subprocess.run(args, capture_output=True, text=True, check=False)
    if result.returncode != 0:
        error = result.stderr.strip() or result.stdout.strip()
        raise RuntimeError(f"Azure Container Apps start failed: {error}")
    return container_name


def stream_container_logs(handle: str) -> Iterator[str]:
    """Stream container app logs as lines of text."""
    _require_az()
    if not handle:
        raise ValueError("Container handle is required.")
    resource_group, _, subscription, _ = _resolve_config()

    args = [
        "az",
        "containerapp",
        "logs",
        "show",
        "--name",
        handle,
        "--resource-group",
        resource_group,
        "--follow",
    ]
    args.extend(_build_subscription_args(subscription))

    proc = subprocess.Popen(
        args,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )
    if not proc.stdout:
        return
    try:
        for line in proc.stdout:
            yield line.rstrip("\n")
    finally:
        if proc.poll() is None:
            proc.terminate()


def stop_container(handle: str) -> None:
    """Stop a container app by deleting it."""
    _require_az()
    if not handle:
        raise ValueError("Container handle is required.")
    resource_group, _, subscription, _ = _resolve_config()

    args = [
        "az",
        "containerapp",
        "delete",
        "--name",
        handle,
        "--resource-group",
        resource_group,
        "--yes",
    ]
    args.extend(_build_subscription_args(subscription))
    result = subprocess.run(args, capture_output=True, text=True, check=False)
    if result.returncode != 0:
        error = result.stderr.strip() or result.stdout.strip()
        raise RuntimeError(f"Azure Container Apps stop failed: {error}")


__all__ = [
    "start_container",
    "stream_container_logs",
    "stop_container",
]

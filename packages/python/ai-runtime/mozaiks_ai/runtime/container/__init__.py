"""Neutral, declarative container runner with swappable backends."""

from __future__ import annotations

import os
from dataclasses import replace
from functools import lru_cache
from typing import Iterator

from .spec import ContainerMount, ContainerResources, ContainerSpec, validate_spec
from .backends import azure_container_apps as _azure_container_apps
from .backends import docker as _docker

_BACKEND_ALIASES = {
    "docker": "docker",
    "local": "docker",
    "azure": "azure_container_apps",
    "aca": "azure_container_apps",
    "azure_container_apps": "azure_container_apps",
}

_STANDARD_ENV_ALIASES = {
    "ASPNETCORE_ENVIRONMENT": ("ASPNETCORE_ENVIRONMENT", "ENV"),
    "APP_ID": ("APP_ID", "MOZAIKS_APP_ID"),
    "APP_TIER": ("APP_TIER", "MOZAIKS_APP_TIER"),
    "CONNECTIONSTRINGS__DEFAULT": (
        "CONNECTIONSTRINGS__DEFAULT",
        "DATABASE_URL",
        "DATABASE_URI",
        "MONGODB_URI",
    ),
}


def _normalize_aspnet_env(value: str) -> str:
    trimmed = value.strip()
    if not trimmed:
        return value
    return trimmed[:1].upper() + trimmed[1:]


def _merge_standard_env(env: dict[str, str] | None) -> dict[str, str]:
    merged = dict(env or {})
    for standard_key, aliases in _STANDARD_ENV_ALIASES.items():
        if merged.get(standard_key):
            continue
        for alias in aliases:
            if alias == standard_key:
                continue
            value = merged.get(alias)
            if value:
                merged[standard_key] = _normalize_aspnet_env(value) if standard_key == "ASPNETCORE_ENVIRONMENT" else value
                break
        if merged.get(standard_key):
            continue
        for alias in aliases:
            value = os.getenv(alias)
            if value:
                merged[standard_key] = _normalize_aspnet_env(value) if standard_key == "ASPNETCORE_ENVIRONMENT" else value
                break
    return merged


def _apply_standard_env(spec: ContainerSpec) -> ContainerSpec:
    merged_env = _merge_standard_env(spec.env)
    if merged_env == spec.env:
        return spec
    return replace(spec, env=merged_env)


def _resolve_backend_name() -> str:
    raw = (os.getenv("MOZAIKS_CONTAINER_BACKEND") or "docker").strip().lower()
    return _BACKEND_ALIASES.get(raw, raw)


@lru_cache(maxsize=1)
def _get_backend():
    backend_name = _resolve_backend_name()
    if backend_name == "docker":
        return _docker
    if backend_name == "azure_container_apps":
        return _azure_container_apps
    raise RuntimeError(
        "Unsupported container backend. Set MOZAIKS_CONTAINER_BACKEND to 'docker' or 'azure_container_apps'."
    )


def start_container(spec: ContainerSpec) -> str:
    """Start a container and return its handle."""
    spec = _apply_standard_env(spec)
    validate_spec(spec)
    backend = _get_backend()
    return backend.start_container(spec)


def stream_container_logs(handle: str) -> Iterator[str]:
    """Stream container logs as lines of text."""
    backend = _get_backend()
    return backend.stream_container_logs(handle)


def stop_container(handle: str) -> None:
    """Stop a running container."""
    backend = _get_backend()
    return backend.stop_container(handle)


__all__ = [
    "ContainerMount",
    "ContainerResources",
    "ContainerSpec",
    "start_container",
    "stream_container_logs",
    "stop_container",
]

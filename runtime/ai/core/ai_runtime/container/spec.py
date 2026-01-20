"""Container runner primitives and validation."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, Iterable, List, Optional


@dataclass(frozen=True)
class ContainerMount:
    host_path: str
    container_path: str


@dataclass(frozen=True)
class ContainerResources:
    cpu: Optional[float] = None
    memory: Optional[str] = None


@dataclass(frozen=True)
class ContainerSpec:
    image: str
    port: int
    env: Dict[str, str] = field(default_factory=dict)
    mounts: List[ContainerMount] = field(default_factory=list)
    resources: Optional[ContainerResources] = None
    enable_websocket: bool = False


def normalize_mounts(mounts: Iterable[ContainerMount | dict[str, str]]) -> List[ContainerMount]:
    normalized: List[ContainerMount] = []
    for mount in mounts:
        if isinstance(mount, ContainerMount):
            normalized.append(mount)
            continue
        if isinstance(mount, dict):
            host_path = mount.get("host_path")
            container_path = mount.get("container_path")
            if not host_path or not container_path:
                raise ValueError("Mount entries require host_path and container_path.")
            normalized.append(ContainerMount(host_path=str(host_path), container_path=str(container_path)))
            continue
        raise TypeError("Mount entries must be ContainerMount or dict.")
    return normalized


def normalize_resources(resources: ContainerResources | dict[str, object] | None) -> Optional[ContainerResources]:
    if resources is None:
        return None
    if isinstance(resources, ContainerResources):
        return resources
    if isinstance(resources, dict):
        return ContainerResources(
            cpu=resources.get("cpu"),
            memory=resources.get("memory"),
        )
    raise TypeError("Resources must be ContainerResources or dict.")


def validate_spec(spec: ContainerSpec) -> None:
    if not isinstance(spec.image, str) or not spec.image.strip():
        raise ValueError("ContainerSpec.image must be a non-empty string.")
    if not isinstance(spec.port, int) or spec.port <= 0:
        raise ValueError("ContainerSpec.port must be a positive integer.")
    if not isinstance(spec.env, dict):
        raise ValueError("ContainerSpec.env must be a dict of strings.")
    for key, value in spec.env.items():
        if not isinstance(key, str) or not isinstance(value, str):
            raise ValueError("ContainerSpec.env must use string keys and values.")


__all__ = [
    "ContainerMount",
    "ContainerResources",
    "ContainerSpec",
    "normalize_mounts",
    "normalize_resources",
    "validate_spec",
]

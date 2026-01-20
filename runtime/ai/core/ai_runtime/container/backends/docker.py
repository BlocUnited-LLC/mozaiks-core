"""Docker-backed container runner."""

from __future__ import annotations

import logging
import shutil
import subprocess
from typing import Iterator, List

from ..spec import ContainerSpec, normalize_mounts, normalize_resources

logger = logging.getLogger("mozaiks_core.container_runner.docker")


def _require_docker() -> None:
    if shutil.which("docker") is None:
        raise RuntimeError("Docker CLI not found. Install Docker or select MOZAIKS_CONTAINER_BACKEND=azure_container_apps.")


def _build_run_args(spec: ContainerSpec) -> List[str]:
    args = ["docker", "run", "-d", "-p", f"{spec.port}:{spec.port}"]
    for key, value in (spec.env or {}).items():
        args.extend(["-e", f"{key}={value}"])
    for mount in normalize_mounts(spec.mounts or []):
        args.extend(["-v", f"{mount.host_path}:{mount.container_path}"])
    resources = normalize_resources(spec.resources)
    if resources:
        if resources.cpu is not None:
            args.extend(["--cpus", str(resources.cpu)])
        if resources.memory:
            args.extend(["--memory", str(resources.memory)])
    args.append(spec.image)
    return args


def start_container(spec: ContainerSpec) -> str:
    """Start a container and return its handle (container id)."""
    _require_docker()
    args = _build_run_args(spec)
    logger.info("Starting container from image: %s", spec.image)
    result = subprocess.run(args, capture_output=True, text=True, check=False)
    if result.returncode != 0:
        error = result.stderr.strip() or result.stdout.strip()
        raise RuntimeError(f"Container start failed: {error}")
    container_id = result.stdout.strip()
    if not container_id:
        raise RuntimeError("Container start failed: no container id returned.")
    return container_id


def stream_container_logs(handle: str) -> Iterator[str]:
    """Stream container logs as lines of text."""
    _require_docker()
    if not handle:
        raise ValueError("Container handle is required.")
    proc = subprocess.Popen(
        ["docker", "logs", "-f", handle],
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
    """Stop a running container."""
    _require_docker()
    if not handle:
        raise ValueError("Container handle is required.")
    result = subprocess.run(
        ["docker", "stop", handle],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        error = result.stderr.strip() or result.stdout.strip()
        raise RuntimeError(f"Container stop failed: {error}")


__all__ = [
    "start_container",
    "stream_container_logs",
    "stop_container",
]

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from types import ModuleType
from typing import Callable, Dict, Optional
import hashlib
import importlib.util
import logging
import re
import sys
import threading
import time

logger = logging.getLogger("plugin_host.loader")


@dataclass
class PluginRecord:
    name: str
    path: Path
    status: str
    error: Optional[str] = None
    module_name: Optional[str] = None
    package_name: Optional[str] = None
    entrypoint_name: Optional[str] = None
    entrypoint: Optional[Callable] = None
    last_modified: Optional[float] = None
    loaded_at: Optional[float] = None


class PluginLoader:
    def __init__(self, plugins_dir: str) -> None:
        self.plugins_dir = Path(plugins_dir)
        self._plugins: Dict[str, PluginRecord] = {}
        self._lock = threading.RLock()

    def list_plugins(self) -> list[dict]:
        with self._lock:
            return [
                {
                    "name": record.name,
                    "status": record.status,
                    **({"error": record.error} if record.error else {}),
                }
                for record in self._plugins.values()
            ]

    def count_loaded(self) -> int:
        with self._lock:
            return sum(1 for record in self._plugins.values() if record.status == "loaded")

    def scan_plugins(self) -> None:
        if not self.plugins_dir.exists():
            logger.warning("Plugins directory does not exist: %s", self.plugins_dir)
            return

        with self._lock:
            discovered = {
                entry.name: entry
                for entry in self.plugins_dir.iterdir()
                if entry.is_dir() and not entry.name.startswith(".")
            }

            for name, path in discovered.items():
                self._ensure_loaded(name, path)

            removed = set(self._plugins) - set(discovered)
            for name in removed:
                record = self._plugins.pop(name)
                self._unload_modules(record)
                logger.info("Removed plugin from registry: %s", name)

    def get_plugin(self, plugin_name: str) -> Optional[PluginRecord]:
        with self._lock:
            return self._plugins.get(plugin_name)

    def ensure_loaded(self, plugin_name: str) -> Optional[PluginRecord]:
        path = self.plugins_dir / plugin_name
        if not path.is_dir():
            return None
        with self._lock:
            return self._ensure_loaded(plugin_name, path)

    def _ensure_loaded(self, plugin_name: str, path: Path) -> PluginRecord:
        record = self._plugins.get(plugin_name)
        logic_path = path / "logic.py"
        if not logic_path.exists():
            if record:
                self._unload_modules(record)
            record = PluginRecord(
                name=plugin_name,
                path=path,
                status="error",
                error="Missing logic.py",
            )
            self._plugins[plugin_name] = record
            return record

        last_modified = logic_path.stat().st_mtime
        if record and record.status == "loaded" and record.last_modified == last_modified:
            return record

        try:
            record = self._load_plugin(plugin_name, path, logic_path, last_modified)
            self._plugins[plugin_name] = record
            return record
        except Exception as exc:
            logger.exception("Failed to load plugin %s", plugin_name)
            record = PluginRecord(
                name=plugin_name,
                path=path,
                status="error",
                error=f"{type(exc).__name__}: {exc}",
                last_modified=last_modified,
            )
            self._plugins[plugin_name] = record
            return record

    def _load_plugin(
        self, plugin_name: str, path: Path, logic_path: Path, last_modified: float
    ) -> PluginRecord:
        existing = self._plugins.get(plugin_name)
        if existing:
            self._unload_modules(existing)

        package_name = self._package_name(plugin_name)
        module_name = f"{package_name}.logic"

        self._ensure_package(package_name, path)
        spec = importlib.util.spec_from_file_location(module_name, logic_path)
        if spec is None or spec.loader is None:
            raise RuntimeError("Unable to create module spec")

        module = importlib.util.module_from_spec(spec)
        module.__package__ = package_name
        sys.modules[module_name] = module
        spec.loader.exec_module(module)

        entrypoint, entrypoint_name = self._resolve_entrypoint(module)
        logger.info("Loaded plugin %s (%s)", plugin_name, entrypoint_name)

        return PluginRecord(
            name=plugin_name,
            path=path,
            status="loaded",
            module_name=module_name,
            package_name=package_name,
            entrypoint_name=entrypoint_name,
            entrypoint=entrypoint,
            last_modified=last_modified,
            loaded_at=time.time(),
        )

    def _resolve_entrypoint(self, module: ModuleType) -> tuple[Callable, str]:
        if hasattr(module, "execute"):
            return getattr(module, "execute"), "execute"
        if hasattr(module, "run"):
            return getattr(module, "run"), "run"
        raise RuntimeError("No execute or run function found")

    def _ensure_package(self, package_name: str, path: Path) -> ModuleType:
        if package_name in sys.modules:
            return sys.modules[package_name]
        spec = importlib.util.spec_from_loader(package_name, loader=None, is_package=True)
        if spec is None:
            raise RuntimeError("Unable to create package spec")
        package = importlib.util.module_from_spec(spec)
        package.__path__ = [str(path)]
        sys.modules[package_name] = package
        return package

    def _unload_modules(self, record: PluginRecord) -> None:
        package_name = record.package_name
        module_name = record.module_name
        if module_name and module_name in sys.modules:
            del sys.modules[module_name]
        if package_name:
            prefix = f"{package_name}."
            for name in list(sys.modules):
                if name == package_name or name.startswith(prefix):
                    del sys.modules[name]

    def _package_name(self, plugin_name: str) -> str:
        safe = re.sub(r"[^0-9a-zA-Z_]", "_", plugin_name)
        if not safe or safe[0].isdigit():
            safe = f"_{safe}"
        digest = hashlib.sha1(plugin_name.encode("utf-8")).hexdigest()[:8]
        return f"mozaiks_plugins.{safe}_{digest}"

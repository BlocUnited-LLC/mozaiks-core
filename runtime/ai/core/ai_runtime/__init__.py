"""Core Package Root
=====================
Aggregated exports for primary subsystems (workflow, transport, observability, data, events).
Downstream code should prefer importing from specific submodules for clarity, but
these re-exports are provided for convenience and a stable public surface.
"""

import sys

# Compatibility shim for legacy `mozaiksai.core.*` imports.
_ai_runtime_module = sys.modules.get(__name__)
sys.modules["mozaiksai"] = sys.modules.get("backend") or sys.modules.get("mozaiksai") or _ai_runtime_module
sys.modules["mozaiksai.core"] = (
    sys.modules.get("backend.core.ai_runtime")
    or sys.modules.get("core.ai_runtime")
    or _ai_runtime_module
)

from . import multitenant as _multitenant
from . import core_config as _core_config

sys.modules["mozaiksai.core.multitenant"] = (
    sys.modules.get("backend.core.ai_runtime.multitenant")
    or sys.modules.get("core.ai_runtime.multitenant")
    or _multitenant
)
sys.modules["mozaiksai.core.core_config"] = (
    sys.modules.get("backend.core.ai_runtime.core_config")
    or sys.modules.get("core.ai_runtime.core_config")
    or _core_config
)

from .workflow import (
	run_workflow_orchestration,
	create_ag2_pattern,
	register_workflow,
	get_workflow_handler,
	get_workflow_transport,
	workflow_status_summary,
)
from .transport import SimpleTransport
from .observability import (
	PerformanceManager,
	PerformanceConfig,
	get_performance_manager,
)
from .data import (
	PersistenceManager,
	AG2PersistenceManager,
)
from .events import (
	emit_business_event,
	emit_ui_tool_event, 
	get_event_dispatcher,
)

__all__ = [
	# Workflow
	"run_workflow_orchestration",
	"create_ag2_pattern",
	"register_workflow",
	"get_workflow_handler",
	"get_workflow_transport",
	"workflow_status_summary",
	# Transport
	"SimpleTransport",
	# Observability
	"PerformanceManager",
	"PerformanceConfig",
	"get_performance_manager",
	# Persistence
	"PersistenceManager",
	"AG2PersistenceManager",
	# Events  
	"emit_business_event",
	"emit_ui_tool_event",
	"get_event_dispatcher",
]

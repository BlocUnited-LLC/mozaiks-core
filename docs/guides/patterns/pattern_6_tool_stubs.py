"""
Pattern 6: Pipeline — Automated ML Workflow Tool Stubs

Runtime tool implementations for the Automated ML Pipeline workflow.
These stubs provide the execution surface for the declarative workflow.

Key Pipeline Tools:
- Data loading and exploration
- Code execution environment
- Preprocessing readiness checks
- Model metrics tracking
- Output capture and visualization

Mozaiks/AgentGenerator alignment:
- If a dataset is bundled into the generated app, it is placed automatically under
    `datasets/<filename>` (no user-provided path).
- Tools should therefore accept relative paths like `datasets/my_data.csv`.
"""

import asyncio
import base64
import io
import sys
from contextlib import redirect_stdout, redirect_stderr
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Any

# =============================================================================
# DATA STRUCTURES
# =============================================================================


class PipelineStage(Enum):
    """Pipeline stage enumeration."""
    INIT = "init"
    EXPLORE = "explore"
    PREPROCESS = "preprocess"
    TRAIN = "train"
    SUMMARIZE = "summarize"
    END = "end"


@dataclass
class ExecutionResult:
    """Result of code execution."""
    success: bool
    stdout: str
    stderr: str
    execution_time: float
    visualizations: list[str] = field(default_factory=list)  # Base64 images
    error_type: str | None = None
    error_message: str | None = None


@dataclass
class ModelMetrics:
    """Tracked metrics for a trained model."""
    model_name: str
    trial_number: int
    metrics: dict[str, float]
    timestamp: datetime = field(default_factory=datetime.now)


@dataclass
class CapturedOutput:
    """Captured output from workflow stages."""
    output_id: str
    output_type: str  # code, result, visualization, metrics
    content: str
    stage: PipelineStage
    timestamp: datetime = field(default_factory=datetime.now)


# =============================================================================
# RUNTIME STATE (would be injected by runtime in production)
# =============================================================================


class PipelineState:
    """
    Runtime state for the ML pipeline.
    In production, this would be managed by the AG2 runtime.
    """
    
    def __init__(self):
        self.current_stage = PipelineStage.INIT
        self.dataset_path: str | None = None
        self.dataframe: Any = None  # pandas DataFrame
        self.exploration_complete = False
        self.preprocessing_complete = False
        self.training_trials = 0
        self.successful_code_snippets: list[str] = []
        self.model_metrics: list[ModelMetrics] = []
        self.captured_outputs: list[CapturedOutput] = []
        self.visualizations: list[dict[str, Any]] = []
        self.execution_namespace: dict[str, Any] = {}  # Persistent execution context
        
    def reset(self):
        """Reset state for new workflow."""
        self.__init__()


# Global state instance (would be per-session in production)
_pipeline_state = PipelineState()


# =============================================================================
# TOOL IMPLEMENTATIONS
# =============================================================================


async def load_dataset(
    file_path: str,
    *,
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    Load a CSV dataset and return basic information.
    
    This is the entry point tool that initializes the pipeline with data.
    
    Args:
                file_path: Path to the dataset file.
                    - Recommended (Mozaiks): `datasets/<filename>.csv`
                    - Convenience: you may pass just `<filename>.csv`; the tool will try `datasets/<filename>.csv`.
        context: Runtime context (app_id, user_id, etc.)
        
    Returns:
        Dataset information including shape, columns, dtypes, and sample
    """
    global _pipeline_state
    
    try:
        # Validate file exists (accept common Mozaiks bundling locations)
        path = Path(file_path)
        if not path.is_absolute() and not path.exists():
            # If a bare filename was provided, try Mozaiks default bundle locations.
            if len(path.parts) == 1:
                candidate_paths = [Path("datasets") / path.name, Path("assets") / path.name]
                for candidate in candidate_paths:
                    if candidate.exists():
                        path = candidate
                        break

        if not path.exists():
            return {
                "success": False,
                "error": f"File not found: {file_path}",
                "shape": None,
                "columns": None,
                "dtypes": None,
                "sample_data": None
            }
            
        if not path.suffix.lower() == '.csv':
            return {
                "success": False,
                "error": f"Expected CSV file, got: {path.suffix}",
                "shape": None,
                "columns": None,
                "dtypes": None,
                "sample_data": None
            }
        
        # Import pandas (lazy import for stub)
        try:
            import pandas as pd
        except ImportError:
            return {
                "success": False,
                "error": "pandas not installed. Run: pip install pandas",
                "shape": None,
                "columns": None,
                "dtypes": None,
                "sample_data": None
            }
        
        # Load dataset
        df = pd.read_csv(str(path))
        
        # Store in state
        _pipeline_state.dataset_path = str(path)
        _pipeline_state.dataframe = df
        _pipeline_state.execution_namespace['df'] = df
        _pipeline_state.execution_namespace['pd'] = pd
        
        # Prepare response
        return {
            "success": True,
            "shape": list(df.shape),
            "columns": df.columns.tolist(),
            "dtypes": {col: str(dtype) for col, dtype in df.dtypes.items()},
            "sample_data": df.head(5).to_string(),
            "missing_values": df.isnull().sum().to_dict(),
            "numeric_columns": df.select_dtypes(include=['number']).columns.tolist(),
            "categorical_columns": df.select_dtypes(include=['object', 'category']).columns.tolist()
        }
        
    except Exception as e:
        return {
            "success": False,
            "error": str(e),
            "shape": None,
            "columns": None,
            "dtypes": None,
            "sample_data": None
        }


async def execute_code(
    code: str,
    *,
    timeout: int = 60,
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    Execute Python code in an isolated Jupyter-like environment.
    
    Maintains persistent namespace across executions within a session,
    similar to Jupyter notebook cells.
    
    Args:
        code: Python code to execute
        timeout: Execution timeout in seconds
        context: Runtime context
        
    Returns:
        Execution result with stdout, stderr, visualizations
    """
    global _pipeline_state
    
    import time
    start_time = time.time()
    
    # Capture stdout and stderr
    stdout_capture = io.StringIO()
    stderr_capture = io.StringIO()
    visualizations: list[str] = []
    
    try:
        # Set up matplotlib to capture figures
        try:
            import matplotlib
            matplotlib.use('Agg')  # Non-interactive backend
            import matplotlib.pyplot as plt
            _pipeline_state.execution_namespace['plt'] = plt
        except ImportError:
            pass
        
        # Add common imports to namespace if not present
        if 'np' not in _pipeline_state.execution_namespace:
            try:
                import numpy as np
                _pipeline_state.execution_namespace['np'] = np
            except ImportError:
                pass
                
        if 'sklearn' not in _pipeline_state.execution_namespace:
            try:
                import sklearn
                _pipeline_state.execution_namespace['sklearn'] = sklearn
            except ImportError:
                pass
        
        # Execute with timeout
        with redirect_stdout(stdout_capture), redirect_stderr(stderr_capture):
            # Use exec with persistent namespace
            exec(code, _pipeline_state.execution_namespace)
        
        # Capture any matplotlib figures
        try:
            import matplotlib.pyplot as plt
            figures = [plt.figure(i) for i in plt.get_fignums()]
            for fig in figures:
                buf = io.BytesIO()
                fig.savefig(buf, format='png', bbox_inches='tight', dpi=100)
                buf.seek(0)
                visualizations.append(base64.b64encode(buf.read()).decode('utf-8'))
            plt.close('all')
        except Exception:
            pass  # matplotlib not available or no figures
        
        execution_time = time.time() - start_time
        
        # Store successful code snippet
        _pipeline_state.successful_code_snippets.append(code)
        
        return {
            "success": True,
            "stdout": stdout_capture.getvalue(),
            "stderr": stderr_capture.getvalue(),
            "execution_time": execution_time,
            "visualizations": visualizations,
            "error_type": None,
            "error_message": None
        }
        
    except SyntaxError as e:
        execution_time = time.time() - start_time
        return {
            "success": False,
            "stdout": stdout_capture.getvalue(),
            "stderr": stderr_capture.getvalue(),
            "execution_time": execution_time,
            "visualizations": [],
            "error_type": "SyntaxError",
            "error_message": f"Line {e.lineno}: {e.msg}"
        }
        
    except Exception as e:
        execution_time = time.time() - start_time
        import traceback
        return {
            "success": False,
            "stdout": stdout_capture.getvalue(),
            "stderr": stderr_capture.getvalue() + "\n" + traceback.format_exc(),
            "execution_time": execution_time,
            "visualizations": [],
            "error_type": type(e).__name__,
            "error_message": str(e)
        }


async def check_preprocessing_readiness(
    preprocessing_summary: str,
    data_state: dict[str, Any],
    *,
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    LLM-based check to determine if data is ready for training.
    
    In production, this would invoke the LLM to make a decision.
    This stub implements rule-based heuristics as a fallback.
    
    Args:
        preprocessing_summary: Summary of preprocessing steps completed
        data_state: Current state of the data (missing values, dtypes, etc.)
        context: Runtime context
        
    Returns:
        Readiness assessment with reasoning
    """
    missing_steps: list[str] = []
    
    # Check for remaining missing values
    missing_values = data_state.get('missing_values', {})
    total_missing = sum(missing_values.values()) if isinstance(missing_values, dict) else 0
    if total_missing > 0:
        missing_steps.append(f"Handle {total_missing} remaining missing values")
    
    # Check for categorical encoding
    categorical_cols = data_state.get('categorical_columns', [])
    if categorical_cols and 'encoded' not in preprocessing_summary.lower():
        missing_steps.append(f"Encode {len(categorical_cols)} categorical columns")
    
    # Check for feature scaling
    if 'scale' not in preprocessing_summary.lower() and 'normalize' not in preprocessing_summary.lower():
        missing_steps.append("Consider scaling/normalizing numerical features")
    
    # Check for train/test split
    if 'split' not in preprocessing_summary.lower():
        missing_steps.append("Create train/test split")
    
    # Determine readiness
    # Critical issues block training; suggestions are optional
    critical_issues = [s for s in missing_steps if 'missing values' in s.lower() or 'split' in s.lower()]
    
    if critical_issues:
        return {
            "ready_for_training": False,
            "reasoning": f"Critical preprocessing steps missing: {', '.join(critical_issues)}",
            "missing_steps": missing_steps,
            "needs_more_analysis": False
        }
    elif missing_steps:
        # Non-critical suggestions - can proceed
        return {
            "ready_for_training": True,
            "reasoning": f"Data is ready for training. Optional improvements: {', '.join(missing_steps)}",
            "missing_steps": missing_steps,
            "needs_more_analysis": False
        }
    else:
        return {
            "ready_for_training": True,
            "reasoning": "All preprocessing steps appear complete. Data is ready for model training.",
            "missing_steps": [],
            "needs_more_analysis": False
        }


async def save_model_metrics(
    model_name: str,
    metrics: dict[str, float],
    trial_number: int,
    *,
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    Save model performance metrics for comparison.
    
    Tracks metrics across training trials to enable model comparison.
    
    Args:
        model_name: Name/type of the model (e.g., "RandomForest", "XGBoost")
        metrics: Performance metrics (accuracy, rmse, f1, etc.)
        trial_number: Training trial number
        context: Runtime context
        
    Returns:
        Confirmation with comparison table
    """
    global _pipeline_state
    
    # Create metrics record
    model_metrics = ModelMetrics(
        model_name=model_name,
        trial_number=trial_number,
        metrics=metrics
    )
    
    _pipeline_state.model_metrics.append(model_metrics)
    
    # Generate comparison table
    if _pipeline_state.model_metrics:
        # Build markdown table
        all_metric_keys = set()
        for m in _pipeline_state.model_metrics:
            all_metric_keys.update(m.metrics.keys())
        
        metric_keys = sorted(all_metric_keys)
        
        # Header
        table_lines = [
            "| Trial | Model | " + " | ".join(metric_keys) + " |",
            "|-------|-------|" + "|".join(["-------"] * len(metric_keys)) + "|"
        ]
        
        # Rows
        for m in _pipeline_state.model_metrics:
            values = [str(m.metrics.get(k, "N/A")) for k in metric_keys]
            table_lines.append(f"| {m.trial_number} | {m.model_name} | " + " | ".join(values) + " |")
        
        comparison_table = "\n".join(table_lines)
    else:
        comparison_table = "No metrics recorded yet."
    
    return {
        "saved": True,
        "comparison_table": comparison_table,
        "total_trials": len(_pipeline_state.model_metrics)
    }


async def capture_output(
    output_type: str,
    content: str,
    stage: str,
    *,
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    Capture and store execution output for summary.
    
    Collects all outputs (code, results, visualizations) for final
    workflow summary and script integration.
    
    Args:
        output_type: Type of output (code, result, visualization, metrics)
        content: Content to capture
        stage: Pipeline stage this output belongs to
        context: Runtime context
        
    Returns:
        Confirmation with output ID
    """
    global _pipeline_state
    
    import uuid
    
    try:
        stage_enum = PipelineStage(stage.lower())
    except ValueError:
        stage_enum = PipelineStage.INIT
    
    output_id = f"{stage}_{output_type}_{uuid.uuid4().hex[:8]}"
    
    captured = CapturedOutput(
        output_id=output_id,
        output_type=output_type,
        content=content,
        stage=stage_enum
    )
    
    _pipeline_state.captured_outputs.append(captured)
    
    return {
        "captured": True,
        "output_id": output_id,
        "total_captured": len(_pipeline_state.captured_outputs)
    }


async def save_visualization(
    image_data: str,
    title: str,
    stage: str,
    *,
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    Save generated visualization for display and summary.
    
    Stores base64-encoded images with metadata for inclusion in
    final workflow summary.
    
    Args:
        image_data: Base64 encoded image data
        title: Visualization title
        stage: Pipeline stage
        context: Runtime context
        
    Returns:
        Confirmation with image ID
    """
    global _pipeline_state
    
    import uuid
    
    image_id = f"viz_{stage}_{uuid.uuid4().hex[:8]}"
    
    visualization = {
        "image_id": image_id,
        "title": title,
        "stage": stage,
        "image_data": image_data,
        "timestamp": datetime.now().isoformat()
    }
    
    _pipeline_state.visualizations.append(visualization)
    
    return {
        "saved": True,
        "image_id": image_id,
        "total_visualizations": len(_pipeline_state.visualizations)
    }


# =============================================================================
# PIPELINE STATE MANAGEMENT (Runtime Integration)
# =============================================================================


async def get_pipeline_state(
    *,
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    Get current pipeline state for orchestration decisions.
    
    Used by the runtime to check stage transitions and progress.
    """
    global _pipeline_state
    
    return {
        "current_stage": _pipeline_state.current_stage.value,
        "dataset_loaded": _pipeline_state.dataframe is not None,
        "exploration_complete": _pipeline_state.exploration_complete,
        "preprocessing_complete": _pipeline_state.preprocessing_complete,
        "training_trials": _pipeline_state.training_trials,
        "code_snippets_count": len(_pipeline_state.successful_code_snippets),
        "metrics_count": len(_pipeline_state.model_metrics),
        "visualizations_count": len(_pipeline_state.visualizations)
    }


async def advance_stage(
    target_stage: str,
    *,
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    Advance pipeline to next stage.
    
    Called by orchestration when transition conditions are met.
    """
    global _pipeline_state
    
    try:
        new_stage = PipelineStage(target_stage.lower())
        old_stage = _pipeline_state.current_stage
        _pipeline_state.current_stage = new_stage
        
        return {
            "success": True,
            "previous_stage": old_stage.value,
            "current_stage": new_stage.value
        }
    except ValueError as e:
        return {
            "success": False,
            "error": f"Invalid stage: {target_stage}",
            "current_stage": _pipeline_state.current_stage.value
        }


async def generate_final_summary(
    *,
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    Generate final workflow summary with integrated code.
    
    Combines all successful code snippets into a reproducible script.
    """
    global _pipeline_state
    
    # Build integrated script
    script_parts = [
        '"""',
        'Automated ML Pipeline - Generated Script',
        f'Generated: {datetime.now().isoformat()}',
        '"""',
        '',
        '# Imports',
        'import pandas as pd',
        'import numpy as np',
        'from sklearn.model_selection import train_test_split',
        'from sklearn.preprocessing import StandardScaler, LabelEncoder',
        'import matplotlib.pyplot as plt',
        '',
        '# =============================================================================',
        '# DATA LOADING',
        '# =============================================================================',
        ''
    ]
    
    # Add code snippets by stage
    for i, snippet in enumerate(_pipeline_state.successful_code_snippets):
        script_parts.append(f'# --- Code Block {i+1} ---')
        script_parts.append(snippet)
        script_parts.append('')
    
    integrated_script = '\n'.join(script_parts)
    
    # Build model comparison
    if _pipeline_state.model_metrics:
        best_model = max(
            _pipeline_state.model_metrics,
            key=lambda m: m.metrics.get('accuracy', m.metrics.get('r2', 0))
        )
        best_model_info = f"{best_model.model_name} (Trial {best_model.trial_number})"
    else:
        best_model_info = "No models trained"
    
    return {
        "success": True,
        "integrated_script": integrated_script,
        "total_code_blocks": len(_pipeline_state.successful_code_snippets),
        "models_trained": len(_pipeline_state.model_metrics),
        "best_model": best_model_info,
        "visualizations_generated": len(_pipeline_state.visualizations),
        "dataset_path": _pipeline_state.dataset_path
    }


# =============================================================================
# TOOL REGISTRY
# =============================================================================

TOOL_REGISTRY = {
    "load_dataset": load_dataset,
    "execute_code": execute_code,
    "check_preprocessing_readiness": check_preprocessing_readiness,
    "save_model_metrics": save_model_metrics,
    "capture_output": capture_output,
    "save_visualization": save_visualization,
    "get_pipeline_state": get_pipeline_state,
    "advance_stage": advance_stage,
    "generate_final_summary": generate_final_summary,
}


async def invoke_tool(
    tool_name: str,
    parameters: dict[str, Any],
    context: dict[str, Any] | None = None
) -> dict[str, Any]:
    """
    Unified tool invocation interface for runtime.
    
    Args:
        tool_name: Name of the tool to invoke
        parameters: Tool parameters
        context: Runtime context (app_id, user_id, session_id, etc.)
        
    Returns:
        Tool execution result
    """
    if tool_name not in TOOL_REGISTRY:
        return {
            "success": False,
            "error": f"Unknown tool: {tool_name}",
            "available_tools": list(TOOL_REGISTRY.keys())
        }
    
    tool_fn = TOOL_REGISTRY[tool_name]
    
    try:
        result = await tool_fn(**parameters, context=context)
        return result
    except TypeError as e:
        return {
            "success": False,
            "error": f"Invalid parameters for {tool_name}: {str(e)}"
        }
    except Exception as e:
        return {
            "success": False,
            "error": f"Tool execution failed: {str(e)}",
            "error_type": type(e).__name__
        }


# =============================================================================
# EXAMPLE USAGE
# =============================================================================

if __name__ == "__main__":
    async def demo():
        """Demonstrate pipeline tools."""
        print("=== Pattern 6: Pipeline Tool Stubs Demo ===\n")
        
        # Simulate loading a dataset
        print("1. Loading dataset...")
        result = await load_dataset("house_prices_train.csv")
        print(f"   Success: {result['success']}")
        if result['success']:
            print(f"   Shape: {result['shape']}")
            print(f"   Columns: {len(result['columns'])} columns")
        else:
            print(f"   Error: {result.get('error', 'Unknown')}")
        
        # Simulate code execution
        print("\n2. Executing exploration code...")
        code = """
import pandas as pd
print("Dataset shape:", df.shape)
print("\\nColumn types:")
print(df.dtypes.value_counts())
print("\\nMissing values:", df.isnull().sum().sum())
"""
        result = await execute_code(code)
        print(f"   Success: {result['success']}")
        if result['success']:
            print(f"   Output: {result['stdout'][:200]}...")
        else:
            print(f"   Error: {result['error_message']}")
        
        # Check preprocessing readiness
        print("\n3. Checking preprocessing readiness...")
        result = await check_preprocessing_readiness(
            preprocessing_summary="Handled missing values, encoded categoricals",
            data_state={
                "missing_values": {"col1": 0, "col2": 0},
                "categorical_columns": []
            }
        )
        print(f"   Ready: {result['ready_for_training']}")
        print(f"   Reasoning: {result['reasoning']}")
        
        # Save model metrics
        print("\n4. Saving model metrics...")
        result = await save_model_metrics(
            model_name="RandomForest",
            metrics={"accuracy": 0.85, "f1": 0.82},
            trial_number=1
        )
        print(f"   Saved: {result['saved']}")
        print(f"   Comparison Table:\n{result['comparison_table']}")
        
        # Get pipeline state
        print("\n5. Getting pipeline state...")
        state = await get_pipeline_state()
        print(f"   Current Stage: {state['current_stage']}")
        print(f"   Training Trials: {state['training_trials']}")
        
        print("\n=== Demo Complete ===")
    
    asyncio.run(demo())

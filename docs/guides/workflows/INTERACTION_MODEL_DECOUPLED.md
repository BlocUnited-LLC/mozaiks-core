# Decoupled Interaction Model

**Status**: Active Source of Truth
**Purpose**: To define the three orthogonal layers of human interaction in MozaiksAI.

## Core Philosophy
Unlike a rigid matrix where "Shape" dictates "Behavior", this model decouples the **User Interface (UI)** from the **Agent Intent**.

*   **UI Pattern** (`ui_pattern`) defines **HOW** the user interacts (The Shape).
*   **Interaction Mode** (`human_interaction`) defines **WHY** the agent stops (The Intent).

These two layers are independent. A "Wizard" (Multi-Step UI) can be used to gather data (`context`) OR to refine a draft (`approval`).

---

## Layer 1: Strategic Intent (Global)
*Defined by `WorkflowStrategyAgent`*

This is the high-level "Why".
*   **`human_in_loop = true`**: The workflow requires human participation at some stage.
*   **`human_in_loop = false`**: The workflow is fully autonomous.

---

## Layer 2: UI Surface (The Interface)
*Defined by `WorkflowArchitectAgent`*

This defines the **Shape** of the component rendered to the user.

| UI Pattern | Display | Description |
| :--- | :--- | :--- |
| **`single_step`** | `inline` | A simple chat message, form, or button. One action. |
| **`two_step_confirmation`** | `artifact` | A preview card followed by a confirmation action. |
| **`multi_step`** | `artifact` | A multi-stage wizard, stepper, or iterative feedback loop. |

---

## Layer 3: Execution Mode (The Agent Behavior)
*Defined by `WorkflowImplementationAgent`*

This defines the **Blocking Behavior** of the agent runtime.

| Interaction Mode | Blocking? | Intent | Description |
| :--- | :--- | :--- | :--- |
| **`none`** | No | Autonomous | Agent runs tools and logic without stopping. |
| **`context`** | Soft Block | **Need Data** | Agent pauses because it needs information to proceed. (e.g., "What is your name?", "Fill out this wizard"). |
| **`approval`** | Hard Block | **Need Permission** | Agent pauses because it needs a decision/judgment. (e.g., "Is this draft okay?", "Deploy to prod?"). |

---

## Valid Combinations (Examples)

Since the layers are decoupled, many combinations are possible. Here are common archetypes:

### The "Wizard" (Data Gathering)
*   **UI**: `multi_step` (Complex Form)
*   **Mode**: `context` (Need Data)
*   **Scenario**: User fills out a 3-step onboarding questionnaire.

### The "Iterative Refiner" (Feedback Loop)
*   **UI**: `multi_step` (Draft -> Feedback -> Revision)
*   **Mode**: `approval` (Need Decision)
*   **Scenario**: Agent generates a blog post, user critiques it, agent revises (Loop).

### The "Gatekeeper" (Simple Permission)
*   **UI**: `single_step` (Approve/Reject Button)
*   **Mode**: `approval` (Need Permission)
*   **Scenario**: "Deploy to production?" [Yes/No].

### The "Co-Pilot" (Clarification)
*   **UI**: `single_step` (Chat Question)
*   **Mode**: `context` (Need Data)
*   **Scenario**: "I found two files. Which one do you want?"

### The "Reviewer" (Standard Approval)
*   **UI**: `two_step_confirmation` (Preview Card)
*   **Mode**: `approval` (Need Permission)
*   **Scenario**: "Here is the generated report. Please review and approve."

---

## Implementation Logic

### Generator Responsibility
1.  **Architect** picks the best **UI Pattern** for the *information density* (Simple = Single, Complex = Multi).
2.  **Implementation** picks the best **Interaction Mode** for the *agent's goal* (Gathering = Context, Verifying = Approval).

### Runtime Responsibility
*   If `mode == context`: Runtime waits for input, then passes it to the agent as a tool output.
*   If `mode == approval`: Runtime waits for decision. If "Reject", it may trigger a retry loop or handoff.

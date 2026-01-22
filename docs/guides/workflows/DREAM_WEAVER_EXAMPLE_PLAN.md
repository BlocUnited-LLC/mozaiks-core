# Dream Weaver - Example Use Case Plan

This document outlines the plan for adding "Dream Weaver" as a new example in `update_agent_state_pattern.py`. This example demonstrates a creative, multi-modal workflow with monetization gates (freemium model).

## 1. Concept Overview
**Name:** Dream Weaver
**Goal:** Visualize user dreams into video and provide a psychoanalytic interpretation.
**Monetization:** Video is free; deep psychoanalysis is premium (preview + gate).

## 2. Workflow Strategy (Layer 1)
**Trigger:** Chat (User says "I had a weird dream...")
**Pattern:** Pattern 3 (Creator Collective) or Pattern 1 (Pipeline). *Recommendation: Pattern 1 (Sequential Pipeline) fits best for a linear transformation process.*

### Phases
1.  **Phase 0: Dream Intake & Immersion**
    *   **Goal:** Extract key visual details, emotions, and narrative arc.
    *   **Human in Loop:** `true` (Interview).
    *   **Agents:** Single (Interviewer).
2.  **Phase 1: Visual Synthesis (Backend)**
    *   **Goal:** Translate narrative into high-fidelity video generation prompts and execute generation.
    *   **Human in Loop:** `false` (Automated).
    *   **Agents:** Sequential (Prompt Engineer -> Video Generator).
3.  **Phase 2: Psychoanalytic Interpretation (Backend)**
    *   **Goal:** Analyze symbols and themes (Jungian/Freudian) to generate a report.
    *   **Human in Loop:** `false` (Automated).
    *   **Agents:** Single (Analyst).
4.  **Phase 3: Delivery & Reveal**
    *   **Goal:** Present the video and the analysis teaser/full report based on tier.
    *   **Human in Loop:** `true` (User views artifacts).
    *   **Agents:** Single (Presenter).

## 3. Technical Blueprint (Layer 2 - Architect)

### Global Context Variables (Six-Type Taxonomy)
| Name | Type | Purpose |
| :--- | :--- | :--- |
| `dream_narrative` | `state` | The raw, detailed description of the user's dream. |
| `visual_prompts` | `computed` | Optimized prompts for the video generation model (Sora/Veo). |
| `generated_video_url` | `data_reference` | URL to the generated video asset. |
| `psychoanalysis_report` | `data_entity` | Structured object containing summary, symbolism, and deep analysis. |
| `user_subscription_tier` | `config` | User's status (free/premium) retrieved from environment/profile. |

### UI Components
| Phase | Agent | Tool | Component | Display | Interaction | Summary |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Phase 0** | `DreamInterviewer` | `confirm_dream_summary` | `DreamSummaryCard` | `inline` | `two_step_confirmation` | A small card summarizing the captured dream details for user confirmation after the chat interview. |
| **Phase 3** | `DreamPresenter` | `present_dream_video` | `VideoCinemaArtifact` | `artifact` | `single_step` | A video player artifact that plays the generated dream visualization. |
| **Phase 3** | `DreamPresenter` | `reveal_analysis` | `AnalysisGateArtifact` | `artifact` | `single_step` | Displays the analysis summary. If `user_subscription_tier` is 'free', shows a blurred 'Deep Dive' section with an upgrade button. |

## 4. Phase Agents (Layer 3 - Implementation)

### Phase 0: Dream Intake
*   **Agent:** `DreamCatcherAgent`
*   **Role:** Empathetic listener. Interviews the user via chat to extract details, then uses an inline tool to confirm understanding.
*   **Tools:** `confirm_dream_summary` (Inline UI).

### Phase 1: Visual Synthesis
*   **Agent:** `PromptAuteurAgent`
*   **Role:** Converts natural language into technical video prompts (camera angles, lighting, style).
*   **Tools:** `generate_video_prompt` (Internal).
*   **Agent:** `VideoForgeAgent`
*   **Role:** Interfaces with the video API.
*   **Tools:** `generate_video_asset` (Integration: Sora/Veo).

### Phase 2: Interpretation
*   **Agent:** `JungianAnalystAgent`
*   **Role:** Analyzes the dream narrative for archetypes and subconscious themes.
*   **Tools:** `generate_psychoanalysis` (Internal).

### Phase 3: Delivery
*   **Agent:** `DreamWeaverHost`
*   **Role:** Orchestrates the reveal. Checks subscription tier to determine which artifact state to render.
*   **Tools:** `present_dream_video` (Artifact), `reveal_analysis` (Artifact).

## 5. Example JSON Snippets (For `update_agent_state_pattern.py`)

### Architect Example (TechnicalBlueprint)
```json
{
  "TechnicalBlueprint": {
    "global_context_variables": [
      {
        "name": "dream_narrative",
        "type": "state",
        "purpose": "Captured user description of the dream",
        "trigger_hint": "Set when DreamCatcher completes interview"
      },
      {
        "name": "generated_video_url",
        "type": "data_reference",
        "purpose": "Link to the final rendered video",
        "trigger_hint": "Set when VideoForge completes generation"
      },
      {
        "name": "analysis_access_level",
        "type": "config",
        "purpose": "Determines if full analysis is visible",
        "trigger_hint": "Loaded from user profile at start"
      }
    ],
    "ui_components": [
      {
        "phase_name": "Phase 0 - Dream Intake",
        "agent": "DreamCatcher",
        "tool": "confirm_dream_summary",
        "label": "Confirm Dream Details",
        "component": "DreamSummaryCard",
        "display": "inline",
        "ui_pattern": "two_step_confirmation",
        "summary": "Agent displays a summary card of the dream details extracted from chat for user verification."
      },
      {
        "phase_name": "Phase 3 - Reveal",
        "agent": "DreamHost",
        "tool": "reveal_analysis",
        "label": "View Interpretation",
        "component": "AnalysisGateArtifact",
        "display": "artifact",
        "ui_pattern": "single_step",
        "summary": "Displays analysis summary with a blurred 'Deep Dive' section for free users."
      }
    ]
    // ... lifecycle hooks
  }
}
```

### Implementation Example (PhaseAgents)
```json
{
  "PhaseAgents": {
    "phase_agents": [
      {
        "phase_index": 0,
        "agents": [
          {
            "agent_name": "DreamCatcherAgent",
            "description": "Interviews the user via chat to extract visual and emotional details, then confirms understanding.",
            "human_interaction": "approval",
            "agent_tools": [
              {"name": "confirm_dream_summary", "integration": null, "purpose": "Show summary card for confirmation", "interaction_mode": "inline"}
            ]
          }
        ]
      },
      // ... other phases
      {
        "phase_index": 3,
        "agents": [
          {
            "agent_name": "DreamHostAgent",
            "description": "Presents the video and analysis, handling the paywall presentation.",
            "human_interaction": "none",
            "agent_tools": [
              {"name": "present_dream_video", "integration": "SoraAPI", "purpose": "Play video", "interaction_mode": "artifact"},
              {"name": "reveal_analysis", "integration": null, "purpose": "Show analysis report", "interaction_mode": "artifact"}
            ]
          }
        ]
      }
    ]
  }
}
```

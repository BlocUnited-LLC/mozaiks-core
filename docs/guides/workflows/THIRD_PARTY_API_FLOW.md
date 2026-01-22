# Third-Party API Flow in Generated Workflows

> ⚠️ **OUTDATED**: This document references legacy schemas (ActionPlan, ActionPlanArchitect) that have been replaced.
> For current schema definitions, see:
> - **Source of Truth**: `docs/ACTION_PLAN_SOURCE_OF_TRUTH.md`
> - **Schema Definitions**: `workflows/Generator/structured_outputs.json`
> 
> Current agent registry: InterviewAgent → PatternAgent → WorkflowStrategyAgent → WorkflowArchitectAgent → WorkflowImplementationAgent → file generators.

## Overview
This document explains how third-party vendor integrations (OpenAI, Slack, Stripe, etc.) flow through the Generator workflow from planning to implementation.

## Schema Structure (Simplified)

### ActionPlan Schema
```json
{
  "workflow": {
    "phases": [
      {
        "agents": [
          {
            "name": "ContentGenerator",
            "humanInLoop": false,
            "internal_tools": ["generate_content"],      // Our app functions
            "third_party_apis": ["OpenAI"]               // External vendors
          }
        ]
      }
    ]
  }
}
```

**Key Points:**
- `internal_tools` = snake_case tool names (our code)
- `third_party_apis` = PascalCase vendor names (external services)
- No `connectedTools` redundancy (removed for simplicity)

## Data Flow

### 1. Planning Stage
**Agent:** `ActionPlanArchitect`
- **Input:** User's automation goal
- **Output:** ActionPlan with phases/agents/tools/vendors
- **Example:**
  ```json
  {
    "name": "ContentGenerator",
    "internal_tools": ["generate_content"],
    "third_party_apis": ["OpenAI", "Sora"]
  }
  ```

### 2. Credential Collection Stage
**Agent:** `APIKeyAgent`
- **Input:** ActionPlan `third_party_apis` arrays
- **Logic:** Extract all unique vendors across all agents
  ```python
  vendors = set()
  for phase in action_plan.phases:
      for agent in phase.agents:
          vendors.update(agent.third_party_apis)
  ```
- **Output:** APIKeyRequest for each vendor (slack, openai, stripe, etc.)
- **Storage:** Credentials stored in environment/database per app

### 3. Context Variables Stage
**Agent:** `ContextVariablesAgent`
- **Input:** ActionPlan `third_party_apis` arrays
- **Logic:** Create context variables for vendor configs
  ```json
  {
    "slack_api_key": {
      "source": {"type": "environment", "env_var": "SLACK_API_KEY"}
    },
    "openai_config": {
      "source": {"type": "database", "collection": "VendorConfigs"}
    }
  }
  ```

### 4. Tool Manifest Stage
**Agent:** `ToolsManagerAgent`
- **Input:** ActionPlan `internal_tools` + `third_party_apis`
- **Logic:** 
  - `internal_tools` → tool entries (snake_case names)
  - `third_party_apis` → referenced in tool descriptions
- **Output:**
  ```json
  {
    "tool_name": "generate_content",
    "description": "Generate marketing copy via OpenAI API",
    "third_party_apis_used": ["OpenAI"]
  }
  ```

### 5. Tool Implementation Stage
**Agent:** `AgentToolsFileGenerator`
- **Input:** Tool manifest + ActionPlan `third_party_apis`
- **Logic:** Generate tool stubs with vendor SDK integration guidance
- **Output:** Python modules with vendor-specific logic
  ```python
  async def generate_content(prompt: str, **runtime):
      """Generate content using OpenAI API.
      
      Third-party APIs: OpenAI
      Required credentials: OPENAI_API_KEY (from environment)
      """
      # Agent will implement using OpenAI SDK knowledge
      openai_client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))
      response = await openai_client.completions.create(...)
      return {"status": "success", "content": response.choices[0].text}
  ```

## Why This Matters

### For Credential Collection
- APIKeyAgent scans `third_party_apis` to know which keys to request
- Converts PascalCase vendor names to snake_case service identifiers
- Example: `GoogleSheets` → `google_sheets` → env var `GOOGLE_SHEETS_API_KEY`

### For Tool Generation
- AgentToolsFileGenerator knows which vendor SDKs to reference
- System message tells the LLM: "Use your knowledge base for {vendor} API best practices"
- Example: If `third_party_apis: ["Stripe"]`, the tool stub includes Stripe SDK patterns

### For Runtime
- Context variables load vendor credentials per app
- Tools access credentials via environment or context injection
- Multi-tenant: each app has isolated vendor configs

## Example End-to-End Flow

### User Input
"Automate my marketing with AI-generated content and social media posting"

### Generated ActionPlan
```json
{
  "workflow": {
    "phases": [
      {
        "name": "Generation",
        "agents": [
          {
            "name": "ContentGenerator",
            "internal_tools": ["generate_content"],
            "third_party_apis": ["OpenAI"]
          },
          {
            "name": "SocialPublisher",
            "internal_tools": ["post_to_social"],
            "third_party_apis": ["Blotato"]
          }
        ]
      }
    ]
  }
}
```

### Credential Collection
- APIKeyAgent requests: `openai` key, `blotato` key
- User provides both
- Stored in DB per app

### Tool Generation
- `generate_content.py` → includes OpenAI SDK usage
- `post_to_social.py` → includes Blotato API calls
- Both tools access credentials via environment

### Runtime Execution
- User triggers workflow
- ContentGenerator calls `generate_content` → uses OpenAI key
- SocialPublisher calls `post_to_social` → uses Blotato key
- All isolated per app/user

## Schema Change Summary

### Before (Complex)
```json
{
  "internal_tools": ["tool1"],
  "third_party_apis": ["Vendor1"],
  "connectedTools": [                    // ❌ REDUNDANT
    {"name": "tool1", "purpose": "..."},
    {"name": "Vendor1", "purpose": "..."}
  ]
}
```

### After (Simple)
```json
{
  "internal_tools": ["tool1"],
  "third_party_apis": ["Vendor1"]        // ✅ CLEAN SEPARATION
}
```

**Benefits:**
- 53% fewer structured fields
- Clear semantic separation (our tools vs their APIs)
- Easier for LLM to generate correctly
- Downstream agents have single source of truth

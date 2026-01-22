# WorkflowArchitectAgent Pattern-Specific Examples

## Purpose
The `inject_workflow_architect_guidance` function should provide:
1. **Technical Requirements** (what tools/context/lifecycle ops are needed per pattern)
2. **Complete Example** (exact JSON output format the agent should produce)

Currently it only provides #1. We need to add #2.

---

## Example Structure Per Pattern

Each pattern needs a complete example showing the **exact JSON output** format:

```json
{
    "phase_technical_requirements": [
        {
            "phase_index": 0,
            "phase_name": "Phase 1: Intent Analysis and Domain Routing",
            "required_tools": [...],
            "required_context_variables": [...],
            "required_lifecycle_operations": [...]
        }
    ],
    "shared_requirements": {
        "workflow_context_variables": [...],
        "shared_tools": [...],
        "typical_integrations": [...]
    },
    "agent_message": "Technical blueprint generated for 3 phases"
}
```

---

## Pattern 1: Context-Aware Routing (Example)

**Workflow Context:** Customer Support Router (from WorkflowStrategyAgent example)

**Complete Example Output:**
```json
{
    "phase_technical_requirements": [
        {
            "phase_index": 0,
            "phase_name": "Phase 1: Intent Analysis and Domain Routing",
            "required_tools": [
                {
                    "tool_name": "analyze_request",
                    "tool_type": "Agent_Tool",
                    "scope": "shared",
                    "description": "Analyze incoming support request to determine domain classification and confidence level",
                    "parameters": {
                        "request_text": "string",
                        "previous_context": "optional[string]"
                    },
                    "returns": {
                        "domain": "string",
                        "confidence": "float",
                        "clarification_needed": "boolean"
                    }
                },
                {
                    "tool_name": "route_to_technical_support",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Route request to technical support specialist",
                    "parameters": {
                        "request_summary": "string",
                        "detected_issue": "string"
                    }
                },
                {
                    "tool_name": "route_to_billing_support",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Route request to billing support specialist",
                    "parameters": {
                        "request_summary": "string",
                        "account_context": "string"
                    }
                },
                {
                    "tool_name": "route_to_policy_guidance",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Route request to policy guidance specialist",
                    "parameters": {
                        "request_summary": "string",
                        "policy_area": "string"
                    }
                },
                {
                    "tool_name": "request_clarification",
                    "tool_type": "Agent_Tool",
                    "scope": "shared",
                    "description": "Request user clarification when domain confidence is below threshold",
                    "parameters": {
                        "clarification_question": "string",
                        "context_summary": "string"
                    }
                }
            ],
            "required_context_variables": [
                {
                    "variable_name": "routing_started",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Set to true when RouterAgent begins analysis",
                    "trigger_condition": "agent emits 'ANALYZING' keyword",
                    "default_value": false
                },
                {
                    "variable_name": "current_domain",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Set to domain name when RouterAgent classifies request",
                    "trigger_condition": "agent emits domain keyword (technical_support, billing_support, policy_guidance)",
                    "default_value": null
                },
                {
                    "variable_name": "domain_confidence",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Confidence score (0.0-1.0) from classification",
                    "trigger_condition": "agent emits 'CONFIDENCE: N' where N is float",
                    "default_value": 0.0
                },
                {
                    "variable_name": "previous_domains",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Array of previously visited domains for tracking routing history",
                    "trigger_condition": "append to array when domain changes",
                    "default_value": []
                },
                {
                    "variable_name": "request_count",
                    "variable_type": "derived",
                    "trigger_method": "ui_response",
                    "description": "Number of requests processed (incremented per routing cycle)",
                    "trigger_condition": "user submits new request via UI",
                    "default_value": 0
                }
            ],
            "required_lifecycle_operations": []
        },
        {
            "phase_index": 1,
            "phase_name": "Phase 2: Domain Specialist Resolution",
            "required_tools": [
                {
                    "tool_name": "provide_technical_response",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Submit technical support resolution with troubleshooting steps",
                    "parameters": {
                        "resolution": "string",
                        "followup_required": "boolean",
                        "escalation_needed": "boolean"
                    }
                },
                {
                    "tool_name": "provide_billing_response",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Submit billing support resolution with account details",
                    "parameters": {
                        "resolution": "string",
                        "refund_processed": "boolean",
                        "followup_required": "boolean"
                    }
                },
                {
                    "tool_name": "provide_policy_response",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Submit policy guidance with relevant documentation links",
                    "parameters": {
                        "resolution": "string",
                        "policy_references": "array[string]",
                        "followup_required": "boolean"
                    }
                }
            ],
            "required_context_variables": [
                {
                    "variable_name": "question_responses",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Dict mapping domains to specialist responses",
                    "trigger_condition": "specialist emits structured response",
                    "default_value": {}
                },
                {
                    "variable_name": "question_answered",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Boolean flag indicating specialist has completed answer",
                    "trigger_condition": "specialist emits 'RESPONSE_READY'",
                    "default_value": false
                }
            ],
            "required_lifecycle_operations": []
        },
        {
            "phase_index": 2,
            "phase_name": "Phase 3: Response Consolidation and Follow-Up",
            "required_tools": [
                {
                    "tool_name": "consolidate_response",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Synthesize specialist response with follow-up tasks and next steps",
                    "parameters": {
                        "specialist_response": "string",
                        "followup_tasks": "array[string]",
                        "next_steps": "string"
                    }
                }
            ],
            "required_context_variables": [],
            "required_lifecycle_operations": [
                {
                    "operation_name": "Log Routing Decision",
                    "trigger": "after_chat",
                    "target": null,
                    "description": "Persist routing metadata (domain, confidence, resolution time) for analytics and future training"
                }
            ]
        }
    ],
    "shared_requirements": {
        "workflow_context_variables": [
            {
                "variable_name": "workflow_name",
                "variable_type": "static",
                "description": "Name of the workflow for identification",
                "default_value": "Customer Support Router"
            },
            {
                "variable_name": "user_id",
                "variable_type": "database",
                "description": "Current user ID for multi-tenant isolation and tracking",
                "default_value": null
            },
            {
                "variable_name": "app_id",
                "variable_type": "database",
                "description": "Current app ID for billing and token tracking",
                "default_value": null
            }
        ],
        "shared_tools": [
            {
                "tool_name": "validate_input",
                "tool_type": "Shared_Tool",
                "description": "Generic input validation for all phases",
                "parameters": {
                    "input_text": "string",
                    "validation_rules": "array[string]"
                }
            },
            {
                "tool_name": "log_routing_decision",
                "tool_type": "Shared_Tool",
                "description": "Log domain classification decisions for analytics",
                "parameters": {
                    "domain": "string",
                    "confidence": "float",
                    "timestamp": "datetime"
                }
            }
        ],
        "typical_integrations": []
    },
    "agent_message": "Technical blueprint generated for 3 phases (Intent Analysis, Domain Specialist Resolution, Response Consolidation) with routing tools, confidence tracking, and specialist handoffs"
}
```

---

## Pattern 6: Pipeline (Example)

**Workflow Context:** Order Fulfillment Pipeline (from WorkflowStrategyAgent example)

**Complete Example Output:**
```json
{
    "phase_technical_requirements": [
        {
            "phase_index": 0,
            "phase_name": "Phase 1: Order Validation",
            "required_tools": [
                {
                    "tool_name": "start_order_processing",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Initiate order processing pipeline with customer order details",
                    "parameters": {
                        "order_id": "string",
                        "customer_id": "string",
                        "order_items": "array[object]"
                    }
                },
                {
                    "tool_name": "run_validation_check",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Validate order completeness, customer details, and promotion eligibility",
                    "parameters": {
                        "order_data": "object",
                        "validation_rules": "array[string]"
                    },
                    "returns": {
                        "is_valid": "boolean",
                        "validation_errors": "array[string]",
                        "warnings": "array[string]"
                    }
                },
                {
                    "tool_name": "complete_validation",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Mark validation stage complete with success/failure status",
                    "parameters": {
                        "validation_result": "object",
                        "proceed_to_next_stage": "boolean"
                    }
                }
            ],
            "required_context_variables": [
                {
                    "variable_name": "pipeline_started",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Boolean indicating pipeline has been initiated",
                    "trigger_condition": "EntryAgent emits 'PIPELINE_START'",
                    "default_value": false
                },
                {
                    "variable_name": "validation_stage_completed",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Boolean indicating validation passed successfully",
                    "trigger_condition": "ValidationAgent emits 'VALIDATION_COMPLETE: true'",
                    "default_value": false
                },
                {
                    "variable_name": "validation_results",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "ValidationResult object with is_valid, error_message, and details",
                    "trigger_condition": "ValidationAgent emits structured validation result",
                    "default_value": null
                },
                {
                    "variable_name": "has_error",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Boolean indicating a pipeline error occurred (triggers early termination)",
                    "trigger_condition": "any agent emits 'ERROR' or validation fails",
                    "default_value": false
                },
                {
                    "variable_name": "error_stage",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Stage name where error occurred (for debugging and user messaging)",
                    "trigger_condition": "set when has_error becomes true",
                    "default_value": null
                }
            ],
            "required_lifecycle_operations": [
                {
                    "operation_name": "Initialize Pipeline State",
                    "trigger": "before_chat",
                    "target": null,
                    "description": "Set all stage_completed flags to false, has_error to false, and initialize stage tracking"
                }
            ]
        },
        {
            "phase_index": 1,
            "phase_name": "Phase 2: Inventory Confirmation",
            "required_tools": [
                {
                    "tool_name": "run_inventory_check",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Check stock levels across warehouses and reserve items",
                    "parameters": {
                        "order_items": "array[object]",
                        "preferred_warehouse": "optional[string]"
                    },
                    "returns": {
                        "items_available": "boolean",
                        "backorder_items": "array[object]",
                        "reservation_ids": "array[string]"
                    }
                },
                {
                    "tool_name": "complete_inventory_check",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Mark inventory stage complete with reservation confirmations",
                    "parameters": {
                        "inventory_result": "object",
                        "proceed_to_payment": "boolean"
                    }
                }
            ],
            "required_context_variables": [
                {
                    "variable_name": "inventory_stage_completed",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Boolean indicating inventory confirmed successfully",
                    "trigger_condition": "InventoryAgent emits 'INVENTORY_COMPLETE: true'",
                    "default_value": false
                },
                {
                    "variable_name": "inventory_results",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "InventoryResult object with availability status and reservation IDs",
                    "trigger_condition": "InventoryAgent emits structured inventory result",
                    "default_value": null
                }
            ],
            "required_lifecycle_operations": [
                {
                    "operation_name": "Validate Stage Prerequisites",
                    "trigger": "before_agent",
                    "target": "InventoryAgent",
                    "description": "Ensure validation_stage_completed is true before proceeding to inventory check"
                },
                {
                    "operation_name": "Early Termination Check",
                    "trigger": "before_agent",
                    "target": null,
                    "description": "If has_error=true, terminate pipeline and return error message to user"
                }
            ]
        },
        {
            "phase_index": 2,
            "phase_name": "Phase 3: Payment Processing",
            "required_tools": [
                {
                    "tool_name": "run_payment_check",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Charge payment method, apply taxes, and record transaction receipts",
                    "parameters": {
                        "payment_method": "object",
                        "amount": "float",
                        "tax_details": "object"
                    },
                    "returns": {
                        "payment_successful": "boolean",
                        "transaction_id": "string",
                        "receipt_url": "string"
                    }
                },
                {
                    "tool_name": "complete_payment_check",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Mark payment stage complete with transaction confirmation",
                    "parameters": {
                        "payment_result": "object",
                        "proceed_to_fulfillment": "boolean"
                    }
                }
            ],
            "required_context_variables": [
                {
                    "variable_name": "payment_stage_completed",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Boolean indicating payment processed successfully",
                    "trigger_condition": "PaymentAgent emits 'PAYMENT_COMPLETE: true'",
                    "default_value": false
                },
                {
                    "variable_name": "payment_results",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "PaymentResult object with transaction ID and receipt details",
                    "trigger_condition": "PaymentAgent emits structured payment result",
                    "default_value": null
                }
            ],
            "required_lifecycle_operations": [
                {
                    "operation_name": "Fraud Screening",
                    "trigger": "before_agent",
                    "target": "PaymentAgent",
                    "description": "Run fraud detection checks before initiating payment processing (from lifecycle_operations in workflow_strategy)"
                },
                {
                    "operation_name": "Validate Stage Prerequisites",
                    "trigger": "before_agent",
                    "target": "PaymentAgent",
                    "description": "Ensure inventory_stage_completed is true before processing payment"
                }
            ]
        },
        {
            "phase_index": 3,
            "phase_name": "Phase 4: Fulfillment and Notification",
            "required_tools": [
                {
                    "tool_name": "schedule_shipment",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Schedule shipment, generate tracking info, and send confirmation email",
                    "parameters": {
                        "order_id": "string",
                        "shipping_address": "object",
                        "shipping_method": "string"
                    },
                    "returns": {
                        "tracking_number": "string",
                        "estimated_delivery": "datetime",
                        "carrier": "string"
                    }
                },
                {
                    "tool_name": "complete_pipeline",
                    "tool_type": "Agent_Tool",
                    "scope": "phase_specific",
                    "description": "Mark entire pipeline complete with final order status",
                    "parameters": {
                        "fulfillment_result": "object",
                        "order_complete": "boolean"
                    }
                }
            ],
            "required_context_variables": [
                {
                    "variable_name": "pipeline_completed",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Boolean indicating all pipeline stages passed successfully",
                    "trigger_condition": "FulfillmentAgent emits 'PIPELINE_COMPLETE: true'",
                    "default_value": false
                },
                {
                    "variable_name": "final_results",
                    "variable_type": "derived",
                    "trigger_method": "agent_text",
                    "description": "Aggregated pipeline results with tracking info and order summary",
                    "trigger_condition": "FulfillmentAgent emits structured final result",
                    "default_value": null
                }
            ],
            "required_lifecycle_operations": [
                {
                    "operation_name": "Validate Stage Prerequisites",
                    "trigger": "before_agent",
                    "target": "FulfillmentAgent",
                    "description": "Ensure payment_stage_completed is true before scheduling fulfillment"
                },
                {
                    "operation_name": "Cleanup Resources",
                    "trigger": "after_chat",
                    "target": null,
                    "description": "Release any held inventory reservations and close pipeline session"
                }
            ]
        }
    ],
    "shared_requirements": {
        "workflow_context_variables": [
            {
                "variable_name": "stage_count",
                "variable_type": "static",
                "description": "Total number of pipeline stages",
                "default_value": 4
            },
            {
                "variable_name": "current_stage_index",
                "variable_type": "derived",
                "description": "Current stage being processed (0-indexed)",
                "default_value": 0
            },
            {
                "variable_name": "error_handling_strategy",
                "variable_type": "static",
                "description": "How to handle stage failures (terminate or retry)",
                "default_value": "terminate"
            }
        ],
        "shared_tools": [
            {
                "tool_name": "validate_stage_result",
                "tool_type": "Shared_Tool",
                "description": "Validate that stage result object has required fields and valid data",
                "parameters": {
                    "stage_result": "object",
                    "required_fields": "array[string]"
                }
            },
            {
                "tool_name": "format_pipeline_summary",
                "tool_type": "Shared_Tool",
                "description": "Format final pipeline results summary for user display",
                "parameters": {
                    "all_stage_results": "array[object]",
                    "format": "string"
                }
            }
        ],
        "typical_integrations": [
            {
                "integration_name": "Stripe Payment API",
                "purpose": "Process credit card payments in Phase 3",
                "required_credentials": ["stripe_api_key", "stripe_secret_key"]
            },
            {
                "integration_name": "SendGrid Email API",
                "purpose": "Send order confirmation emails in Phase 4",
                "required_credentials": ["sendgrid_api_key"]
            },
            {
                "integration_name": "Inventory Management System",
                "purpose": "Check stock levels and reserve items in Phase 2",
                "required_credentials": ["inventory_api_endpoint", "inventory_api_token"]
            }
        ]
    },
    "agent_message": "Technical blueprint generated for 4 sequential pipeline stages (Order Validation, Inventory Confirmation, Payment Processing, Fulfillment) with stage prerequisite validation, error handling, and external API integrations for payment and notifications"
}
```

---

## Key Differences from WorkflowStrategyAgent Examples

### WorkflowStrategyAgent provides:
- **Strategic phase structure** (phase names, descriptions, purposes)
- **High-level coordination** (approval points, agents_needed)
- **Business logic** (workflow triggers, strategy notes)

### WorkflowArchitectAgent provides:
- **Technical implementation details** (exact tool schemas with parameters/returns)
- **Context variable triggers** (how runtime detects state changes)
- **Lifecycle operation specs** (when hooks fire, what they do)
- **External integrations** (APIs, credentials, purposes)

---

## Implementation Plan

Add `architect_examples` dict to `inject_workflow_architect_guidance` similar to `strategy_examples`:

```python
architect_examples = {
    1: """<Pattern 1 complete JSON from above>""",
    2: """<Pattern 2 complete JSON>""",
    3: """<Pattern 3 complete JSON>""",
    # ... etc for all 9 patterns
}
```

Then inject after the requirements section:

```python
example = architect_examples.get(pattern_id)
if example:
    guidance += f"\n**Pattern-Specific Example:**\n```json\n{example}\n```\n"
```

This ensures WorkflowArchitectAgent sees:
1. **What** technical requirements are needed (current requirements text)
2. **How** to format the output (complete JSON example)

Same pattern as WorkflowStrategyAgent: Requirements → Example

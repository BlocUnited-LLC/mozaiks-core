# Why Dynamic Extraction Matters: 2 Examples

You asked for examples to see the value of the new `$1` dynamic extraction feature. Here is the difference between the "Old Way" (Static) and the "New Way" (Dynamic).

## Example 1: The "50 Departments" Problem (Routing)

Imagine you are building a Customer Support bot that needs to route users to the correct department. There are 50 possible departments (Billing, Tech Support, Sales, Returns, etc.).

### ❌ The Old Way (Static)
You would have to write **50 separate rules** in your configuration. If you added a new department, you had to update the code.

```json
// You needed a separate rule for EVERY possibility
"transitions": [
  { "match": { "equals": "ROUTE_TO_BILLING" }, "new_value": "billing" },
  { "match": { "equals": "ROUTE_TO_SALES" },   "new_value": "sales" },
  { "match": { "equals": "ROUTE_TO_TECH" },    "new_value": "tech_support" }
  // ... plus 47 more blocks of code ...
]
```
*   **The Pain**: Huge, messy configuration files. If the AI invented a new valid category, the system broke.

### ✅ The New Way (Dynamic)
You write **1 single rule**. It handles 5 departments or 5,000 departments automatically.

```json
// One rule handles everything
"transitions": [
  { 
    "match": { "regex": "ROUTING_DECISION: (.*)" }, 
    "new_value": "$1" 
  }
]
```
*   **The Value**: 
    *   If the Agent says `ROUTING_DECISION: billing`, the variable becomes `billing`.
    *   If the Agent says `ROUTING_DECISION: legal`, the variable becomes `legal`.
    *   **Zero extra code** for new departments.

---

## Example 2: Saving the "Why" (Data Capture)

Imagine a Manager Agent rejects a budget request. You want to save the **reason** for the rejection into the database so the user knows what to fix.

### ❌ The Old Way (Static)
The runtime could only detect *that* it happened, not *what* was said.

*   Agent says: `REJECTED: Budget is too high`
*   Runtime sees: "Oh, the word REJECTED is there."
*   Variable set: `status = "rejected"`
*   **Lost Data**: The reason ("Budget is too high") is lost. The runtime couldn't "grab" it to put it in a variable. You would have to write a custom Python tool just to parse that string.

### ✅ The New Way (Dynamic)
The runtime can now "grab" the text and save it.

*   **Agent says**: `REJECTED: Budget is too high`
*   **Rule**: `match: "REJECTED: (.*)"`, `new_value: "$1"`
*   **Result**: 
    *   Variable `status` = `rejected`
    *   Variable `rejection_reason` = `Budget is too high`

*   **The Value**: You can now capture **dynamic business data** (reasons, names, categories, IDs) purely through conversation, without writing custom tools.

## Summary
1.  **Scalability**: Handle infinite categories with 1 rule instead of 100.
2.  **Simplicity**: No need to write custom Python tools just to "read" what the agent said.

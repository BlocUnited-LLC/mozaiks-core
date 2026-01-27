# MozaiksAI Runtime & Workflow Model (Source of Truth)

## Purpose

This document defines the execution semantics, workflow model, and runtime guarantees of MozaiksAI.

It exists to answer:

How workflows are structured, loaded, executed, and resumed — independent of who invokes them.

This document applies equally to:
- hosted runtimes
- self-hosted runtimes
- first-party (Mozaiks) usage
- customer and third-party usage

---

## Runtime Responsibility (Restated)

MozaiksAI is a pure execution engine.

It is responsible for:
- executing declarative workflows
- coordinating agents
- invoking tools
- emitting events
- persisting execution state

It is not responsible for:
- deciding which workflow to run
- deciding who is allowed to run it
- enforcing subscriptions or pricing
- interpreting business intent

---

## Workflow Definition Model

A workflow is a declarative, versioned unit of execution.

Each workflow lives under:

`workflows/{workflow_id}/`

and is composed of configuration files plus optional code and UI artifacts.

---

## Core Workflow Files

### `agents.json`
Defines the agents participating in the workflow:
- agent roles
- system prompts
- model configuration
- allowed tools

### `tools.json`
Declares tools that agents may invoke:
- tool names
- argument schemas
- descriptions

### `handoffs.json`
Defines allowed agent-to-agent transitions:
- explicit edges
- enforced at runtime

### `hooks.json`
Defines lifecycle hooks for agent behavior modification:
- hook types (`update_agent_state`, `process_message_before_send`, etc.)
- target agents (specific agent or `all`)
- Python function references

### `orchestrator.json`
Defines workflow-level execution behavior:
- entry agent
- termination conditions
- retries
- parallelism

### `structured_outputs.json`
Optional schemas for structured outputs.

### `context_variables.json`
Workflow-scoped context configuration:
- literals
- environment variables
- file-backed values (restricted by default)

### `ui_config.json`  
Optional UI surface declarations.

---

## Tool Implementations

Tool code lives under:

`workflows/{workflow_id}/tools/`  

Rules:
- arguments are schema validated
- errors propagate as execution events
- runtime does not infer semantics

---

## Workflow UI Artifacts

Optional workflow-specific UI components live under:

`ChatUI/src/workflows/{workflow_id}/components/` 

If absent, generic artifact rendering is used.

---

## Workflow Packs (Macro-Orchestration)

Workflow packs define relationships between workflows.

They live under:

`workflows/_pack/` 

`workflow_graph.json` defines macro dependencies.

A workflow becomes eligible when all parent workflows have completed.

---

## Execution Lifecycle

1. Execution is requested externally
2. Runtime authenticates token
3. Workflow is loaded by workflow_id
4. chat_id is created or resumed
5. Agents execute per orchestration rules
6. Tools are invoked
7. Events stream via WebSocket
8. State is persisted
9. Workflow completes or pauses

---

## Authentication vs Authorization

The runtime authenticates execution tokens.
Authorization must occur before execution.

---

## Runtime Guarantees

MozaiksAI guarantees:
- deterministic orchestration
- schema-validated tool calls
- isolated execution
- recoverable state

It does not guarantee:
- business correctness
- outcome success
- idempotent side effects

---

## One-Sentence Summary

MozaiksAI executes declarative workflows deterministically, safely, and without business context.

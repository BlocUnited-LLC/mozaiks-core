# Mozaiks System Overview (Source of Truth)

## Purpose

This document defines the **core systems** that make up the Mozaiks platform and how they relate.

It exists to answer one question only:

> **What exists, who owns what, and how do the pieces interact?**

Detailed repository structure, runtime behavior, workflow specs, and UI boundaries are defined in separate documents.

---

## Core Systems

Mozaiks is composed of **five distinct systems**, each with a clear responsibility boundary.

### 1. MozaiksAI — Execution Runtime (Open Source)

**Role:** Execute agentic workflows.

MozaiksAI is a **neutral, embeddable runtime** responsible for:

- Workflow execution
- Agent orchestration
- Tool invocation
- Artifact rendering
- Chat-based interaction (ChatUI)
- WebSocket + HTTP transport
- Workflow state persistence

MozaiksAI **does not own**:
- Users or organizations
- Billing or subscriptions
- Authorization decisions
- Business logic or growth logic

MozaiksAI executes workflows **only when instructed** by an external control plane.

It does not decide *what* should run or *who* is allowed to run it.

---

### 2. Mozaiks Backend — Control Plane (Closed, Hosted Only)

**Role:** Business authority and platform moat.

The Mozaiks backend (implemented in .NET) is the **single source of truth** for:

- Users, orgs, and apps
- Subscriptions and entitlements
- Capability registry (what workflows exist and who can run them)
- KPI ingestion and aggregation
- Investor network and marketing systems
- Admin tooling and governance

The backend:
- Authorizes execution
- Issues execution tokens
- Brokers access to hosted runtimes
- Never executes workflows itself

This system is **not open source** and is **not self-hosted**.

---

### 3. Mozaiks Frontend — First-Party Client

**Role:** Mozaiks’ own product UI.

The Mozaiks frontend (React) is a **direct client of MozaiksAI**, mediated by the Mozaiks backend.

It:
- Calls the backend to request execution
- Receives execution authorization and runtime connection details
- Connects directly to MozaiksAI via WebSocket
- Renders ChatUI and workflow-specific artifacts

Important:
> **Mozaiks (the product) does NOT use MozaiksCore.**

Mozaiks is simply one client of MozaiksAI.

---

### 4. MozaiksCore — Product Foundation for Customer Apps (Open Source)

**Role:** App shell and control-plane adapter for customer-built apps.

MozaiksCore is an **open-source product foundation** intended for *customers*, not for Mozaiks itself.

It provides:
- Product shell (routing, navigation, theming)
- Auth adapters
- Billing adapters
- Plugin host system
- Function Plugin infrastructure
- Session broker to MozaiksAI

MozaiksCore:
- Embeds or connects to MozaiksAI
- Authorizes execution on behalf of customer apps
- Does not contain runtime logic itself

Customer apps built on MozaiksCore use it as their control plane.

---

### 5. Generators (AgentGenerator, AppGenerator, Foundry) — Intelligence Layer (Closed)

**Role:** Intelligence, leverage, and acceleration.

Generators are **proprietary AI Plugins** that run *on top of* MozaiksAI.

They are responsible for:
- Generating workflows and agents
- Generating product pages, APIs, and plugins
- Encoding best practices and platform knowledge

Generators are:
- Subscription-gated
- Not committed to open-source repos
- Loaded into runtimes only via authorized channels

They are the **primary monetization surface**, but not the platform moat by themselves.

---

## Execution Model (Canonical)

MozaiksAI executes workflows, but **never authorizes them**.

Authorization always happens **before execution** in a host control plane.

### First-Party (Mozaiks)

Mozaiks Frontend
→ Mozaiks Backend (authorize capability)
→ MozaiksAI Runtime (execute workflow)

### Customer App

Customer UI
→ MozaiksCore (authorize capability)
→ MozaiksAI Runtime (execute workflow)


MozaiksAI treats all callers the same:
- It validates tokens
- It executes workflows
- It emits events

It does not care who the caller is.

---

## Capability Model (Key Concept)

Workflows are not discovered dynamically by the backend.

Instead:
- Workflows are registered as **capabilities** in the control plane
- Capabilities map to workflow IDs
- The control plane decides which capability a user is allowed to invoke

Example (conceptual):

Capability: "agent_generator"
→ Workflow ID: "AgentGenerator"
→ Gated: true
→ Required Plan: Pro


The runtime never knows or cares that a workflow is “paid”.

---

## Deployment Modes

MozaiksAI is packaged once but deployed in multiple contexts.

### Hosted Runtime (Mozaiks-Managed)

- Used by Mozaiks (first-party)
- Used by MozaiksCore-based customer apps
- Control plane authorizes access
- Runtime executes workflows

### Self-Hosted Runtime (Customer-Operated)

- Customers run MozaiksAI themselves
- They provide their own workflows and control plane
- Mozaiks services (capital, marketing, network) are optional integrations

The runtime behavior is identical in both modes.

---

## Non-Goals of This Document

This document intentionally does **not** define:

- Repository file structures
- Workflow JSON schemas
- UI ownership rules
- Billing mechanics
- Generator internals

Those are defined in separate, narrower documents.

---

## One-Sentence Mental Model

> **MozaiksAI executes.  
> Control planes decide.  
> Generators accelerate.  
> Mozaiks monetizes outcomes and networks — not execution.**
# Repository Boundaries (Source of Truth)

## Purpose

This document defines **which repository owns which responsibilities** in the Mozaiks platform.

It exists to prevent:
- architectural drift
- duplicated logic
- accidental coupling
- leakage of gated functionality into open-source code

If a change violates this document, the change is incorrect.

---

## Canonical Repositories

The Mozaiks platform currently consists of **four repositories**.

Each repository has a **single primary responsibility**.

---

## 1. `mozaiksai` — Execution Runtime (Open Source)

### Role

`mozaiksai` is the **agentic execution runtime**.

It is responsible for executing workflows and rendering agent-driven UI.

---

### Owns

This repository **owns**:

- Workflow execution engine
- Agent orchestration
- Tool invocation
- ChatUI (messages, artifacts, widgets)
- WebSocket + HTTP transport
- Workflow persistence and recovery
- Pack and workflow loading
- Schema validation for workflows and tools

---

### Must Support

- Hosted SaaS deployments
- Self-hosted deployments
- Multiple clients (Mozaiks frontend, MozaiksCore apps, third-party shells)
- Dynamic workflow loading

---

### Explicitly Does NOT Own

`mozaiksai` must **never** contain:

- User or org management
- Subscription or billing logic
- Capability gating
- Admin UI
- Investor, marketing, or growth logic
- Proprietary generators (AgentGenerator, AppGenerator, Foundry)

---

### Workflow Policy

- The open-source repository must **not** ship proprietary workflows.
- Only example or demo workflows may exist in the repo.
- Proprietary workflows are loaded at runtime via private channels.

---

### Decision Rule

If logic answers:
> “How does an agent or workflow execute?”

It belongs here.

If it answers:
> “Who is allowed to run this?”

It does **not**.

---

## 2. `mozaiks` — First-Party Product (Closed)

### Role

This repository implements **Mozaiks the product**.

It is a **first-party client** of MozaiksAI.

---

### Owns

- Mozaiks React frontend
- Mozaiks backend (.NET)
- Subscription and entitlement logic
- Capability registry
- Admin portal
- API key management
- Generator access
- KPI ingestion
- Investor and marketing systems

---

### Responsibilities

- Decide which workflows exist as capabilities
- Decide who can run which workflows
- Issue execution tokens
- Provide runtime connection details to the frontend
- Operate hosted MozaiksAI runtimes

---

### Explicitly Does NOT Own

- Workflow execution logic
- Agent orchestration
- Chat rendering logic
- Runtime state management

Those belong to `mozaiksai`.

---

### Decision Rule

If logic answers:
> “Is the user allowed to do this, and why?”

It belongs here.

---

## 3. `mozaikscore` — Product Foundation for Customer Apps (Open Source)

### Role

`mozaikscore` is a **product foundation** that customers can use to build apps powered by MozaiksAI.

Mozaiks itself does **not** depend on this repository.

---

### Owns

- Product shell (routing, navigation, layout)
- Auth adapters
- Billing adapters
- Plugin host system
- Function Plugin infrastructure
- Session broker to MozaiksAI
- Theming and branding system

---

### Responsibilities

- Act as a control plane for customer apps
- Authorize workflow execution for customer users
- Bridge customer UI to MozaiksAI runtime
- Launch AI Plugin workflows from product pages

---

### Explicitly Does NOT Own

- Workflow execution logic
- Agent definitions
- Generator intelligence
- Proprietary workflows
- Platform-level monetization logic

---

### Decision Rule

If logic answers:
> “How does a customer app look and behave as a product?”

It belongs here.

If it answers:
> “How does an agent reason or execute?”

It does not.

---

## 4. Generators (AgentGenerator, AppGenerator, Foundry) — Intelligence Layer (Closed)

### Role

Generators are **proprietary AI Plugins** that run on top of MozaiksAI.

They encode platform intelligence and best practices.

---

### Owns

- Workflow generation logic
- Agent architecture intelligence
- App and plugin scaffolding
- Governance and audit workflows
- Foundry meta-workflows

---

### Distribution Rules

- Generators are **not committed** to open-source repos
- They are loaded into runtimes via authorized, private channels
- Access is controlled by a control plane (Mozaiks backend)

---

### Decision Rule

If logic answers:
> “What should be built, and how should it be structured?”

It belongs here.

---

## Cross-Repository Rules (Strict)

The following rules apply globally:

### 1. No Cross-Cutting Logic

- Runtime does not gate access
- Control planes do not execute workflows
- Generators do not own product UI

---

### 2. Authorization Always Happens Outside the Runtime

- MozaiksAI authenticates tokens
- Authorization decisions are made by:
  - Mozaiks backend (first-party)
  - MozaiksCore or customer backend (customer apps)

---

### 3. Frontends Never Choose Workflows Directly

- Frontends request **capabilities**
- Control planes resolve capabilities to workflow IDs
- Runtimes execute what they are told

---

### 4. Self-Hosting Safety Rule

All open-source repositories must:
- function without Mozaiks services
- avoid hardcoded SaaS assumptions
- support replacement control planes

---

## Enforcement Guidance for Agents

Before implementing a change, ask:

1. Which repository owns this responsibility?
2. Does this logic cross an ownership boundary?
3. Does this introduce a dependency on gated services?

If the answer is unclear, **stop and ask**.

---

## One-Line Summary

> **Execution lives in `mozaiksai`.  
> Authority lives in control planes.  
> Intelligence lives in generators.  
> Product shells live in `mozaikscore`.**

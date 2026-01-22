# UI Ownership & Plugin Boundaries (Source of Truth)

> Canonical source of truth: `docs/source_of_truth/03_UI_OWNERSHIP.md`

## Purpose

This document defines **where UI belongs** across the Mozaiks platform.

It exists to answer one question only:

Which UI should live inside agentic workflows (AI Plugins) versus which UI should live in product shells (Function Plugins or first-party apps).

This document applies to:
- Mozaiks (first-party product)
- MozaiksCore-based customer apps
- Any third-party shell embedding MozaiksAI

---

## Core Principle

MozaiksAI provides a **chat-native, agent-driven UI surface**.

Product shells provide **deterministic, navigational UI surfaces**.

These responsibilities must not be mixed.

---

## UI Categories

There are two canonical UI categories in the Mozaiks ecosystem.

### 1. AI Plugin UI (Chat-Native)

**Owned by:** MozaiksAI Runtime  
**Created by:** AgentGenerator workflows

AI Plugin UI is used when:
- The user collaborates with agents
- The UI state evolves as agents reason
- The interaction is conversational and iterative
- The state belongs to a specific chat or workflow run

Examples:
- AgentGenerator workspace
- Campaign builder canvas updated by agents
- AI-assisted onboarding interview
- Live spec or code generation workspace

---

### 2. Function Plugin UI (Product-Native)

**Owned by:** Product shell (Mozaiks or MozaiksCore)  
**Created by:** AppGenerator workflows

Function Plugin UI is used when:
- The UI is navigational or CRUD-based
- The UI exists outside any single chat
- The state must be auditable and permissioned
- The interaction must be deterministic

Examples:
- Dashboards
- Lists and tables
- Settings pages
- Marketplace browsing
- Billing and admin screens

---

## ChatUI Responsibilities

ChatUI is part of MozaiksAI and provides:

- Chat transcript rendering
- Message streaming
- Artifact panels and inline artifacts
- Mode switching (chat / workflow)
- Persistent chat widgets or overlays

ChatUI must remain:
- stable
- embeddable
- product-agnostic

ChatUI must not:
- own global navigation
- implement admin pages
- handle billing or permissions

---

## Workflow-Specific UI (Artifacts)

Workflows may optionally ship UI components for artifacts.

These live under:

ChatUI/src/workflows/{workflow_id}/components/

Rules:
- Components are scoped to a single workflow
- Components render artifacts emitted by tools or agents
- Components must tolerate reconnects and re-renders
- Absence of components must not break execution

If no custom component exists, generic artifact rendering is used.

---

## UI Launch Patterns

### Launching an AI Plugin

From a product UI:

1. User clicks an action (e.g., Generate Agents)
2. Product shell requests execution authorization
3. Control plane resolves capability to workflow
4. Product shell opens ChatUI connected to MozaiksAI
5. Workflow executes and renders artifacts

The product shell never reimplements the chat surface.

---

### Launching a Function Plugin

From navigation or marketplace:

1. User navigates to a page
2. Product shell renders deterministic UI
3. APIs handle CRUD and queries
4. Optional actions may launch AI Plugins

---

## Mozaiks vs MozaiksCore

### Mozaiks (First-Party)

- Uses ChatUI directly
- Opens WebSocket connections to MozaiksAI
- Hosts product pages separately
- Does not embed MozaiksCore

---

### MozaiksCore (Customer Apps)

- Provides product shell for customer apps
- Embeds or connects to ChatUI
- Acts as session broker to MozaiksAI
- Launches AI Plugins from Function Plugin pages

MozaiksCore is not required for MozaiksAI usage.

---

## Decision Rules (When Unsure)

Put UI in an AI Plugin if:
- The user is conversing with agents
- The UI evolves as reasoning progresses
- The state belongs to a chat_id
- The UI is driven by tool events

Put UI in a Function Plugin if:
- The UI is navigational or administrative
- The state exists outside a chat
- The UI requires strict permissions
- The UI spans multiple workflows

If still unsure:
Default to Function Plugin UI and launch AI Plugins as needed.

---

## Enforcement Rules

- AI Plugins must not introduce product navigation
- Function Plugins must not reimplement chat
- ChatUI must remain runtime-owned
- Product shells own routing and permissions

Violations of these rules are architectural bugs.

---

## One-Sentence Summary

Chat-native, agent-driven UI lives with workflows.  
Product-native, deterministic UI lives with the product shell.

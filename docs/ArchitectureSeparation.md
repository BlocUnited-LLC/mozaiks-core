# Architecture Separation Guide

## Overview
This document outlines the strict separation of concerns between **MozaiksCore** (App Layer) and **Mozaiks Platform** (Governance/Funding Layer).

## Core Principles

1.  **App Users vs. Platform Users**
    *   **MozaiksCore** stores **App Users** in the app's MongoDB `users_collection` (see `backend/core/config/database.py`).
        *   **Self-hosted mode**: users authenticate locally via `backend/security/auth.py` (core-issued JWTs).
        *   **Managed mode**: the shell validates **platform-issued JWTs** via JWKS (`backend/security/platform_jwt.py`) and maps them to local user records (`backend/security/authentication.py`). There are **no shared signing keys**.
    *   **Mozaiks Platform** manages **Platform Users** (investors, governance participants). Platform governance/funding systems are **NOT** accessible from MozaiksCore.

2.  **AI Integration**
    *   MozaiksCore is a **control plane** and **session broker**.
    *   MozaiksAI is an external **execution runtime** (workflows + ChatUI).
    *   MozaiksCore must not ship proprietary workflows or implement ChatUI; it should **launch capabilities** into the runtime.
    *   Configure runtime connectivity via env vars and use the control plane endpoints:
        *   `GET /api/ai/capabilities`
        *   `POST /api/ai/launch`

5.  **Chat UI vs. Runtime (Production Integration)**
    *   Treat **ChatUI** as a *client surface* and **MozaiksAI runtime** as the *server that drives state/workflows*.
    *   The app UI must not encode one-React-page-per-chat-step. Instead, the runtime emits step/state events and the UI renders them dynamically.
    *   Integration must work for:
        *   **Existing apps** with their own UI repo + microservices repo (like Mozaiks).
        *   **New apps** starting from scratch.

    **Recommended separation**
    *   **MozaiksCore (App Layer)**: owns app users, permissions, and app data.
    *   **MozaiksAI Runtime (AI Layer)**: owns agent orchestration, tool execution, streaming events, and workflow state.
    *   **ChatUI (Presentation Layer)**: renders messages/steps and sends user input; it should be embeddable in any host app.

    **What "hosting" means here**
    *   You can host the **app** (your microservices + your UI) and also host the **MozaiksAI runtime**.
    *   In production these are typically *separate services*:
        *   App UI (React) served from one deployment.
        *   App backend (microservices) served from one or more deployments.
        *   MozaiksAI runtime served from another deployment.
    *   "We host it" vs "they self-host" can apply specifically to the **runtime**, even if the rest of the app is self-hosted.

    **What "domain" means**
    *   A **domain** is the host name a browser connects to, e.g. `app.customer.com` vs `ai.customer.com`.
    *   If ChatUI runs inside the customer’s existing UI, it will be on their existing domain (recommended).
    *   If the runtime is on a different domain, you must handle:
        *   CORS for HTTP calls.
        *   WebSocket origin rules.
        *   Token/cookie strategy (usually short-lived bearer tokens).

    **Tool-call safety ("gateway" recommendation)**
    *   Simplest safe model: the runtime does NOT directly call internal microservices.
    *   Instead, the app backend exposes a small **tool gateway** (HTTP endpoints) that the runtime can call using app-issued auth.
    *   This lets the customer keep network boundaries (VPC/firewalls) and audit access.
    *   The gateway enforces permission checks as the **App User**, not as a platform user.

    **Minimum contract for a dynamic ChatUI**
    *   **Auth**: ChatUI uses a delegated bearer token; the runtime validates it (issuer/audience/signature).
    *   **Sessions**: the control plane provides a `chat_id` and workflow context; the runtime owns state and persistence.
    *   **Event stream (WebSocket)**: runtime → ChatUI streaming messages, tool progress, and artifact updates.
    *   **UI**: MozaiksCore hosts navigation and deterministic pages; ChatUI is embedded or opened as a runtime-owned surface.

3.  **Workflows**
    *   Workflows execute in **MozaiksAI runtime**, not in MozaiksCore.
    *   MozaiksCore defines a **capability registry** that maps capabilities → workflow IDs.
    *   The runtime never decides who is allowed to run a workflow; MozaiksCore authorizes before launch.

4.  **Subscriptions**
    *   App subscriptions are managed by `SubscriptionManager` (`backend/core/subscription_manager.py`).
    *   This logic is strictly for app feature access and billing, separate from platform tokenomics.
    *   In the OSS MozaiksCore Shell, hosted billing integration is **MozaiksPay-only** (see `docs/HostingModes.md`).

## Developer Guidelines

*   **Never** import platform-specific modules into MozaiksCore.
*   **Never** modify `backend/security/auth.py` to include platform wallet/governance logic.
*   **Never** make MozaiksCore mint platform tokens; managed mode must only verify platform JWTs.
*   **Always** launch AI capabilities into the MozaiksAI runtime (do not call LLMs directly from the shell).

## Integration Guidelines (Embedding ChatUI)

These rules exist so MozaiksCore can be integrated into existing applications without rewriting their UI routing.

*   Prefer **one Chat surface** (widget or page) over many per-step chat pages.
*   Keep all workflow progression in runtime state (events), not in React routes.
*   Treat the runtime as an external dependency: the host app provides auth + context + tool gateway.
*   Never allow ChatUI to bypass app authorization checks; the app backend remains the source of truth for access control.

## Directory Structure

*   `backend/core/runtime/`: Runtime bridge (session broker, no workflow execution).
*   `backend/core/routes/ai.py`: AI control plane endpoints (capabilities + launch).
*   `backend/security/`: Local auth + platform JWT verification.
*   `backend/core/subscription_manager.py`: App subscription logic.

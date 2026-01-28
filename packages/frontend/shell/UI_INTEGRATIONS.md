# ChatUI – Recent UI Integrations

This log captures the latest interaction-layer upgrades so we can retrace how the Chat UI evolved and quickly reason about future changes.

## 1. Ask/Workflow Mode Orchestration
- **Files:** `src/pages/ChatPage.js`, `src/context/ChatUIContext.js`
- **What changed:** Centralized mode management keeps separate caches for workflow threads and Ask Mozaiks transcripts. Switching modes now snapshots artifact state, preserves pending messages, and restores the correct history when you come back.
- **Why it matters:** Users can hop between structured workflows and free-form Ask sessions without losing context or duplicating requests.
- **How to exercise:** From `/chat`, start a workflow run, switch to Ask mode via the toggle, then return to workflow mode. Expect the Ask transcript to persist and the workflow artifact panel to reopen with its previous contents.

## 2. Artifact Panel Persistence
- **Files:** `src/pages/ChatPage.js`
- **What changed:** Added `workflowArtifactSnapshotRef` plus layout tracking so leaving workflow mode captures whether the side panel was open, its layout style, and the rendered artifact messages. Returning to workflow mode replays that snapshot (or infers artifacts from message metadata).
- **Why it matters:** Complex UI artifacts (forms, previews, inline builders) no longer disappear when the user explores Ask mode or refreshes the page.
- **How to exercise:** Trigger any workflow that emits a UI tool artifact, open the Artifact panel, switch to Ask mode, then back to workflow. The panel should reopen with the same artifact payload.

## 3. Persistent Chat Widget Enhancements
- **Files:** `src/components/chat/PersistentChatWidget.js`
- **What changed:** The minimized widget now displays the active mode and exposes a quick toggle that fires the same WebSocket control messages as the main chat (e.g., `chat.enter_general_mode`).
- **Why it matters:** When browsing pages like `/workflows`, the user can still flip between Ask ↔ workflow contexts without reloading the main chat view.
- **How to exercise:** Navigate to `/workflows`, use the widget’s 🧠/🤖 button to switch modes, then expand the chat. The full chat should already be in that mode with the correct transcript loaded.

## 4. Auto-switch to Ask Mode Off Chat Routes
- **Files:** `src/pages/ChatPage.js`
- **What changed:** A guard watches the router path; if the user leaves the primary chat routes (`/chat`, app deep links, etc.), the runtime automatically sends `chat.enter_general_mode` to park the session in Ask mode while keeping workflow state cached.
- **Why it matters:** Prevents partially completed workflows from staying “active” while the user browses other sections, and guarantees the widget always reflects Ask mode across the rest of the app.
- **How to exercise:** Start in workflow mode, navigate to `/workflows`, then expand the widget or return to `/chat`. You should land in Ask mode with your previous Ask session restored while the workflow cache remains intact for later.

## 5. General Chat Transcript Hydration
- **Files:** `src/pages/ChatPage.js`
- **What changed:** Added `hydrateGeneralTranscript`, caching, and session listing so Ask mode can immediately reload previous general chats once the backend responds.
- **Why it matters:** Users can pick up long-running Ask conversations without retyping context, and the runtime avoids redundant API calls by caching transcripts per mode.
- **How to exercise:** From Ask mode, select an older general chat (or refresh the page if one is active). The transcript should repopulate automatically once the API call completes.

---
_Keep appending to this file as new transport, widget, or layout integrations land. Quick notes (date, feature, touchpoints) help us spot regressions and communicate changes to the broader team._
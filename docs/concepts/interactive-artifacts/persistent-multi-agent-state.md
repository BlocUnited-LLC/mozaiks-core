# Persistent Multi-Agent State: Why It Matters

MozaiksAI now keeps one continuous nervous system for every user session:
Transport, workflows, and the Ask Mozaiks companion all share the same
WebSocket, persistence layer, and audit trail. This is a big shift from
the usual "one workflow per tab" model—users can jump between workflows,
ask a general question, then resume execution without losing state or
replaying conversations.

---

## What We Just Shipped

- **SessionRegistry + SimpleTransport upgrades** keep a single WebSocket
  alive while users start, pause, and resume multiple workflows. Session
  tabs behave like browser tabs for agents.
- **Ask Mozaiks general agent** plugs into that same transport. When a
  user enters general mode, all workflows pause, the message routes to a
  lightweight LLM call (outside AG2), and the reply is persisted with
  metadata so it never pollutes workflow resumes.
- **General chat isolation** now stores every Ask Mozaiks session in its
  own `GeneralChatSessions` collection with numbered labels (“General
  Chat #3”, etc.). Workflow documents stay clean, while general chats
  remain resumable with their own sequence counters and metrics.
- **General chat APIs** expose REST endpoints for listing and resuming
  Ask Mozaiks transcripts, so the ChatUI can fetch history without
  touching workflow documents or replaying AG2 turns.
- **Shared persistence contract** (`AG2PersistenceManager`) now stores
  general agent messages with `source=general_agent`, allowing the UI to
  replay everything while orchestration filters them out when a workflow
  restarts.
- **Dependency manager alignment** (planned next) keeps portal guards at
  the workflow layer, so manual switching + Ask Mozaiks respect the same
  prerequisites that generator-defined apps rely on.

---

## Why This Is a Milestone

1. **Continuous UX:** Users experience an operating system, not a
   collection of disconnected workflows. Ask Mozaiks can reference live
   workflow status, artifacts, and tabs because it taps directly into the
   runtime registry.
2. **Deterministic state recovery:** WebSocket reconnects replay the full
   mixed conversation (workflow turns + Ask Mozaiks chat) without double
   counting tokens or confusing AG2. Persistent state truly spans
   multiple agents.
3. **Modular architecture:** The general agent is implemented as a
   service adapter layered on top of existing transport/persistence
   primitives. No AG2 forks, no special-case workflows—any app that opts
   into manual switching automatically gets the companion experience.
4. **Observability + billing ready:** Every Ask Mozaiks turn reports
   prompt/completion token usage through the same `update_session_metrics`
   hook the workflows use, so MozaiksPay billing/analytics stay in
   sync.

---

## Talking Points for Stakeholders

- *"Persistent state management for a multi-agent runtime"* is not just
  marketing copy—we now pause/resume AG2 workflows, stream general-agent
  answers, and keep a single transcript + metrics channel per session.
- Ask Mozaiks is the proof point: it feels like a native OS assistant,
  yet it uses the same pluggable LLM config list and Mongo persistence as
  every generated workflow.
- The groundwork enables future upgrades (generator-defined knowledge
  packs, auto intent detection, portal guards) without re-architecting.
  We have the transport, persistence, and observability hooks in place.

Share this doc whenever someone asks *"Why invest in manual workflow
switching + Ask Mozaiks?"*—it captures the platform value beyond the UI
chrome.

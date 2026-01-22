# Temporary Handoff Instrumentation

## Why This Exists

AG2 does not yet emit built-in events for handoff transitions (context conditions, after-work fallbacks, LLM-triggered targets). To surface those transitions in MozaiksAI we patched runtime code to:

- Wrap the private AG2 helpers `_run_oncontextconditions` and `_evaluate_after_works_conditions` so we can log extra details and emit internal `runtime.handoff` events.
- Add `core/events/handoff_events.py` with a lightweight dispatcher hook that mirrors the future upstream API.
- Export the helper from `core.events` and feed richer metadata (workflow, chat, condition index, expression text) into each handoff event.

These changes should be removed once AG2 ships native handoff events (tracked in https://github.com/ag2ai/ag2/pull/2093).

## Impacted Files

- `core/workflow/handoffs.py` — monkeypatches AG2 helpers and fires `emit_handoff_event()` payloads.
- `core/events/handoff_events.py` — temporary helper for emitting `runtime.handoff` events.
- `core/events/__init__.py` — exports the helper so other modules can import it.

## How To Remove When Upstream Lands Support

1. **Confirm upstream version**: upgrade AG2 to the release that includes native handoff events. Verify the new event classes/emitters exist before deleting anything.
2. **Delete the temporary helper**: remove `core/events/handoff_events.py` and its export from `core/events/__init__.py`.
3. **Restore original handoff behaviour**:
   - Replace the patched logic in `core/workflow/handoffs.py` with the upstream wiring (remove the `_extract_context_metadata`, `emit_handoff_event`, and `_patch_autogen_handoff_logging()` instrumentation that now duplicates upstream capabilities).
4. **Adjust listeners**: if any Mozaiks components subscribed to `runtime.handoff`, repoint them to the official AG2 event(s) or drop the listener if no longer needed.
5. **Smoke test**: run a generator workflow that exercises context and after-work transitions. Ensure the new AG2 events flow through the dispatcher and there are no duplicate logs or missing transitions.

Keep this note until the upstream implementation fully replaces our shim, then remove this file as part of the cleanup commit.

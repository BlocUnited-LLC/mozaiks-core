# Agent Instructions: Widget Mode for New Pages

## Quick Start

When creating a new page that should allow users to return to their active workflow:

### Step 1: Import the Hook
```javascript
import { useWidgetMode } from '../hooks/useWidgetMode';
```

### Step 2: Call It in Your Component
```javascript
function MyNewPage() {
  useWidgetMode(); // ← Add this line
  // Optional: hide the floating widget UI on this page
  // useWidgetMode({ showWidget: false });

  return (
    <div>
      {/* Your page content */}
    </div>
  );
}
```

### That's It!

The page now has:
- ✅ Floating chat widget (persistent chat on non-ChatPage routes)
- ✅ 🧠 brain toggle to return to workflow
- ✅ Automatic state management during navigation
- ✅ Safe-area spacing handled via the global `widget-safe-bottom` utility

## Safe-Area Contract (Global Behavior)

- `src/index.css` defines `--widget-bottom-offset`, which currently evaluates to `calc(env(safe-area-inset-bottom, 0px) + 2rem)` (adjust this token for global spacing).
- Any floating container that needs to hover above mobile OS chrome simply adds the `widget-safe-bottom` class (already wired into `ChatPage` and `PersistentChatWidget`).
- No per-page spacing tweaks are required. When generators create new pages, they **only** need to call `useWidgetMode()`; the runtime-owned widget automatically inherits the safe-area offset.
- Avoid overriding `bottom` on the widget containers in page-level CSS; instead adjust `--widget-bottom-offset` if global spacing needs to change.
- For additional platform-owned variables (colors, shadows, future widget knobs) see `RUNTIME_TOKENS.md`.

## Complete Example

```javascript
import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useChatUI } from '../context/ChatUIContext';
import { useWidgetMode } from '../hooks/useWidgetMode';
import Header from '../components/layout/Header';

function AnalyticsPage() {
  useWidgetMode(); // ← REQUIRED for workflow return

  const navigate = useNavigate();
  const { config } = useChatUI();
  const [data, setData] = useState([]);

  useEffect(() => {
    // Load your data
  }, []);

  return (
    <>
      <Header />
      <div className="container mx-auto px-4 py-8">
        <h1>Analytics Dashboard</h1>
        {/* Your content */}
      </div>
    </>
  );
}

export default AnalyticsPage;
```

## When NOT to Use

Don't use `useWidgetMode()` for:
- ChatPage.js (primary chat route, not widget mode)
- Login/Auth pages (no workflows active)
- Modal/overlay components (not full pages)
- Error pages (user needs to fix error, not return to workflow)

## Verification

After creating the page, test:
1. Start a workflow on ChatPage
2. Click 🧠 to enter Ask mode (or navigate to your new page)
3. Navigate to your new page
4. Verify chat widget appears as a persistent floating widget
5. Click 🧠 → should fetch most recent IN_PROGRESS workflow and return to ChatPage

## Troubleshooting

If chat widget doesn't appear:
- Check you imported the hook correctly
- Verify you called `useWidgetMode()` inside the component (not conditionally)
- Check console for "🔍 [WIDGET_MODE] Entering widget mode (persistent chat on non-ChatPage route)"
- Verify PersistentChatWidget is rendering

If 🧠 doesn't work:
- Ensure a workflow is IN_PROGRESS (check `/api/sessions/recent` endpoint)
- Check network tab for API call to fetch most recent workflow
- Verify user has an active workflow to return to
- Check console logs for widget mode state transitions

## How It Works

**Widget Mode State Management:**
- When you navigate to a page with `useWidgetMode()`, it sets `isInWidgetMode = true`
- This flag persists during navigation (not cleared on unmount)
- When clicking 🧠 from widget mode:
  1. Fetches most recent IN_PROGRESS workflow
  2. Navigates to `/chat`
  3. ChatPage detects `isInWidgetMode = true`
  4. Switches to workflow mode and resumes the workflow
  5. Clears `isInWidgetMode` after successful transition

**Key Fix:**
The hook no longer clears `isInWidgetMode` on unmount, which was causing the state to be lost during navigation. ChatPage now handles clearing the flag after processing the return from widget mode.

## Generator/Plugin Checklist

When you ask a stateless PageGenerator (or any plugin agent) to build widget-ready pages, include this checklist in the prompt so outputs are compatible with the runtime:

- **Always import & call** `useWidgetMode()` at the top of the component. Example:
  ```javascript
  import { useWidgetMode } from '../hooks/useWidgetMode';

  export default function MyPage() {
    useWidgetMode();
    return <div>...</div>;
  }
  ```
- **Declare AI tools instead of hardcoding sockets.** Client-side code should call `fetch('/api/tools/<tool>')` or `window.Mozaiks.callTool('<tool>', payload)`; the runtime maps those to WebSocket/tool adapters.
- **Emit a manifest** for every generated page that lists `page_name`, `route`, `isWidgetMode`, `files`, and a `tools` array describing HTTP/WS bindings (method, path, input/output schema). This lets MozaiksCore auto-register the page and keep tenancy boundaries intact.
- **Describe any WebSocket needs declaratively.** Generators should list required topics (e.g., `app.{app_id}.workflow.{workflow_id}.update` - legacy: `app.{app_id}...`) in the manifest so the runtime wires them to the persistent chat widget without leaking raw URLs.
- **Reference this doc** inside your generator prompt (e.g., "include the widget-mode instructions from `WIDGET_MODE.md`") so downstream agents never forget to add the hook or tool metadata.

## Migration from Discovery Mode

If you have existing code using the old `useDiscoveryMode` naming:

**Old:**
```javascript
import { useDiscoveryMode } from '../hooks/useDiscoveryMode';
const { isInDiscoveryMode } = useChatUI();
```

**New:**
```javascript
import { useWidgetMode } from '../hooks/useWidgetMode';
const { isInWidgetMode } = useChatUI();
```

The functionality is identical, only the naming has changed to better reflect that this is about persistent chat widget mode on non-ChatPage routes, not just a "discovery" feature.

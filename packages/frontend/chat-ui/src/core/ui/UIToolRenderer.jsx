import React from 'react';
import { useChatUI } from '../../context/ChatUIContext';

/**
 * UIToolRenderer
 *
 * The OSS-friendly contract here is: chat-ui renders UI tools only if the host
 * provides a renderer function (via ChatUIProvider `uiToolRenderer`).
 *
 * This avoids coupling @mozaiks/chat-ui to any workflow registry implementation.
 */
const UIToolRenderer = ({ event, onResponse, submitInputRequest, className = '' }) => {
  const { uiToolRenderer } = useChatUI();

  if (!event || !event.ui_tool_id) {
    return null;
  }

  if (typeof uiToolRenderer === 'function') {
    try {
      const rendered = uiToolRenderer(event, onResponse, submitInputRequest);
      return <div className={className}>{rendered}</div>;
    } catch (err) {
      // Do not hard-fail the whole chat UI if a host renderer throws.
      console.error('UIToolRenderer: host uiToolRenderer threw', err);
      return (
        <div className={className}>
          <div className="text-xs text-[var(--color-warning)]">
            UI tool renderer error: {event.ui_tool_id}
          </div>
        </div>
      );
    }
  }

  // Default: render nothing (workflow UI tools are an optional integration).
  return null;
};

export default UIToolRenderer;

// ==============================================================================
// FILE: ChatUI/src/core/ui/UIToolRenderer.js
// DESCRIPTION: Component that renders UI tools from backend events
// PURPOSE: Core system component for rendering any workflow's UI tools
// ==============================================================================

import React from 'react';
import { handleEvent } from '../eventDispatcher';

/**
 * 🎯 UI TOOL RENDERER - CORE COMPONENT
 * 
 * This component receives UI tool events from the backend and renders
 * the appropriate UI component using the event dispatcher.
 * 
 * WORKFLOW-AGNOSTIC:
 * - Works with any workflow's UI tools
 * - Uses the core event dispatcher for routing
 * - Provides consistent error handling
 */
const UIToolRenderer = ({ 
  event, 
  onResponse,
  submitInputRequest,
  className = ""
}) => {
  // Validate event structure
  if (!event || !event.ui_tool_id) {
  console.warn('⚠️ UIToolRenderer: Invalid event structure', event);
    return (
      <div className={`ui-tool-error ${className}`}>
        <p className="text-[var(--color-error)]">Invalid UI tool event</p>
      </div>
    );
  }

  try {
    // Prefer explicit agentName when available to attribute UI tools correctly
    const resolvedAgentName = event.agentName || event.agent_name || event.payload?.agentName || event.payload?.agent_name || event.agent || null;
    console.log('🧩 UIToolRenderer: Rendering', {
      ui_tool_id: event.ui_tool_id,
      eventId: event.eventId,
      workflow_name: event.workflow_name,
      agentName: resolvedAgentName,
      payloadKeys: event.payload ? Object.keys(event.payload) : []
    });
    // Use the event dispatcher to render the component
    const renderedComponent = handleEvent(event, onResponse, submitInputRequest);

    if (!renderedComponent) {
      console.warn('⚠️ UIToolRenderer: handleEvent returned null; component may be missing');
      return (
        <div className={`ui-tool-not-found ${className}`}>
          <p className="text-[var(--color-warning)] text-slate-200">
            UI tool '{event.ui_tool_id}' not found or failed to load
          </p>
          <p className="text-gray-400 text-sm">
            Check if the workflow is properly registered
          </p>
        </div>
      );
    }

    return (
      <div className={`ui-tool-container ${className}`}>
        {renderedComponent}
      </div>
    );

  } catch (error) {
    console.error('❌ UIToolRenderer: Error rendering UI tool', error);
    return (
      <div className={`ui-tool-error ${className}`}>
        <p className="text-[var(--color-error)]">Error rendering UI tool: {event.ui_tool_id}</p>
        <p className="text-gray-400 text-sm">{error.message}</p>
      </div>
    );
  }
};

export default UIToolRenderer;

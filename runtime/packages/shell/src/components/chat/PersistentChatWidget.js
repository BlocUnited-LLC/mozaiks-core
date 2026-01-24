import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useChatUI } from '../../context/ChatUIContext';
import ChatInterface from './ChatInterface';

/**
 * PersistentChatWidget - Floating chat widget in bottom-right corner
 *
 * Shows when user is on Discovery/Workflows page.
 * Two states:
 * - Minimized: Small rounded square with Mozaiks logo
 * - Expanded: Chat panel in corner with two navigation buttons:
 *   - Robot (🤖) → Navigate to Ask mode
 *   - Mozaiks logo → Navigate to Workflow mode (persisted state)
 */
const PersistentChatWidget = ({
  chatId,
  workflowName,
  conversationMode
}) => {
  const {
    setConversationMode,
    activeChatId,
    activeWorkflowName,
    setActiveChatId,
    setActiveWorkflowName,
  } = useChatUI();
  const navigate = useNavigate();

  const [isExpanded, setIsExpanded] = useState(false);
  const [messages, setMessages] = useState([]);

  const toggleExpanded = () => {
    setIsExpanded(prev => !prev);
  };

  const handleSendMessage = (message) => {
    // In widget mode without backend, show placeholder
    console.log('📨 [WIDGET] Message sent (offline mode):', message);
    setMessages(prev => [...prev, {
      id: Date.now(),
      role: 'user',
      content: message,
      timestamp: new Date().toISOString(),
    }]);

    // Add a placeholder response
    setTimeout(() => {
      setMessages(prev => [...prev, {
        id: Date.now() + 1,
        role: 'assistant',
        content: 'Backend is currently unavailable. Please try again later.',
        timestamp: new Date().toISOString(),
      }]);
    }, 500);
  };

  const handleConversationModeChange = (mode) => {
    setConversationMode(mode);
  };

  // Navigate to Ask mode
  const handleGoToAskMode = () => {
    console.log('🧭 [WIDGET] Navigating to ChatPage in Ask mode');
    setConversationMode('ask');
    setIsExpanded(false);
    // Pass mode=ask query param so ChatPage bootstrap respects it
    navigate('/chat?mode=ask');
  };

  // Navigate to Workflow mode with persisted state
  const handleGoToWorkflowMode = () => {
    console.log('🧭 [WIDGET] Navigating to ChatPage in Workflow mode (persisted state)');
    
    // Get persisted chat/workflow from props, context, or localStorage
    const safeGet = (key) => {
      try { return localStorage.getItem(key); } catch { return null; }
    };
    const resolvedChatId = chatId || activeChatId || safeGet('mozaiks.current_chat_id');
    const resolvedWorkflowName = workflowName || activeWorkflowName || safeGet('mozaiks.current_workflow_name');

    // Update context with persisted values
    if (resolvedChatId) {
      setActiveChatId(resolvedChatId);
      try { localStorage.setItem('mozaiks.current_chat_id', resolvedChatId); } catch {}
    }
    if (resolvedWorkflowName) {
      setActiveWorkflowName(resolvedWorkflowName);
      try { localStorage.setItem('mozaiks.current_workflow_name', resolvedWorkflowName); } catch {}
    }

    // Set workflow mode
    setConversationMode('workflow');
    setIsExpanded(false);

    // Navigate with params to restore state
    const params = new URLSearchParams();
    if (resolvedChatId) {
      params.set('chat_id', resolvedChatId);
    }
    if (resolvedWorkflowName) {
      params.set('workflow', resolvedWorkflowName);
    }
    const suffix = params.toString() ? `?${params.toString()}` : '';
    navigate(`/chat${suffix}`);
  };

  // Minimized state - small square bubble with logo
  if (!isExpanded) {
    return (
      <div className="fixed right-4 z-50 widget-safe-bottom">
        <button
          type="button"
          onClick={toggleExpanded}
          className="group relative w-20 h-20 rounded-2xl bg-gradient-to-br from-[var(--color-primary)] to-[var(--color-secondary)] shadow-[0_8px_32px_rgba(15,23,42,0.6)] border-2 border-[rgba(var(--color-primary-light-rgb),0.5)] hover:shadow-[0_16px_48px_rgba(51,240,250,0.4)] hover:scale-105 transition-all duration-300 flex items-center justify-center"
          title={workflowName ? `Continue: ${workflowName}` : 'Open chat'}
        >
          <div className="absolute inset-0 rounded-2xl bg-gradient-to-br from-[rgba(var(--color-primary-light-rgb),0.2)] to-transparent opacity-0 group-hover:opacity-100 transition-opacity"></div>
          <img
            src="/mozaik_logo.svg"
            alt="MozaiksAI"
            className="w-11 h-11 relative z-10 group-hover:scale-110 transition-transform"
            onError={(e) => {
              e.currentTarget.onerror = null;
              e.currentTarget.src = '/mozaik.png';
            }}
          />
        </button>
      </div>
    );
  }

  // Expanded state - chat panel in corner with navigation buttons
  return (
    <div className="fixed right-4 z-50 flex flex-col items-end gap-0 pointer-events-none widget-safe-bottom">
      {/* Collapse button / header tab */}
      <button
        type="button"
        onClick={toggleExpanded}
        className="pointer-events-auto relative group mb-[-1px] z-20"
        title="Minimize chat"
      >
        <div className="w-32 h-8 rounded-t-2xl bg-gradient-to-r from-[rgba(var(--color-primary-rgb),0.4)] to-[rgba(var(--color-secondary-rgb),0.4)] border-t border-l border-r border-[rgba(var(--color-primary-light-rgb),0.4)] backdrop-blur-sm flex items-center justify-center group-hover:bg-gradient-to-r group-hover:from-[rgba(var(--color-primary-rgb),0.6)] group-hover:to-[rgba(var(--color-secondary-rgb),0.6)] transition-all">
          <svg className="w-5 h-5 text-[var(--color-primary-light)] group-hover:text-white transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth="2.5">
            <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
          </svg>
        </div>
      </button>

      {/* Chat panel */}
      <div className="pointer-events-auto w-[26rem] max-w-[calc(100vw-2.5rem)] h-[50vh] md:h-[70vh] min-h-[360px] bg-gradient-to-br from-gray-900/95 via-slate-900/95 to-black/95 backdrop-blur-xl border border-[rgba(var(--color-primary-light-rgb),0.3)] rounded-2xl rounded-tr-none shadow-2xl overflow-hidden flex flex-col">
        
        {/* Widget header with two navigation buttons only */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-[rgba(var(--color-primary-light-rgb),0.2)] bg-[rgba(0,0,0,0.3)]">
          {/* Left: Brain button → Ask mode (Chat Station) */}
          <button
            type="button"
            onClick={handleGoToAskMode}
            className="group flex items-center gap-2 px-4 py-2.5 rounded-xl bg-gradient-to-r from-[rgba(var(--color-secondary-rgb),0.2)] to-[rgba(var(--color-primary-rgb),0.2)] border border-[rgba(var(--color-secondary-rgb),0.4)] hover:border-[rgba(var(--color-secondary-rgb),0.7)] hover:scale-105 transition-all"
            title="MozaiksAI Chat Station"
          >
            <span className="text-2xl" role="img" aria-hidden="true">🧠</span>
            <span className="text-sm font-medium text-gray-300 group-hover:text-white">Chat Station</span>
          </button>
          
          {/* Right: Mozaiks logo only → Workflow mode (persisted) */}
          <button
            type="button"
            onClick={handleGoToWorkflowMode}
            className="group p-3 rounded-xl bg-gradient-to-r from-[rgba(var(--color-primary-rgb),0.2)] to-[rgba(var(--color-secondary-rgb),0.2)] border border-[rgba(var(--color-primary-light-rgb),0.4)] hover:border-[rgba(var(--color-primary-light-rgb),0.7)] hover:scale-105 transition-all"
            title="Resume Workflow"
          >
            <img
              src="/mozaik_logo.svg"
              className="w-8 h-8 opacity-80 group-hover:opacity-100 transition-all"
              alt="Workflow"
              onError={(e) => {
                e.currentTarget.onerror = null;
                e.currentTarget.src = '/mozaik.png';
              }}
            />
          </button>
        </div>

        {/* Chat content area - remove any extra decorations */}
        <div className="flex-1 overflow-hidden">
          <ChatInterface
            messages={messages}
            onSendMessage={handleSendMessage}
            loading={false}
            connectionStatus="disconnected"
            conversationMode={conversationMode}
            onConversationModeChange={handleConversationModeChange}
            isOnChatPage={false}
            hideHeader={true}
            disableMobileShellChrome={true}
            plainContainer={true}
          />
        </div>
      </div>
    </div>
  );
};

export default PersistentChatWidget;

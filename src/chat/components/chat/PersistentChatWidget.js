import React from 'react';
import { useChatUI } from '../../context/ChatUIContext';

/**
 * PersistentChatWidget - Minimized chat window in bottom-left corner (20% of screen height)
 * 
 * Shows when user is on Discovery/Workflows page.
 * Small always-available chat widget.
 *
 * Contract (3 surfaces):
 * - Bubble (minimized): opens this widget.
 * - Widget (this): Ask-only preview surface.
 * - Full-screen overlay: Ask or Workflow, opened via host API.
 */
const PersistentChatWidget = ({
  chatId,
  workflowName,
  conversationMode
}) => {
  const {
    setIsChatOverlayOpen,
    setConversationMode
  } = useChatUI();
  const widgetStyle = {
    height: '20vh',
    minHeight: '150px'
  };

  const requestOverlay = (detail) => {
    try {
      if (window.mozaiksChat && typeof window.mozaiksChat.open === 'function') {
        window.mozaiksChat.open(detail);
        return;
      }
      window.dispatchEvent(new CustomEvent('mozaiks:chat:open', { detail }));
    } catch (_) {
      // Fallback: at least open whatever surface is available.
      if (detail?.mode === 'workflow') {
        setConversationMode?.('workflow');
      } else {
        setConversationMode?.('ask');
      }
      setIsChatOverlayOpen(true);
    }
  };

  const openAskOverlay = () => requestOverlay({ mode: 'ask' });
  const openWorkflowOverlay = () => requestOverlay({
    mode: 'workflow',
    ...(chatId ? { chat_id: chatId } : null),
    ...(workflowName ? { workflow_name: workflowName } : null),
  });

  // Minimized chat window (20% screen height) in bottom-left corner
  return (
    <div
      className="fixed left-0 z-50 w-full sm:w-80 md:w-96 bg-gradient-to-br from-gray-900/95 via-slate-900/95 to-black/95 backdrop-blur-xl border-t border-r border-[rgba(var(--color-primary-light-rgb),0.3)] sm:rounded-tr-2xl shadow-2xl overflow-hidden widget-safe-bottom"
      style={widgetStyle}
    >
      {/* Header */}
      <div className="flex items-center justify-between px-3 sm:px-4 py-2 sm:py-3 bg-gradient-to-r from-[rgba(var(--color-primary-rgb),0.2)] to-[rgba(var(--color-secondary-rgb),0.2)] border-b border-[rgba(var(--color-primary-light-rgb),0.2)]">
        <div className="flex items-center gap-2 min-w-0 flex-1">
          <div className="w-7 h-7 sm:w-8 sm:h-8 rounded-lg bg-gradient-to-br from-[var(--color-primary)] to-[var(--color-secondary)] flex items-center justify-center p-1.5 border border-[var(--color-primary-light)]/30 flex-shrink-0">
            <img
              src="/mozaik_logo.svg"
              alt="MozaiksAI"
              className="w-full h-full"
              onError={(e) => {
                e.target.onerror = null;
                e.target.src = "/mozaik.png";
              }}
            />
          </div>
          <div className="min-w-0 flex-1">
            <h3 className="text-xs sm:text-sm font-bold text-white oxanium truncate">MozaiksAI</h3>
            {workflowName && (
              <p className="text-[10px] sm:text-xs text-[var(--color-primary-light)]/70 oxanium truncate">{workflowName}</p>
            )}
          </div>
        </div>
        
        <div className="flex items-center gap-1.5 sm:gap-2 flex-shrink-0">
          {/* Mode Toggle Button */}
          <button
            onClick={openWorkflowOverlay}
            className="w-7 h-7 sm:w-8 sm:h-8 rounded-lg bg-[var(--color-primary)]/20 hover:bg-[var(--color-primary)]/40 transition-all flex items-center justify-center group border border-[var(--color-primary-light)]/30"
            title="Open Workflow (full screen)"
          >
            <span className="text-sm sm:text-base group-hover:scale-110 transition-transform">
              🤖
            </span>
          </button>

          {/* Expand Chat Button */}
          <button
            onClick={openAskOverlay}
            className="w-7 h-7 sm:w-8 sm:h-8 rounded-lg bg-[var(--color-primary)]/20 hover:bg-[var(--color-primary)]/40 transition-all flex items-center justify-center group border border-[var(--color-primary-light)]/30"
            title="Open Ask (full screen)"
          >
            <svg className="w-3.5 h-3.5 sm:w-4 sm:h-4 text-[var(--color-primary-light)] group-hover:scale-110 transition-transform" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4" />
            </svg>
          </button>
        </div>
      </div>

      {/* Minimized chat preview */}
      <div className="p-3 sm:p-4 h-full flex flex-col justify-center items-center text-center">
        <div className="text-gray-400 text-sm mb-2">
          {chatId ? (
            <>
              <div className="text-2xl sm:text-3xl mb-1 sm:mb-2">
                {conversationMode === 'workflow' ? '🤖' : '🧠'}
              </div>
              <p className="oxanium text-white font-semibold text-xs sm:text-sm">
                Ask
              </p>
              <p className="text-[10px] sm:text-xs text-gray-500 mt-1 truncate px-2 max-w-full">{chatId}</p>
            </>
          ) : (
            <>
              <svg className="w-5 h-5 sm:w-6 sm:h-6 mx-auto mb-2 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
              </svg>
              <p className="oxanium text-xs sm:text-sm">No active chat</p>
            </>
          )}
        </div>
        <button
          onClick={openAskOverlay}
          className="mt-1 sm:mt-2 px-3 sm:px-4 py-1.5 sm:py-2 bg-gradient-to-r from-[var(--color-primary)] to-[var(--color-secondary)] text-white text-[10px] sm:text-xs rounded-lg hover:shadow-lg hover:shadow-[var(--color-primary)]/50 transition-all font-semibold oxanium"
        >
          Open Chat
        </button>
      </div>
    </div>
  );
};

export default PersistentChatWidget;

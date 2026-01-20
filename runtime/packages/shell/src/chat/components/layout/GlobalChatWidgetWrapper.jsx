/**
 * GlobalChatWidgetWrapper
 * 
 * Renders the persistent chat widget (PersistentChatWidget) and overlay (ChatOverlay)
 * when the user is in widget mode (navigating outside of ChatPage).
 * 
 * This component should be rendered at the root level, INSIDE the ChatUIProvider,
 * so it has access to the chat context.
 * 
 * Usage:
 * ```jsx
 * <ChatUIProvider>
 *   <Router>
 *     <GlobalChatWidgetWrapper />
 *     <Routes>...</Routes>
 *   </Router>
 * </ChatUIProvider>
 * ```
 */
import React from 'react';
import { useLocation } from 'react-router-dom';
import { useChatUI } from '../../context/ChatUIContext';
import PersistentChatWidget from '../chat/PersistentChatWidget';
import ChatOverlay from '../chat/ChatOverlay';

/**
 * Floating chat bubble that opens the widget/overlay.
 */
const ChatBubble = ({ onClick }) => {
  return (
    <button
      onClick={onClick}
      className="fixed z-50 w-14 h-14 sm:w-16 sm:h-16 rounded-full bg-gradient-to-br from-[var(--color-primary)] to-[var(--color-secondary)] shadow-xl hover:shadow-2xl hover:shadow-[var(--color-primary)]/50 transition-all duration-300 flex items-center justify-center group widget-safe-bottom"
      style={{ right: '1rem', bottom: 'calc(var(--widget-bottom-offset, 1rem) + env(safe-area-inset-bottom, 0px))' }}
      aria-label="Open chat"
    >
      <div className="w-8 h-8 sm:w-9 sm:h-9 rounded-lg flex items-center justify-center">
        <img
          src="/mozaik_logo.svg"
          alt="MozaiksAI"
          className="w-full h-full group-hover:scale-110 transition-transform"
          onError={(e) => {
            e.target.onerror = null;
            e.target.src = "/mozaik.png";
          }}
        />
      </div>
      <span className="absolute -top-1 -right-1 w-3 h-3 bg-green-500 rounded-full border-2 border-white animate-pulse" />
    </button>
  );
};

/**
 * GlobalChatWidgetWrapper
 * 
 * Conditionally renders the chat widget UI based on:
 * - isInWidgetMode: User is on a non-ChatPage route
 * - isWidgetVisible: Page hasn't suppressed the widget
 * - isChatOverlayOpen: User expanded the widget to overlay mode
 */
const GlobalChatWidgetWrapper = () => {
  const location = useLocation();
  const {
    isInWidgetMode,
    isWidgetVisible,
    isChatOverlayOpen,
    setIsChatOverlayOpen,
    activeChatId,
    activeWorkflowName,
    conversationMode,
    chatMinimized,
    setChatMinimized,
  } = useChatUI();

  // Determine if we're on the primary chat routes (don't show widget there)
  const pathSegments = location.pathname.split('/').filter(Boolean);
  const isPrimaryChatRoute =
    pathSegments.length === 0 ||
    pathSegments[0] === 'chat' ||
    (pathSegments[0] === 'app' && pathSegments.length >= 3); // /app/:appId/:workflow pattern

  // Don't render anything on primary chat routes
  if (isPrimaryChatRoute) {
    return null;
  }

  // Don't render if not in widget mode or widget is hidden
  if (!isInWidgetMode || !isWidgetVisible) {
    return null;
  }

  const handleBubbleClick = () => {
    if (chatMinimized) {
      setChatMinimized(false);
    } else {
      setIsChatOverlayOpen(true);
    }
  };

  const handleCloseOverlay = () => {
    setIsChatOverlayOpen(false);
  };

  return (
    <>
      {/* Full-screen overlay (when expanded) */}
      <ChatOverlay
        isOpen={isChatOverlayOpen}
        onClose={handleCloseOverlay}
        chatId={activeChatId}
        workflowName={activeWorkflowName}
      />

      {/* Minimized widget (shows when overlay is closed) */}
      {!isChatOverlayOpen && !chatMinimized && (
        <PersistentChatWidget
          chatId={activeChatId}
          workflowName={activeWorkflowName}
          conversationMode={conversationMode}
        />
      )}

      {/* Chat bubble (shows when minimized) */}
      {!isChatOverlayOpen && chatMinimized && (
        <ChatBubble onClick={handleBubbleClick} />
      )}
    </>
  );
};

export default GlobalChatWidgetWrapper;

/**
 * Re-export ChatUI context from @mozaiks/chat-ui package.
 * This ensures all components in the Shell use the same React context
 * as the package's components (like GlobalChatWidgetWrapper).
 */
export { ChatUIProvider, useChatUI } from '@mozaiks/chat-ui';

import React, { createContext, useContext, useEffect, useState } from 'react';
import services from '../services';
import config from '../config';
// Import workflow registry for UI tool registration
import { initializeWorkflows } from '../workflows';

const ChatUIContext = createContext(null);

export const useChatUI = () => {
  const context = useContext(ChatUIContext);
  if (!context) {
    throw new Error('useChatUI must be used within a ChatUIProvider');
  }
  return context;
};

export const ChatUIProvider = ({ 
  children,
  authAdapter = null,
  apiAdapter = null,
  onReady = () => {},
  agents = []
}) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [initialized, setInitialized] = useState(false);
  const [authAdapterInstance, setAuthAdapterInstance] = useState(null);
  const [apiAdapterInstance, setApiAdapterInstance] = useState(null);
  const [agentSystemInitialized, setAgentSystemInitialized] = useState(false);
  const [workflowsInitialized, setWorkflowsInitialized] = useState(false);
  const WORKFLOW_INIT_TIMEOUT_MS = 8000; // guard against endless spinner
  
  // Persistent chat state (survives navigation)
  const [activeChatId, setActiveChatId] = useState(null);
  const [activeWorkflowName, setActiveWorkflowName] = useState(null);
  const [chatMinimized, setChatMinimized] = useState(false);
  const [unreadChatCount, setUnreadChatCount] = useState(0);
  
  // Fluid layout state - controls chat/artifact panel behavior
  const [layoutMode, setLayoutMode] = useState('full'); // 'full' | 'split' | 'minimized'
  const [previousLayoutMode, setPreviousLayoutMode] = useState('full'); // Remember state before minimizing
  const [isArtifactOpen, setIsArtifactOpen] = useState(false);
  const [isInWidgetMode, setIsInWidgetMode] = useState(false); // Track if chat is in persistent widget mode (non-ChatPage routes)
  const [isWidgetVisible, setIsWidgetVisible] = useState(true); // Allows specific pages to suppress the widget UI while staying in widget mode
  const [isChatOverlayOpen, setIsChatOverlayOpen] = useState(false); // Full-screen overlay while remaining in widget mode

  // Conversation mode + general chat state (non-workflow / ask mode)
  const [conversationMode, setConversationMode] = useState(() => {
    try {
      const stored = localStorage.getItem('mozaiks.conversation_mode');
      if (stored === 'ask' || stored === 'workflow') {
        return stored;
      }
    } catch (_) {
      /* ignore storage errors */
    }
    return 'workflow';
  });
  const [activeGeneralChatId, setActiveGeneralChatId] = useState(null);
  const [generalChatSummary, setGeneralChatSummary] = useState(null);
  const [generalChatSessions, setGeneralChatSessions] = useState([]);
  
  // Workflow sessions list for future dropdown (IN_PROGRESS workflows)
  const [workflowSessions, setWorkflowSessions] = useState([]);

  useEffect(() => {
    try {
      localStorage.setItem('mozaiks.conversation_mode', conversationMode);
    } catch (_) {
      /* ignore storage errors */
    }
  }, [conversationMode]);

  useEffect(() => {
    const initializeServices = async () => {
      try {
        // Initialize workflow registry first (UI tools need to be registered)
        console.log('🔧 Initializing workflow registry...');
        try {
          await Promise.race([
            initializeWorkflows(),
            new Promise((_, reject) => setTimeout(() => reject(new Error('workflow_init_timeout')), WORKFLOW_INIT_TIMEOUT_MS))
          ]);
          setWorkflowsInitialized(true);
          console.log('✅ Workflow registry initialized');
        } catch (wfErr) {
          if (wfErr.message === 'workflow_init_timeout') {
            console.warn('⚠️ Workflow initialization timed out – continuing with partial UI.');
          } else {
            console.warn('⚠️ Workflow registry init failed:', wfErr);
          }
        }

        // Initialize services with custom adapters
        services.initialize({ authAdapter, apiAdapter });

        // Get the adapter instances
        const authAdapterInst = services.getAuthAdapter();
        const apiAdapterInst = services.getApiAdapter();
        
        setAuthAdapterInstance(authAdapterInst);
        setApiAdapterInstance(apiAdapterInst);

        // Get initial user
        const currentUser = await authAdapterInst?.getCurrentUser();
        setUser(currentUser);

        // Listen for auth state changes
        if (authAdapterInst?.onAuthStateChange) {
          authAdapterInst.onAuthStateChange((newUser) => {
            setUser(newUser);
          });
        }

  // Agent system functionality now piggybacks on workflow registry status
  setAgentSystemInitialized(true);
  console.log('✅ Workflow-based agent system ready');

        setInitialized(true);
  setLoading(false);
        onReady();

      } catch (error) {
  console.error('Failed to initialize ChatUI:', error);
  setLoading(false);
      }
    };

    initializeServices();
  }, [authAdapter, apiAdapter, onReady]);

  useEffect(() => {
    // Agents are auto-discovered through the workflow system
    if (agents.length > 0) {
      console.warn('Custom agent registration via props is not supported. Agents are defined in the agents.json file.');
    }
  }, [agents]);

  const contextValue = {
    // User state
    user,
    setUser,
    loading,
    initialized,

    // System state
    agentSystemInitialized,
    workflowsInitialized,

    // Persistent chat state
    activeChatId,
    setActiveChatId,
    activeWorkflowName,
    setActiveWorkflowName,
    chatMinimized,
    setChatMinimized,
    unreadChatCount,
    setUnreadChatCount,
    
    // Fluid layout state
    layoutMode,
    setLayoutMode,
    previousLayoutMode,
    setPreviousLayoutMode,
    isArtifactOpen,
    setIsArtifactOpen,
    isInWidgetMode,
    setIsInWidgetMode,
    isWidgetVisible,
    setIsWidgetVisible,
    isChatOverlayOpen,
    setIsChatOverlayOpen,
    conversationMode,
    setConversationMode,
    activeGeneralChatId,
    setActiveGeneralChatId,
    generalChatSummary,
    setGeneralChatSummary,
    generalChatSessions,
    setGeneralChatSessions,
    workflowSessions,
    setWorkflowSessions,

    // Configuration
    config: config.getConfig(),

    // Services (use state instances to avoid initialization errors)
    auth: authAdapterInstance,
    api: apiAdapterInstance,

    // Actions
    logout: async () => {
      if (authAdapterInstance) {
        await authAdapterInstance.logout();
        setUser(null);
      }
    },
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-gradient-to-br from-gray-900 to-blue-900">
        <div className="text-white text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-white mx-auto mb-4"></div>
          <p className="techfont">Initializing ChatUI...</p>
        </div>
      </div>
    );
  }

  return (
    <ChatUIContext.Provider value={contextValue}>
      {children}
    </ChatUIContext.Provider>
  );
};

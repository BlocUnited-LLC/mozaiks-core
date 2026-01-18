import { useEffect, useState, useRef, useCallback } from "react";
import Header from "../components/layout/Header";
import ChatInterface from "../components/chat/ChatInterface";
import ArtifactPanel from "../components/chat/ArtifactPanel";
import WorkflowCompletion from "../components/chat/WorkflowCompletion";
import FluidChatLayout from "../components/chat/FluidChatLayout";
import MobileArtifactDrawer from "../components/chat/MobileArtifactDrawer";
import { useNavigate, useLocation } from "react-router-dom";
import { useChatUI } from "../context/ChatUIContext";
import workflowConfig from '../config/workflowConfig';
import { getLoadedWorkflows, getWorkflow } from '../workflows/index';
import { dynamicUIHandler } from '../core/dynamicUIHandler';
import LoadingSpinner from '../utils/AgentChatLoadingSpinner';

// Debug utilities
const DEBUG_LOG_ALL_AGENT_OUTPUT = true;
const shouldDebugAllAgents = () => { try { const v = localStorage.getItem('mozaiks.debug_all_agents'); if (v!=null) return v==='1'||v==='true'; } catch{} return DEBUG_LOG_ALL_AGENT_OUTPUT; };
const logAgentOutput = (phase, agentName, content, meta={}) => { if(!shouldDebugAllAgents()) return; try { const prev = typeof content==='string'?content.slice(0,400):JSON.stringify(content); console.log(`🛰️ [${phase}]`, {agent:agentName||'Unknown', content:prev, ...meta}); } catch { console.log(`🛰️ [${phase}]`, {agent:agentName||'Unknown', content}); } };
// Generic localStorage gated debug flag helper (used for pipeline + render tracing)
const debugFlag = (k) => {
  try { return ['1','true','on','yes'].includes((localStorage.getItem(k)||'').toLowerCase()); } catch { return false; }
};

const formatHistoryTimestamp = (timestamp) => {
  if (!timestamp) return 'Just now';
  try {
    const parsed = new Date(timestamp);
    if (Number.isNaN(parsed.getTime())) return 'Just now';
    return parsed.toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return 'Just now';
  }
};

const AskHistorySidebar = ({
  sessions = [],
  activeChatId,
  loading,
  onSelectChat,
  onStartNewChat,
  onRefresh,
}) => {
  const hasSessions = Array.isArray(sessions) && sessions.length > 0;
  return (
    <aside className="hidden lg:flex flex-col w-64 xl:w-72 2xl:w-80 shrink-0 rounded-2xl border border-[rgba(var(--color-primary-light-rgb),0.25)] bg-[rgba(2,6,23,0.72)] backdrop-blur-xl shadow-[0_20px_60px_rgba(2,6,23,0.6)] px-4 py-4 text-sm text-slate-100">
      <div className="flex items-center justify-between gap-2">
        <div>
          <p className="text-[11px] tracking-[0.2em] uppercase text-[rgba(148,163,184,0.9)]">Ask</p>
          <h2 className="text-lg font-semibold">Recent Conversations</h2>
        </div>
        <button
          type="button"
          className="px-2 py-1 rounded-lg border border-[rgba(var(--color-primary-light-rgb),0.4)] text-[11px] tracking-wide text-[rgba(148,163,184,0.9)] hover:text-white hover:border-[rgba(var(--color-primary-light-rgb),0.8)] transition"
          onClick={onRefresh}
          disabled={loading}
        >
          {loading ? '…' : 'Refresh'}
        </button>
      </div>
      <div className="mt-3 flex flex-col gap-2">
        <button
          type="button"
          onClick={onStartNewChat}
          className="w-full inline-flex items-center justify-center gap-2 rounded-xl border border-[rgba(var(--color-secondary-rgb),0.35)] bg-[rgba(var(--color-secondary-rgb),0.12)] py-2 text-sm font-semibold text-white hover:border-[rgba(var(--color-secondary-rgb),0.55)] hover:bg-[rgba(var(--color-secondary-rgb),0.18)] transition"
        >
          <span className="text-base" aria-hidden="true">＋</span>
          New Chat
        </button>
        <p className="text-[12px] text-[rgba(148,163,184,0.9)]">{hasSessions ? `${sessions.length} saved session${sessions.length === 1 ? '' : 's'}` : 'No saved conversations yet.'}</p>
      </div>
      <div className="mt-3 flex-1 overflow-y-auto my-scroll1 pr-1 space-y-2">
        {hasSessions ? (
          sessions.map((session) => {
            const chatId = session?.chat_id;
            if (!chatId) return null;
            const isActive = chatId === activeChatId;
            const label = session?.label || `Chat ${chatId.slice(-4)}`;
            const summary = session?.summary || session?.last_message_preview || 'Tap to resume conversation.';
            const timestamp = formatHistoryTimestamp(session?.last_updated_at || session?.updated_at);
            return (
              <button
                key={chatId}
                type="button"
                onClick={() => onSelectChat(chatId)}
                className={`w-full text-left rounded-2xl border px-3 py-2 transition focus:outline-none focus:ring-2 focus:ring-[rgba(var(--color-primary-light-rgb),0.6)] ${isActive
                  ? 'border-[rgba(var(--color-primary-light-rgb),0.7)] bg-[rgba(var(--color-primary-rgb),0.15)] shadow-[0_10px_35px_rgba(6,182,212,0.15)]'
                  : 'border-[rgba(148,163,184,0.2)] bg-[rgba(15,23,42,0.65)] hover:border-[rgba(148,163,184,0.4)] hover:bg-[rgba(15,23,42,0.8)]'}`}
              >
                <div className="flex items-center justify-between text-[11px] text-[rgba(148,163,184,0.95)]">
                  <span className="font-semibold text-[rgba(226,232,240,0.9)] text-sm">{label}</span>
                  <span>{timestamp}</span>
                </div>
                <p className="mt-1 text-[13px] leading-relaxed text-[rgba(226,232,240,0.8)] max-h-[3.6rem] overflow-hidden">{summary}</p>
              </button>
            );
          })
        ) : (
          <div className="rounded-2xl border border-dashed border-[rgba(148,163,184,0.4)] px-3 py-6 text-center text-[13px] text-[rgba(148,163,184,0.9)]">
            Start a conversation to see it appear here.
          </div>
        )}
      </div>
    </aside>
  );
};

const MobileAskHistoryDrawer = ({
  open,
  sessions = [],
  activeChatId,
  loading,
  onSelectChat,
  onStartNewChat,
  onRefresh,
  onClose,
}) => {
  if (!open) {
    return null;
  }

  const hasSessions = Array.isArray(sessions) && sessions.length > 0;

  return (
    <div className="fixed inset-0 z-40 flex lg:hidden">
      <button
        type="button"
        aria-label="Close Ask history"
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        onClick={onClose}
      ></button>
      <div className="relative z-10 h-full w-[min(86vw,320px)] max-w-sm bg-[rgba(5,10,24,0.96)] backdrop-blur-2xl border-r border-[rgba(var(--color-primary-light-rgb),0.3)] shadow-[0_20px_60px_rgba(2,6,23,0.85)] flex flex-col">
        <div className="flex items-center justify-between px-4 py-3 border-b border-[rgba(var(--color-primary-light-rgb),0.25)]">
          <div>
            <p className="text-[10px] tracking-[0.3em] uppercase text-[rgba(148,163,184,0.8)]">Ask</p>
            <h2 className="text-base font-semibold text-white">Conversations</h2>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={onRefresh}
              disabled={loading}
              className="px-2 py-1 rounded-lg border border-[rgba(var(--color-primary-light-rgb),0.35)] text-[11px] text-[rgba(148,163,184,0.95)] hover:border-[rgba(var(--color-primary-light-rgb),0.7)] hover:text-white transition disabled:opacity-60"
            >
              {loading ? '…' : 'Refresh'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="w-8 h-8 rounded-full border border-[rgba(148,163,184,0.4)] text-white flex items-center justify-center hover:border-[rgba(var(--color-primary-light-rgb),0.7)]"
            >
              ✕
            </button>
          </div>
        </div>
        <div className="px-4 py-3 border-b border-[rgba(148,163,184,0.35)]">
          <button
            type="button"
            onClick={onStartNewChat}
            className="w-full inline-flex items-center justify-center gap-2 rounded-xl border border-[rgba(var(--color-secondary-rgb),0.35)] bg-[rgba(var(--color-secondary-rgb),0.15)] py-2 text-sm font-semibold text-white"
          >
            <span className="text-lg" aria-hidden="true">＋</span>
            New Chat
          </button>
          <p className="mt-2 text-[12px] text-[rgba(148,163,184,0.9)]">
            {hasSessions ? `${sessions.length} saved conversation${sessions.length === 1 ? '' : 's'}` : 'No saved conversations yet.'}
          </p>
        </div>
        <div className="flex-1 overflow-y-auto my-scroll1 px-3 py-3 space-y-2">
          {hasSessions ? (
            sessions.map((session) => {
              const chatId = session?.chat_id;
              if (!chatId) return null;
              const isActive = chatId === activeChatId;
              const label = session?.label || `Chat ${chatId.slice(-4)}`;
              const summary = session?.summary || session?.last_message_preview || 'Tap to resume conversation.';
              const timestamp = formatHistoryTimestamp(session?.last_updated_at || session?.updated_at);
              return (
                <button
                  key={chatId}
                  type="button"
                  onClick={() => {
                    onSelectChat(chatId);
                    onClose?.();
                  }}
                  className={`w-full text-left rounded-2xl border px-3 py-2 transition focus:outline-none focus:ring-2 focus:ring-[rgba(var(--color-primary-light-rgb),0.6)] ${isActive
                    ? 'border-[rgba(var(--color-primary-light-rgb),0.7)] bg-[rgba(var(--color-primary-rgb),0.15)] shadow-[0_10px_35px_rgba(6,182,212,0.15)]'
                    : 'border-[rgba(148,163,184,0.2)] bg-[rgba(15,23,42,0.65)] hover:border-[rgba(148,163,184,0.4)] hover:bg-[rgba(15,23,42,0.8)]'}`}
                >
                  <div className="flex items-center justify-between text-[11px] text-[rgba(148,163,184,0.95)]">
                    <span className="font-semibold text-[rgba(226,232,240,0.95)] text-sm">{label}</span>
                    <span>{timestamp}</span>
                  </div>
                  <p className="mt-1 text-[13px] leading-relaxed text-[rgba(226,232,240,0.85)] max-h-[3.6rem] overflow-hidden">{summary}</p>
                </button>
              );
            })
          ) : (
            <div className="rounded-2xl border border-dashed border-[rgba(148,163,184,0.4)] px-3 py-6 text-center text-[13px] text-[rgba(148,163,184,0.9)]">
              Start a conversation to see it appear here.
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

const ChatPage = () => {
  const navigate = useNavigate();
  const location = useLocation();
  // Core state
  const [messages, setMessages] = useState([]);
  // Ref mirror to access latest messages inside callbacks without stale closure
  const messagesRef = useRef([]);
  useEffect(() => { messagesRef.current = messages; }, [messages]);
  const [ws, setWs] = useState(null);
  const wsRef = useRef(null);
  const [loading, setLoading] = useState(true);
  const setMessagesWithLogging = useCallback(updater => setMessages(prev => typeof updater==='function'?updater(prev):updater), []);
  const [connectionStatus, setConnectionStatus] = useState('disconnected');
  const [transportType, setTransportType] = useState(null);
  const [currentChatId, setCurrentChatId] = useState(null); // set via start/resume flow below
  const LOCAL_STORAGE_KEY = 'mozaiks.current_chat_id';
  const [connectionInitialized, setConnectionInitialized] = useState(false);
  const [workflowConfigLoaded, setWorkflowConfigLoaded] = useState(false); // becomes true once workflow config resolved
  const [cacheSeed, setCacheSeed] = useState(null); // per-chat cache seed for unified backend/frontend caching
  const [chatExists, setChatExists] = useState(null); // tri-state: null=unknown, true=exists, false=new
  const connectionInProgressRef = useRef(false);
  // Guard to prevent overlapping start logic (used by preflight existence effect)
  const pendingStartRef = useRef(false);
  const conversationBootstrapRef = useRef(false);
  const pathSegments = location.pathname.split('/').filter(Boolean);
  let pathAppId = null;
  let pathWorkflowName = null;
  const isPrimaryChatRoute = pathSegments.length === 0 || pathSegments[0] === 'chat' || pathSegments[0] === 'app';
  const lastPrimaryRouteRef = useRef(isPrimaryChatRoute);

  if (pathSegments[0] === 'chat') {
    pathAppId = pathSegments[1] || null;
    pathWorkflowName = pathSegments[2] || null;
  } else if (pathSegments[0] === 'app') {
    pathAppId = pathSegments[1] || null;
    pathWorkflowName = pathSegments[2] || null;
  }

  const searchParams = new URLSearchParams(location.search || '');
  const queryAppId = searchParams.get('appId') || searchParams.get('app_id');
  const queryWorkflowName = searchParams.get('workflow');
  const queryChatId = searchParams.get('chat_id');

  const appId = pathAppId || queryAppId;
  const urlWorkflowName = pathWorkflowName || queryWorkflowName;
  const { 
    user, 
    api, 
    config, 
    activeChatId,
    setActiveChatId, 
    activeWorkflowName,
    setActiveWorkflowName,
    setChatMinimized,
    layoutMode,
    setLayoutMode,
    isInWidgetMode,
    setPreviousLayoutMode,
    setIsInWidgetMode,
    conversationMode,
    setConversationMode,
    activeGeneralChatId,
    setActiveGeneralChatId,
    generalChatSummary,
    setGeneralChatSummary,
    generalChatSessions,
    setGeneralChatSessions,
  } = useChatUI();
  const [isSidePanelOpen, setIsSidePanelOpen] = useState(false);
  const [forceOverlay, setForceOverlay] = useState(false);
  const [discoveryChatMinimized, setDiscoveryChatMinimized] = useState(false);
  const [isAskHistoryDrawerOpen, setIsAskHistoryDrawerOpen] = useState(false);
  
  // Mobile-specific state
  const [isMobileView, setIsMobileView] = useState(false);
  const [mobileDrawerState, setMobileDrawerState] = useState('peek'); // 'hidden' | 'peek' | 'expanded'
  const [hasUnseenChat, setHasUnseenChat] = useState(false);
  const [hasUnseenArtifact, setHasUnseenArtifact] = useState(false);
  
  // Current artifact messages rendered inside ArtifactPanel (not in chat messages)
  const [currentArtifactMessages, setCurrentArtifactMessages] = useState([]);
  // Track the most recent artifact-mode UI event id to manage auto-collapse
  const lastArtifactEventRef = useRef(null);
  // Track pending AG2 input request (when user should respond via submitInputRequest)
  const [pendingInputRequestId, setPendingInputRequestId] = useState(null);
  // Prevent duplicate restores per connection
  const artifactRestoredOnceRef = useRef(false);
  const artifactCacheValidRef = useRef(false);
  const lastErrorIdRef = useRef(null); // Track last error to prevent duplicates
  const workflowMessagesCacheRef = useRef([]);
  const generalMessagesCacheRef = useRef([]);
  const generalHydrationPendingRef = useRef(false);
  const workflowArtifactSnapshotRef = useRef({ isOpen: false, messages: [], layoutMode: 'split' });
  const queryResumeHandledRef = useRef(null);
  
  useEffect(() => {
    if (conversationMode === 'workflow') {
      workflowMessagesCacheRef.current = messages;
    } else {
      generalMessagesCacheRef.current = messages;
    }
  }, [messages, conversationMode]);
  
  // Note: Artifact panel restoration is handled directly in ensureWorkflowMode with setTimeout
  const implicitDevAppId =
    process.env.NODE_ENV !== 'production' &&
    (process.env.REACT_APP_ALLOW_IMPLICIT_APP_ID === 'true' || config?.auth?.mode === 'mock')
      ? 'local-dev'
      : null;

  const currentAppId =
    appId ||
    config?.chat?.defaultAppId ||
    process.env.REACT_APP_DEFAULT_APP_ID ||
    process.env.REACT_APP_DEFAULT_app_id ||
    implicitDevAppId ||
    null;
  const currentUserId = user?.id || config?.chat?.defaultUserId || '56132';
  const [generalSessionsLoading, setGeneralSessionsLoading] = useState(false);
  // Workflow completion state
  const [workflowCompleted, setWorkflowCompleted] = useState(false);
  const [completionData, setCompletionData] = useState(null);
  // Helper function to get default workflow from registry
  const getDefaultWorkflowFromRegistry = () => {
    const cfgDefault = typeof workflowConfig?.getDefaultWorkflow === 'function'
      ? workflowConfig.getDefaultWorkflow()
      : null;
    if (cfgDefault) return cfgDefault;

    const workflows = getLoadedWorkflows();
    if (!Array.isArray(workflows) || workflows.length === 0) return null;
    return workflows[0].name || null;
  };

  const defaultWorkflow = (urlWorkflowName || config?.chat?.defaultWorkflow || getDefaultWorkflowFromRegistry() || '');
  const [currentWorkflowName, setCurrentWorkflowName] = useState(defaultWorkflow);

  useEffect(() => {
    if (urlWorkflowName && urlWorkflowName !== currentWorkflowName) {
      console.log('🧭 [WORKFLOW_ROUTE] Updating workflow from URL param:', urlWorkflowName);
      setCurrentWorkflowName(urlWorkflowName);
    }
  }, [urlWorkflowName, currentWorkflowName]);
  // One-time initial spinner: show after websocket connects, hide after first agent chat.text message
  const [showInitSpinner, setShowInitSpinner] = useState(false);
  // Refs for race-free spinner control
  const initSpinnerShownRef = useRef(false);
  const initSpinnerHiddenOnceRef = useRef(false);
  // Track whether we suppressed the synthetic first instruction message (when no initial message configured)
  const firstAgentMessageSuppressedRef = useRef(false);
  // Removed legacy dynamic UI accumulation & dedupe refs (no longer needed with chat.* events)


  // Helper function to extract agent name from nested message structure
  const extractAgentName = useCallback((data) => {
    try {
      // First try direct agent field
      if (data.agent && data.agent !== 'Unknown') {
        return data.agent;
      }
      if (data.agentName && data.agentName !== 'Unknown') {
        return data.agentName;
      }
      if (data.agent_name && data.agent_name !== 'Unknown') {
        return data.agent_name;
      }
      
      // Parse content if it's a JSON string containing nested agent info
      if (data.content && typeof data.content === 'string') {
        const parsed = JSON.parse(data.content);
        if (parsed?.data?.content?.sender) {
          return parsed.data.content.sender;
        }
        if (parsed?.data?.agent) {
          return parsed.data.agent;
        }
      }
      
      return 'Agent'; // fallback
    } catch {
      return data.agent || data.agent_name || 'Agent';
    }
  }, []);

  const refreshGeneralSessions = useCallback(async () => {
    if (!api || !currentAppId || !currentUserId) {
      return [];
    }
    setGeneralSessionsLoading(true);
    try {
      const result = await api.listGeneralChats(currentAppId, currentUserId, 50);
      const sessions = Array.isArray(result?.sessions) ? result.sessions : [];
      setGeneralChatSessions(sessions);
      return sessions;
    } catch (err) {
      console.error('Failed to list general chats:', err);
      return [];
    } finally {
      setGeneralSessionsLoading(false);
    }
  }, [api, currentAppId, currentUserId, setGeneralChatSessions]);

  const mapGeneralMessage = useCallback((message) => {
    if (!message) {
      return null;
    }
    const timestamp = (() => {
      if (!message.timestamp) return Date.now();
      try {
        return new Date(message.timestamp).getTime();
      } catch (_) {
        return Date.now();
      }
    })();
    return {
      id: message.event_id || `general-${message.sequence || Date.now()}`,
      sender: message.role === 'assistant' ? 'agent' : 'user',
      agentName: message.role === 'assistant' ? 'Assistant' : 'You',
      content: message.content,
      isStreaming: false,
      timestamp,
      metadata: message.metadata || {},
    };
  }, []);

  const hydrateGeneralTranscript = useCallback(async (chatId, options = {}) => {
    if (!api || !chatId) {
      return;
    }
    try {
      const transcript = await api.fetchGeneralChatTranscript(currentAppId, chatId, options);
      if (!transcript) {
        return;
      }
      const normalized = (transcript.messages || [])
        .map(mapGeneralMessage)
        .filter(Boolean);
      generalMessagesCacheRef.current = normalized;
      if (conversationMode === 'ask') {
        setMessagesWithLogging(normalized);
      }
      setActiveGeneralChatId(transcript.chat_id);
      setGeneralChatSummary({
        chatId: transcript.chat_id,
        label: transcript.label || transcript.chat_id,
        lastUpdatedAt: transcript.last_updated_at,
        lastSequence: transcript.last_sequence,
      });
      setGeneralChatSessions((prev) => {
        if (!Array.isArray(prev) || !transcript.chat_id) {
          return prev;
        }
        const next = [...prev];
        const idx = next.findIndex((session) => session?.chat_id === transcript.chat_id);
        if (idx === -1) {
          return prev;
        }
        next[idx] = {
          ...next[idx],
          label: transcript.label || next[idx]?.label,
          last_updated_at: transcript.last_updated_at,
          lastSequence: transcript.last_sequence,
          sequence: transcript.sequence ?? next[idx]?.sequence,
        };
        return next;
      });
    } catch (err) {
      console.error('Failed to hydrate general chat transcript:', err);
    }
  }, [api, conversationMode, currentAppId, mapGeneralMessage, setActiveGeneralChatId, setGeneralChatSummary, setGeneralChatSessions, setMessagesWithLogging]);

  useEffect(() => {
    if (!api || !currentAppId || !currentUserId) {
      return;
    }
    refreshGeneralSessions();
  }, [api, currentAppId, currentUserId, refreshGeneralSessions]);

  useEffect(() => {
    if (conversationBootstrapRef.current) {
      return;
    }
    conversationBootstrapRef.current = true;
    if (conversationMode !== 'workflow') {
      setConversationMode('workflow');
    }
  }, [conversationMode, setConversationMode]);

  useEffect(() => {
    if (conversationMode === 'workflow') {
      const cached = workflowMessagesCacheRef.current;
      if (cached) {
        setMessagesWithLogging(cached);
      }
      return;
    }
    if (conversationMode !== 'ask') {
      return;
    }
    if (generalMessagesCacheRef.current && generalMessagesCacheRef.current.length > 0) {
      setMessagesWithLogging(generalMessagesCacheRef.current);
      return;
    }
    if ((!generalMessagesCacheRef.current || generalMessagesCacheRef.current.length === 0) && messagesRef.current.length > 0) {
      setMessagesWithLogging([]);
    }
    if (!activeGeneralChatId || generalHydrationPendingRef.current) {
      return;
    }
    generalHydrationPendingRef.current = true;
    Promise.resolve(hydrateGeneralTranscript(activeGeneralChatId)).finally(() => {
      generalHydrationPendingRef.current = false;
    });
  }, [activeGeneralChatId, conversationMode, hydrateGeneralTranscript, setMessagesWithLogging]);

  // Simplified incoming handler (namespaced chat.* only)
  const handleIncoming = useCallback((data) => {
    // Log all incoming events for debugging - you can filter this later if needed
    if (data?.type) {
      try { logAgentOutput('INCOMING', extractAgentName(data), data, { type: data?.type }); } catch {}
    }
    if (!data?.type) return;
    // Robust spinner hide: only once
    try {
      if (initSpinnerShownRef.current && !initSpinnerHiddenOnceRef.current) {
        const outerType = data.type || '';
        const isText = outerType === 'chat.text';
        const isInputRequest = outerType === 'chat.input_request';
        const serializedText = !isText && !isInputRequest && typeof data.content === 'string' && 
          (data.content.includes('"type":"chat.text"') || data.content.includes('"type":"chat.input_request"'));
        if (isText || isInputRequest || serializedText) {
          initSpinnerHiddenOnceRef.current = true;
          if (showInitSpinner) console.log('🧹 [SPINNER] Hiding spinner (interactive event). text?', isText, 'input?', isInputRequest, 'serialized?', serializedText);
          setShowInitSpinner(false);
        }
      }
    } catch {}
    if (debugFlag('mozaiks.debug_pipeline')) {
      const agentDbg = data.agent || data.agent_name;
      console.log('[PIPELINE] raw event', {
        type: data.type,
        agent: agentDbg,
        visual: data.is_visual,
        structuredCapable: data.is_structured_capable,
        hasStructuredOutput: !!data.structured_output,
        contentPreview: (data.content||'').slice(0,120)
      });
      if ((data.type === 'chat.print' || data.type === 'chat.text') && data.is_visual === false) {
        console.warn('[PIPELINE] Non-visual agent message received (unexpected?)', agentDbg);
      }
    }
    // Minimal legacy passthrough for still-emitted dynamic UI events until backend fully migrated
    if (data.type === 'ui_tool_event' || data.type === 'UI_TOOL_EVENT') {
      // Create a response callback that uses the WebSocket connection
      const sendResponse = (responseData) => {
        console.log('🔌 ChatPage: Sending WebSocket response:', responseData);
        const activeWs = wsRef.current;
        if (activeWs && activeWs.send) {
          return activeWs.send(responseData);
        } else {
          console.warn('⚠️ No WebSocket connection available for UI tool response');
          return false;
        }
      };
      
      console.debug('🔌 ChatPage: Passing sendResponse callback type:', typeof sendResponse, 'event:', data);
      // Auto-open artifact panel ONLY for display === 'artifact' (not inline components)
      try {
        const displayMode = data.display || data.display_type || data.mode;
        if (displayMode === 'artifact') {
          console.log('📊 [TELEMETRY] Auto-opening artifact panel for ui_tool_event:', {
            ui_tool_id: data.ui_tool_id,
            display: displayMode,
            workflow: currentWorkflowName,
            chat_id: currentChatId,
            isMobile: isMobileView
          });
          
          if (isMobileView) {
            // Mobile: surface the bottom drawer and clear artifact badge
            setIsSidePanelOpen(true);
            setMobileDrawerState('expanded');
            setHasUnseenArtifact(false);
          } else {
            // Desktop: Open split view as usual
            setLayoutMode && setLayoutMode('split');
            setIsSidePanelOpen && setIsSidePanelOpen(true);
          }
        }
      } catch (e) { /* ignore if not available */ }
      try {
        dynamicUIHandler.processUIEvent(data, sendResponse);
      } catch (err) {
        console.error('🔌 ChatPage: dynamicUIHandler.processUIEvent threw', err, data);
      }
      return;
    }
    
    // Handle chat_meta events (which may not have chat. prefix)
    if (data.type === 'chat_meta' || data.type === 'chat.chat_meta') {
      // Initial metadata handshake from backend
      console.log('🧬 [META] Received chat_meta event:', data);
      if (data.cache_seed !== undefined && data.cache_seed !== null) {
        setCacheSeed(data.cache_seed);
        if (currentChatId) {
          try { localStorage.setItem(`${LOCAL_STORAGE_KEY}.cache_seed.${currentChatId}`, String(data.cache_seed)); } catch {}
        }
        console.log('🧬 [META] Received cache_seed', data.cache_seed, 'for chat', currentChatId);
      }
      if (data.chat_exists === false) {
        // Backend indicates this chat_id had no persisted session (fresh after client-side reuse)
        setChatExists(false);
        console.log('🧬 [META] Backend reports chat did NOT previously exist. Suppressing artifact restore. chat_id=', currentChatId);
        try {
          // Purge any stale local artifacts for this chat to avoid ghost UI
          localStorage.removeItem(`mozaiks.last_artifact.${currentChatId}`);
          localStorage.removeItem(`mozaiks.current_artifact.${currentChatId}`);
          console.log('🧼 [META] Purged stale artifacts for non-existent chat');
        } catch {}
        // Reset any prior artifact state
        setCurrentArtifactMessages([]);
        lastArtifactEventRef.current = null;
        artifactCacheValidRef.current = false;
        artifactRestoredOnceRef.current = true; // prevent later restore effect
      } else if (data.chat_exists === true) {
        setChatExists(true);
        console.log('🧬 [META] Backend confirms chat exists. Artifact restore allowed.');
        if (!data.last_artifact || !data.last_artifact.ui_tool_id) {
          try {
            localStorage.removeItem(`mozaiks.last_artifact.${currentChatId}`);
            localStorage.removeItem(`mozaiks.current_artifact.${currentChatId}`);
          } catch {}
          setCurrentArtifactMessages([]);
          lastArtifactEventRef.current = null;
          artifactRestoredOnceRef.current = false;
          artifactCacheValidRef.current = false;
        }

        // If backend already sent last_artifact and we have not restored yet, cache it for restore effect
        if (!artifactRestoredOnceRef.current && data.last_artifact && data.last_artifact.ui_tool_id) {
          try {
            const key = `mozaiks.last_artifact.${currentChatId}`;
            localStorage.setItem(key, JSON.stringify({
              ui_tool_id: data.last_artifact.ui_tool_id,
              eventId: data.last_artifact.event_id || null,
              workflow_name: data.last_artifact.workflow_name || currentWorkflowName,
              payload: data.last_artifact.payload || {},
              display: data.last_artifact.display || 'artifact',
              ts: Date.now(),
            }));
            console.log('🧬 [META] Cached last_artifact from server meta event');
            artifactCacheValidRef.current = true;
          } catch (e) { console.warn('Failed to cache server last_artifact', e); }
        }
      }
      return;
    }
    
    // Some backends double-serialize the envelope: outer {type, content: JSON-stringified {type:"chat.text", data:{...}}}
    // Detect and unwrap once so downstream logic always works with a flat object.
    // Do this BEFORE checking for chat. prefix so we can unwrap "unknown" events that contain chat events.
    try {
      if (typeof data.content === 'string' && data.content.startsWith('{') && data.content.includes('"type":"')) {
        const inner = JSON.parse(data.content);
        if (inner && inner.type) {
          if (debugFlag('mozaiks.debug_pipeline')) {
            console.log('[PIPELINE] unwrapped nested envelope', { originalType: data.type, innerType: inner.type });
          }
          // If this is chat_meta, handle it directly
          if (inner.type === 'chat_meta') {
            console.log('🧬 [META] Received chat_meta event (unwrapped):', inner);
            const metaData = inner.data || {};
            if (metaData.cache_seed !== undefined && metaData.cache_seed !== null) {
              setCacheSeed(metaData.cache_seed);
              if (currentChatId) {
                try { localStorage.setItem(`${LOCAL_STORAGE_KEY}.cache_seed.${currentChatId}`, String(metaData.cache_seed)); } catch {}
              }
              console.log('🧬 [META] Received cache_seed', metaData.cache_seed, 'for chat', currentChatId);
            }
            if (metaData.chat_exists === false) {
              setChatExists(false);
              console.log('🧬 [META] Backend reports chat did NOT previously exist. Suppressing artifact restore. chat_id=', currentChatId);
              try {
                localStorage.removeItem(`mozaiks.last_artifact.${currentChatId}`);
                localStorage.removeItem(`mozaiks.current_artifact.${currentChatId}`);
                console.log('🧼 [META] Purged stale artifacts for non-existent chat');
              } catch {}
              setCurrentArtifactMessages([]);
              lastArtifactEventRef.current = null;
              artifactRestoredOnceRef.current = true;
              artifactCacheValidRef.current = false;
            } else if (metaData.chat_exists === true) {
              setChatExists(true);
              console.log('🧬 [META] Backend confirms chat exists. Artifact restore allowed.');
              if (!metaData.last_artifact || !metaData.last_artifact.ui_tool_id) {
                try {
                  localStorage.removeItem(`mozaiks.last_artifact.${currentChatId}`);
                  localStorage.removeItem(`mozaiks.current_artifact.${currentChatId}`);
                } catch {}
                setCurrentArtifactMessages([]);
                lastArtifactEventRef.current = null;
                artifactRestoredOnceRef.current = false;
                artifactCacheValidRef.current = false;
              }
              if (!artifactRestoredOnceRef.current && metaData.last_artifact && metaData.last_artifact.ui_tool_id) {
                try {
                  const key = `mozaiks.last_artifact.${currentChatId}`;
                  localStorage.setItem(key, JSON.stringify({
                    ui_tool_id: metaData.last_artifact.ui_tool_id,
                    eventId: metaData.last_artifact.event_id || null,
                    workflow_name: metaData.last_artifact.workflow_name || currentWorkflowName,
                    payload: metaData.last_artifact.payload || {},
                    display: metaData.last_artifact.display || 'artifact',
                    ts: Date.now(),
                  }));
                  console.log('🧬 [META] Cached last_artifact from server meta event');
                  artifactCacheValidRef.current = true;
                } catch (e) { console.warn('Failed to cache server last_artifact', e); }
              }
            }
            return;
          }
          // For other events, unwrap and continue processing
          const innerData = inner.data || {};
          data.type = inner.type; // Use the inner type (could be chat.*, unknown, etc.)
          if (typeof innerData.content === 'string') data.content = innerData.content;
          if (innerData.agent) data.agent = innerData.agent;
          if (innerData.agent_name) data.agent_name = innerData.agent_name;
          if (innerData.sender && !innerData.agent && !innerData.agent_name) data.agent = innerData.sender;
          // Capability flags
          data.is_structured_capable = !!innerData.is_structured_capable;
          if (innerData.is_visual !== undefined) data.is_visual = innerData.is_visual;
          if (innerData.is_tool_agent !== undefined) data.is_tool_agent = innerData.is_tool_agent;
          // Preserve structured output payloads if present
          if (innerData.structured_output !== undefined) data.structured_output = innerData.structured_output;
          if (innerData.structured_schema !== undefined) data.structured_schema = innerData.structured_schema;
          if (debugFlag('mozaiks.debug_pipeline')) {
            console.log('[PIPELINE] unwrap summary', {
              finalType: data.type,
              agent: data.agent || data.agent_name,
              visual: data.is_visual,
              structuredCapable: data.is_structured_capable,
              contentPreview: (data.content||'').slice(0,120)
            });
          }
        }
      }
    } catch (e) {
      if (debugFlag('mozaiks.debug_pipeline')) console.warn('[PIPELINE] failed to unwrap nested envelope', e);
    }
    
    // Allow chat.* events and other known event types (like input_request, unknown, etc.)
    if (!data.type.startsWith('chat.') && !['input_request', 'unknown'].includes(data.type)) return; // ignore unrecognized legacy

    // Some events may arrive already as { type:'chat.text', data:{ ...actualFields... } } (no double-serialization)
    // or after the above unwrap we can still retain an inner data object we need to promote.
    try {
      if (data.data && typeof data.data === 'object') {
        const inner = data.data;
        // Promote only if target field missing or clearly placeholder
        const promote = (k, aliasArr=[]) => {
          if (inner[k] === undefined) return;
          if (data[k] === undefined || data[k] === 'Unknown' || data[k] === null) data[k] = inner[k];
          // Apply aliases (e.g. sender -> agent) if primary absent
          aliasArr.forEach(alias => {
            if (data[alias] === undefined && inner[k] !== undefined) data[alias] = inner[k];
          });
        };
        promote('agent');
        promote('agent_name');
        // sender can act as agent fallback
        if (!data.agent && !data.agent_name && inner.sender) data.agent = inner.sender;
        // Core textual content (avoid overwriting if we already have a non-empty string)
        if (inner.content && (!data.content || !String(data.content).trim())) data.content = inner.content;
        // Capability / classification flags
        ['is_visual','is_structured_capable','is_tool_agent'].forEach(f => { if (inner[f] !== undefined && data[f] === undefined) data[f] = inner[f]; });
        // Structured output payload + schema
        if (inner.structured_output !== undefined && data.structured_output === undefined) data.structured_output = inner.structured_output;
        if (inner.structured_schema !== undefined && data.structured_schema === undefined) data.structured_schema = inner.structured_schema;
        // UI tool / component hints (input_request etc.) + error messages
        ['component_type','ui_tool_id','tool_name','tool_call_id','request_id','progress_percent','prompt','success','interaction_type','status','corr','call_id','payload','message','error_code'].forEach(f => {
          if (inner[f] !== undefined && data[f] === undefined) data[f] = inner[f];
        });
      }
    } catch (e) {
      if (debugFlag('mozaiks.debug_pipeline')) console.warn('[PIPELINE] failed to promote data.data fields', e);
    }

    // Final resolution / fallback normalization before dispatch
    try {
      if (typeof data.content === 'object' && data.content !== null) {
        // Some backends might leave content object like { content: 'text' }
        if (data.content.content && typeof data.content.content === 'string') {
          data.content = data.content.content;
        } else if (data.content.text && typeof data.content.text === 'string') {
          data.content = data.content.text;
        } else if (data.content.message && typeof data.content.message === 'string') {
          data.content = data.content.message;
        }
      }
      if (!data.agent && !data.agent_name && data.sender) data.agent = data.sender;
      if (debugFlag('mozaiks.debug_pipeline')) {
        console.log('[PIPELINE] resolved event pre-dispatch', {
          type: data.type,
          agent: data.agent || data.agent_name,
          visual: data.is_visual,
          structuredCapable: data.is_structured_capable,
          hasStructuredOutput: !!data.structured_output,
          contentPreview: (typeof data.content === 'string' ? data.content : JSON.stringify(data.content || '')).slice(0,120)
        });
      }
    } catch {}

    const evt = data.type.startsWith('chat.') ? data.type.slice(5) : data.type;
    switch (evt) {
      case 'mode_changed': {
        const payload = data.data || {};
        const nextMode = payload.mode || payload.status;
        if (nextMode === 'general') {
          setConversationMode('ask');
          const generalId = payload.general_chat_id || payload.chat_id;
          if (generalId) {
            setActiveGeneralChatId(generalId);
            setGeneralChatSummary({
              chatId: generalId,
              label: payload.general_chat_label || payload.label || 'Ask',
              lastUpdatedAt: payload.last_updated_at || payload.timestamp || null,
              lastSequence: payload.general_chat_sequence,
            });
            if (!generalHydrationPendingRef.current) {
              generalHydrationPendingRef.current = true;
              Promise.resolve(hydrateGeneralTranscript(generalId)).finally(() => {
                generalHydrationPendingRef.current = false;
              });
            }
          }
          refreshGeneralSessions();
        } else if (nextMode === 'workflow') {
          setConversationMode('workflow');
        }
        if (payload.message) {
          setMessagesWithLogging((prev) => ([
            ...prev,
            {
              id: `mode-msg-${Date.now()}`,
              sender: 'system',
              agentName: 'System',
              content: payload.message,
              isStreaming: false,
            }
          ]));
        }
        return;
      }
      case 'general_session_created': {
        const payload = data.data || {};
        const generalId = payload.general_chat_id || payload.chat_id;
        if (generalId) {
          generalMessagesCacheRef.current = [];
          setActiveGeneralChatId(generalId);
          setGeneralChatSummary({
            chatId: generalId,
            label: payload.general_chat_label || payload.label || 'Ask',
            lastUpdatedAt: payload.last_updated_at || payload.timestamp || null,
            lastSequence: payload.general_chat_sequence,
          });
          setConversationMode('ask');
          generalHydrationPendingRef.current = true;
          Promise.resolve(hydrateGeneralTranscript(generalId)).finally(() => {
            generalHydrationPendingRef.current = false;
          });
        }
        refreshGeneralSessions();
        return;
      }
      case 'context_switched': {
        const payload = data.data || {};
        const targetChatId = payload.to_chat_id || payload.chat_id;
        if (targetChatId) {
          setCurrentChatId(targetChatId);
          setActiveChatId(targetChatId);
          try { localStorage.setItem(LOCAL_STORAGE_KEY, targetChatId); } catch {}
        }
        if (payload.workflow_name) {
          setCurrentWorkflowName(payload.workflow_name);
          setActiveWorkflowName(payload.workflow_name);
        }
        setConversationMode('workflow');
        if (payload.message) {
          setMessagesWithLogging((prev) => ([
            ...prev,
            {
              id: `ctx-msg-${Date.now()}`,
              sender: 'system',
              agentName: 'System',
              content: payload.message,
              isStreaming: false,
            }
          ]));
        }
        return;
      }
      case 'print': {
        // Suppress very first auto-generated instruction style message if workflow has no explicit initial_message_to_user
        try {
          if (!firstAgentMessageSuppressedRef.current) {
            const wfCfg = workflowConfig?.getWorkflowConfig(currentWorkflowName);
            const hasInitial = !!(wfCfg && wfCfg.initial_message_to_user && wfCfg.initial_message_to_user.trim());
            const seq = data.sequence || data.data?.sequence;
            if (!hasInitial && (seq === 1 || seq === '1')) {
              if (debugFlag('mozaiks.debug_pipeline')) console.log('[PIPELINE] suppressing first agent print message (no initial_message_to_user configured)');
              firstAgentMessageSuppressedRef.current = true; // one-time action
              if (showInitSpinner) setShowInitSpinner(false); // still hide spinner even if we suppress message
              return; // skip rendering
            }
          }
        } catch {}
        const agentName = extractAgentName(data);
        const chunk = data.content || '';
        if (!chunk) return;
        setMessagesWithLogging(prev => {
          const updated = [...prev];
            for (let i = updated.length -1; i>=0; i--) {
              const m = updated[i];
              if (m.__streaming && m.agentName === agentName) {
                m.content += chunk;
                if (debugFlag('mozaiks.debug_pipeline')) {
                  console.log('[PIPELINE] appended chunk to existing stream', { agent: agentName, newLength: m.content.length });
                }
                return updated;
              }
            }
          // Capability flags (new schema) from backend event data
          const isStructuredCapable = !!data.is_structured_capable;
          const isVisual = !!data.is_visual;
          const isToolAgent = !!data.is_tool_agent;
          updated.push({
            id:`stream-${Date.now()}`,
            sender:'agent',
            agentName,
            content:chunk,
            isStreaming:true,
            __streaming:true,
            isStructuredCapable,
            isVisual,
            isToolAgent
          });
          if (debugFlag('mozaiks.debug_pipeline')) {
            console.log('[PIPELINE] created new streaming message', { agent: agentName, chunkLen: chunk.length, isStructuredCapable, isVisual });
          }
          return updated;
        });
        return;
      }
      case 'text': {
        // Suppress very first auto-generated instruction style message if workflow has no explicit initial_message_to_user
        try {
          if (!firstAgentMessageSuppressedRef.current) {
            const wfCfg = workflowConfig?.getWorkflowConfig(currentWorkflowName);
            const hasInitial = !!(wfCfg && wfCfg.initial_message_to_user && wfCfg.initial_message_to_user.trim());
            const seq = data.sequence || data.data?.sequence;
            if (!hasInitial && (seq === 1 || seq === '1')) {
              if (debugFlag('mozaiks.debug_pipeline')) console.log('[PIPELINE] suppressing first agent text message (no initial_message_to_user configured)');
              firstAgentMessageSuppressedRef.current = true; // one-time action
              if (showInitSpinner) {
                console.log('🧹 [SPINNER] Hiding spinner due to suppressed first message');
                setShowInitSpinner(false); // hide spinner even if message suppressed
              }
              return; // skip rendering
            }
          }
        } catch {}
        const content = data.content || '';
        const metadataSource = data.metadata || data.data?.metadata || {};
        const normalizedAgent = (data.agent || data.agent_name || data.sender || '').toLowerCase();
        const isGeneralUserEcho = metadataSource?.source === 'general_agent' && normalizedAgent === 'user';
        const isWorkflowUserMessage = !isGeneralUserEcho && (
          normalizedAgent === 'user' ||
          (data.role && String(data.role).toLowerCase() === 'user') ||
          metadataSource?.source === 'workflow_user' ||
          Boolean(metadataSource?.input_request_id)
        );
        if (isGeneralUserEcho) {
          const recent = messagesRef.current[messagesRef.current.length - 1];
          if (recent && recent.sender === 'user') {
            const recentText = String(recent.content || '').trim();
            if (recentText && recentText === String(content).trim()) {
              return;
            }
          }
        }
        // Enhanced suppression for assistant parroting user input (including minor variation)
        try {
          if (content) {
            const msgs = messagesRef.current;
            if (msgs.length) {
              const lastUserIdx = [...msgs].reverse().findIndex(m => m && m.sender === 'user');
              if (lastUserIdx !== -1) {
                const actualIndex = msgs.length - 1 - lastUserIdx;
                const lastUser = msgs[actualIndex];
                if (lastUser) {
                  const normUser = String(lastUser.content||'').trim();
                  const normContent = String(content).trim();
                  // Simple fuzzy: treat as echo if one contains the other and length difference small
                  const shorter = normUser.length <= normContent.length ? normUser : normContent;
                  const longer = normUser.length > normContent.length ? normUser : normContent;
                  const lengthDiff = Math.abs(normUser.length - normContent.length);
                  const containsRel = longer.includes(shorter);
                  const smallDiff = lengthDiff <= 3; // allow small typos / spacing differences
                  const identical = normUser === normContent;
                  if (normUser && (identical || (containsRel && smallDiff))) {
                    if (debugFlag('mozaiks.debug_pipeline')) console.log('[PIPELINE] suppressing assistant echo/fuzzy repeat of user content');
                    return; // skip echo-like repetition
                  }
                }
              }
            }
          }
        } catch {}
        if (!content.trim()) return;
        const displayAsUser = isGeneralUserEcho || isWorkflowUserMessage;
        const agentName = displayAsUser ? 'You' : extractAgentName(data);
        const computedSender = displayAsUser ? 'user' : 'agent';
        setMessagesWithLogging(prev => {
          // Remove any thinking messages when agent actually speaks
          const thinkingMessages = prev.filter(m => m.isThinking);
          if (thinkingMessages.length > 0) {
            console.log('💭 [THINKING] Text event received - removing', thinkingMessages.length, 'thinking bubble(s)');
          }
          const filtered = prev.filter(m => !m.isThinking);
          const updated = [...filtered];
          
          if (updated.length) {
            const last = updated[updated.length-1];
            if (last.__streaming && last.agentName === agentName) {
              last.isStreaming = false; delete last.__streaming; if(!last.content.endsWith(content)) last.content+=content; return updated;
            }
          }
          
          // Capability + structured output payload (if any) from new unified dispatcher
          const isStructuredCapable = !!data.is_structured_capable;
          const structuredOutput = data.structured_output || null; // actual structured content (if produced)
          const structuredSchema = data.structured_schema || null; // schema describing structuredOutput
          const isVisual = !!data.is_visual;
          const isToolAgent = !!data.is_tool_agent;
          updated.push({
            id:`text-${Date.now()}`,
            sender: computedSender,
            agentName,
            content,
            isStreaming:false,
            isStructuredCapable,
            structuredOutput,
            structuredSchema,
            isVisual,
            isToolAgent,
            metadata: metadataSource
          });
          if (debugFlag('mozaiks.debug_pipeline')) {
            console.log('[PIPELINE] appended final text message', { agent: agentName, len: content.length, isStructuredCapable, hasStructuredOutput: !!structuredOutput });
          }
          return updated;
        });
        if (metadataSource?.general_chat_id) {
          setGeneralChatSummary((prev) => ({
            chatId: metadataSource.general_chat_id,
            label: metadataSource.general_chat_label || prev?.label || 'Ask',
            lastUpdatedAt: Date.now(),
            lastSequence: metadataSource.general_chat_sequence || metadataSource.sequence || prev?.lastSequence,
          }));
        }
        
        // Badge notification: Set unseen chat badge if artifact drawer is covering chat
        if (isMobileView && mobileDrawerState === 'expanded') {
          setHasUnseenChat(true);
        }
        
        // Hide the one-time initialization spinner after the first successfully rendered chat.text
        if (showInitSpinner) {
          setShowInitSpinner(false);
        }
        // Auto-open artifact panel for visual outputs with display === 'artifact'
        try {
          const hasStructuredOutput = data.structured_output && Object.keys(data.structured_output).length > 0;
          const displayMode = data.display || data.display_type || data.mode || 
                              (data.structured_output && data.structured_output.display);
          
          // Only auto-open if explicitly marked as artifact display (not inline)
          if ((hasStructuredOutput || data.is_visual) && displayMode === 'artifact') {
            console.log('📊 [TELEMETRY] Auto-opening artifact panel for text event:', {
              agent: agentName,
              is_visual: data.is_visual,
              has_structured_output: hasStructuredOutput,
              display: displayMode,
              workflow: currentWorkflowName,
              chat_id: currentChatId
            });
            setLayoutMode && setLayoutMode('split');
            setIsSidePanelOpen && setIsSidePanelOpen(true);
          }
        } catch (e) {}
        return;
      }
      case 'input_request': {
        console.log('📥 [INPUT_REQUEST] Received input_request event:', data);
        if (data.request_id) {
          console.log('🔖 [INPUT_REQUEST] Storing pending request_id:', data.request_id);
          setPendingInputRequestId(data.request_id);
        }
        const componentType = data.component_type || data.ui_tool_id || null;
        if (componentType) {
          const payload = {
            type: 'user_input_request',
            data: {
              input_request_id: data.request_id,
              chat_id: currentChatId,
              payload: {
                prompt: data.prompt,
                ui_tool_id: componentType,
                workflow_name: currentWorkflowName
              }
            }
          };
          console.debug('🧩 [UI_EVENT] input_request -> processUIEvent payload:', payload);
          try {
            dynamicUIHandler.processUIEvent(payload);
          } catch (err) {
            console.error('🧩 [UI_EVENT] dynamicUIHandler.processUIEvent threw for input_request', err, data);
          }
        } else if (data.prompt) {
          console.log('📝 [INPUT_REQUEST] Simple input request:', data.prompt);
        }
        return;
      }
      case 'tool_call': {
        if (data.component_type || (data.data && data.data.component_type)) {
          const envelope = data || {};
          const detail = envelope.data || {};
          const toolName = envelope.tool_name || detail.tool_name || detail.tool || envelope.ui_tool_id || 'UnknownTool';
          const componentType = envelope.component_type || detail.component_type || detail.component || toolName;
          const basePayload = detail.payload || envelope.payload || {};
          const payloadKeys = Object.keys(basePayload);
          console.log('🛠️ [TOOL_CALL] Processing UI tool event:', toolName, 'component:', componentType);
          console.log('🛠️ [TOOL_CALL] Raw tool_call data:', envelope);
          console.log('🛠️ [TOOL_CALL] Payload keys received:', payloadKeys);
          const derivedDisplay = envelope.display || envelope.display_type || envelope.mode || detail.display || detail.display_type || detail.mode || basePayload.display || basePayload.mode || null;
          const eventId = envelope.tool_call_id || envelope.corr || detail.tool_call_id || detail.corr || null;
          const awaiting = envelope.awaiting_response !== undefined ? envelope.awaiting_response : detail.awaiting_response;
          const sendResponse = (responseData) => {
            console.log('dY"O ChatPage: Sending WebSocket response for tool_call:', responseData);
            const activeWs = wsRef.current;
            if (activeWs && activeWs.send) {
              return activeWs.send(responseData);
            }
            console.warn('���s������,? No WebSocket connection available for UI tool response (tool_call)');
            return false;
          };
          dynamicUIHandler.processUIEvent({
            type: 'ui_tool_event',
            ui_tool_id: toolName,
            eventId: eventId || undefined,
            workflow_name: currentWorkflowName,
            display: derivedDisplay,
            agent: envelope.agent || detail.agent || envelope.agent_name || detail.agent_name,
            agentName: envelope.agent || detail.agent || envelope.agent_name || detail.agent_name,
            payload: {
              ...basePayload,
              tool_name: toolName,
              component_type: componentType,
              workflow_name: currentWorkflowName,
              awaiting_response: awaiting,
              ...(derivedDisplay ? { display: derivedDisplay } : {})
            }
          }, sendResponse);
          // Auto-open artifact panel ONLY for display === 'artifact'
          try {
            if (derivedDisplay === 'artifact') {
              console.log('📊 [TELEMETRY] Auto-opening artifact panel for tool_call:', {
                tool_name: toolName,
                component_type: componentType,
                display: derivedDisplay,
                workflow: currentWorkflowName,
                chat_id: currentChatId
              });
              setLayoutMode && setLayoutMode('split');
              setIsSidePanelOpen && setIsSidePanelOpen(true);
            }
          } catch (e) {}
        } else {
          setMessagesWithLogging(prev => [...prev, { id: data.tool_call_id || `tool-call-${Date.now()}`, sender:'system', agentName:'System', content:`🔧 Tool Call: ${data.tool_name}`, isStreaming:false }]);
        }
        return;
      }
      case 'ui_tool_complete':
      case 'chat.ui_tool_complete': {
        const envelope = data || {};
        const detail = envelope.data || {};
        const completedId = detail.event_id || envelope.event_id || detail.eventId || envelope.eventId || null;
        const completedTool = detail.ui_tool_id || envelope.ui_tool_id || detail.tool_name || envelope.tool_name || null;
        const status = detail.status || envelope.status || 'completed';
        
        console.log('✓ [UI_COMPLETE] Inline tool completed:', { completedId, completedTool, status });

        if (completedId) {
          // Mark the message as completed so it renders as a collapsed badge
          setMessagesWithLogging((prev) =>
            prev.map((msg) => {
              if (msg?.metadata?.eventId === completedId && msg?.metadata?.display === 'inline') {
                return {
                  ...msg,
                  ui_tool_completed: true,
                  ui_tool_status: status,
                  ui_tool_summary: `${completedTool || 'Tool'} completed`
                };
              }
              return msg;
            })
          );
        }
        return;
      }
      case 'ui_tool_dismiss':
      case 'chat.ui_tool_dismiss': {
        const envelope = data || {};
        const detail = envelope.data || {};
        const dismissedId = detail.event_id || envelope.event_id || detail.eventId || envelope.eventId || null;
        const dismissedTool = detail.ui_tool_id || envelope.ui_tool_id || detail.tool_name || envelope.tool_name || null;
        console.log('🧩 [UI] Received ui_tool_dismiss event:', { dismissedId, dismissedTool });

        if (dismissedId) {
          setMessagesWithLogging((prev) =>
            prev.filter(
              (msg) => !(msg?.metadata?.eventId === dismissedId && msg?.metadata?.type === 'ui_tool_agent_message')
            )
          );
        }

        if (lastArtifactEventRef.current && (!dismissedId || dismissedId === lastArtifactEventRef.current)) {
          try { console.log('🧹 [UI] Backend-dismissed artifact event -> collapsing panel'); } catch {}
          setIsSidePanelOpen(false);
          lastArtifactEventRef.current = null;
          artifactCacheValidRef.current = false;
          setCurrentArtifactMessages([]);
          if (currentChatId) {
            try {
              localStorage.removeItem(`mozaiks.current_artifact.${currentChatId}`);
              localStorage.removeItem(`mozaiks.last_artifact.${currentChatId}`);
            } catch {}
          }
        }
        return;
      }
      case 'tool_response': {
        // Suppress intermediate auto-tool responses (already handled by dynamicUIHandler)
        // Only show failures or non-auto-tool responses
        if (data.interaction_type === 'auto_tool' && data.success) {
          console.log(`⏭️ Skipping auto-tool success response (${data.tool_name}) - handled by UI renderer`);
          return;
        }
        const responseContent = data.success ? `✅ Tool Response: ${data.content || 'Success'}` : `❌ Tool Failed: ${data.content || 'Error'}`;
        setMessagesWithLogging(prev => [...prev, { id: data.tool_call_id || `tool-response-${Date.now()}`, sender:'system', agentName:'System', content: responseContent, isStreaming:false }]);
        return;
      }
      case 'usage_summary': {
        setMessagesWithLogging(prev => [...prev, { id:`usage-${Date.now()}`, sender:'system', agentName:'System', content:`📊 Usage: tokens=${data.total_tokens} prompt=${data.prompt_tokens} completion=${data.completion_tokens}${data.cost?` cost=$${data.cost}`:''}`, isStreaming:false }]);
        return;
      }
      case 'select_speaker': {
        // Speaker selection marks a new agent taking over - inject thinking state
        const nextAgentName = data.agent || data.agent_name || data.selected_speaker || 'Agent';
        
        console.log('💭 [THINKING] Speaker selected:', nextAgentName, '- adding thinking bubble');
        
        // Add a temporary "thinking" message that will be removed when next agent speaks
        setMessagesWithLogging(prev => {
          // Remove any existing thinking messages first
          const existingThinking = prev.filter(m => m.isThinking);
          if (existingThinking.length > 0) {
            console.log('💭 [THINKING] Removing', existingThinking.length, 'existing thinking bubble(s) before adding new one');
          }
          
          const filtered = prev.filter(m => !m.isThinking);
          return [
            ...filtered,
            {
              id: `thinking-${Date.now()}`,
              sender: 'agent',
              agentName: nextAgentName,
              content: '', // Empty content - will show thinking indicator
              isThinking: true,
              timestamp: Date.now()
            }
          ];
        });
        
        // Speaker selection often marks a new turn/run start. If we have an open artifact
        // from a prior sequence, collapse it now and clear the cache.
        if (lastArtifactEventRef.current && isSidePanelOpen) {
          try {
            console.log('🧹 [UI] New sequence detected; collapsing ArtifactPanel (event:', lastArtifactEventRef.current, ')');
          } catch {}
          setIsSidePanelOpen(false);
          lastArtifactEventRef.current = null;
          setCurrentArtifactMessages([]);
          // Clear artifact cache on new conversation turn
          try {
            if (currentChatId) {
              localStorage.removeItem(`mozaiks.current_artifact.${currentChatId}`);
              localStorage.removeItem(`mozaiks.last_artifact.${currentChatId}`);
            }
          } catch {}
        }
        return;
      }
      case 'tool_progress': {
        // Update or append progress for a long-running tool
        const progress = data.progress_percent;
        const tool = data.tool_name || 'tool';
        setMessagesWithLogging(prev => {
          const updated = [...prev];
          for (let i = updated.length - 1; i >=0; i--) {
            const m = updated[i];
            if (m.metadata && m.metadata.event_type === 'tool_call' && m.metadata.tool_name === tool) {
              m.content = `🔧 ${tool} progress: ${progress}%`;
              m.metadata.progress_percent = progress;
              return updated;
            }
          }
          updated.push({ id:`tool-progress-${Date.now()}`, sender:'system', agentName:'System', content:`🔧 ${tool} progress: ${progress}%`, isStreaming:false, metadata:{ event_type:'tool_progress', tool_name: tool, progress_percent: progress }});
          return updated;
        });
        return;
      }
      case 'deployment_started': {
        const payload = data.data || {};
        const message = payload.message || 'Starting deployment to GitHub...';
        setMessagesWithLogging(prev => [...prev, {
          id: `deploy-start-${Date.now()}`,
          sender: 'system',
          agentName: 'System',
          content: `🚀 ${message}`,
          isStreaming: false,
          metadata: { event_type: 'deployment', status: 'started', job_id: payload.job_id || payload.jobId || null }
        }]);
        return;
      }
      case 'deployment_progress': {
        const payload = data.data || {};
        const jobId = payload.job_id || payload.jobId || null;
        const statusText = payload.status || payload.message || 'Deployment in progress...';
        setMessagesWithLogging(prev => {
          const updated = [...prev];
          if (jobId) {
            for (let i = updated.length - 1; i >= 0; i--) {
              const m = updated[i];
              if (m.metadata && m.metadata.event_type === 'deployment' && m.metadata.job_id === jobId) {
                m.content = `⏳ ${statusText}`;
                m.metadata.status = 'progress';
                return updated;
              }
            }
          }
          updated.push({
            id: `deploy-progress-${Date.now()}`,
            sender: 'system',
            agentName: 'System',
            content: `⏳ ${statusText}`,
            isStreaming: false,
            metadata: { event_type: 'deployment', status: 'progress', job_id: jobId }
          });
          return updated;
        });
        return;
      }
      case 'deployment_completed': {
        const payload = data.data || {};
        const repoUrl = payload.repo_url || payload.repoUrl;
        const message = payload.message || 'Deployment completed.';
        setMessagesWithLogging(prev => [...prev, {
          id: `deploy-done-${Date.now()}`,
          sender: 'system',
          agentName: 'System',
          content: repoUrl ? `✅ ${message} Repo: ${repoUrl}` : `✅ ${message}`,
          isStreaming: false,
          metadata: { event_type: 'deployment', status: 'completed', job_id: payload.job_id || payload.jobId || null, repo_url: repoUrl || null }
        }]);
        return;
      }
      case 'deployment_failed': {
        const payload = data.data || {};
        const message = payload.message || payload.error || 'Deployment failed.';
        setMessagesWithLogging(prev => [...prev, {
          id: `deploy-fail-${Date.now()}`,
          sender: 'system',
          agentName: 'System',
          content: `❌ ${message}`,
          isStreaming: false,
          metadata: { event_type: 'deployment', status: 'failed', job_id: payload.job_id || payload.jobId || null }
        }]);
        return;
      }
      case 'input_timeout': {
        setMessagesWithLogging(prev => [...prev, { id:`timeout-${Date.now()}`, sender:'system', agentName:'System', content:`⏱️ Input request timed out.`, isStreaming:false }]);
        return;
      }
      case 'run_complete': {
        console.log('🎉 [COMPLETION] Workflow completed:', data);
        
        // Extract completion metadata
        const reason = data.reason || data.data?.reason || 'finished';
        const status = data.status || data.data?.status || 1;
        const duration = data.duration_sec || data.data?.duration_sec;
        const tokensUsed = data.total_tokens || data.data?.total_tokens;
        
        // Set completion state to show WorkflowCompletion component
        setWorkflowCompleted(true);
        setCompletionData({
          reason,
          status,
          duration: duration ? `${Math.round(duration)}s` : null,
          tokensUsed: tokensUsed ? tokensUsed.toLocaleString() : null,
        });
        
        // Also add a system message to chat history
        setMessagesWithLogging(prev => [...prev, { 
          id:`run-complete-${Date.now()}`, 
          sender:'system', 
          agentName:'System', 
          content:`✅ Workflow complete (${reason})`, 
          isStreaming:false 
        }]);
        return;
      }
      case 'error': {
        const errorMsg = data.message || data.data?.message || 'Unknown error';
        const errorCode = data.error_code || data.data?.error_code;
        // Create a stable ID based on error content to prevent duplicates
        const errorId = `${errorCode || 'error'}-${errorMsg.slice(0, 50)}`;
        
        // Prevent duplicate errors (React StrictMode or event replay)
        if (lastErrorIdRef.current === errorId) {
          console.log('🔴 [ERROR_EVENT] Duplicate error detected, skipping:', errorId);
          return;
        }
        lastErrorIdRef.current = errorId;
        
        console.log('🔴 [ERROR_EVENT] Received error event:', { message: errorMsg, error_code: errorCode, raw_data: data });
        setMessagesWithLogging(prev => [...prev, { id:`err-${Date.now()}`, sender:'system', agentName:'System', content:`❌ Error: ${errorMsg}`, isStreaming:false }]);
        return;
      }
      case 'input_ack':
        // Acknowledgment: no UI mutation needed
        return;
      case 'resume_boundary':
  // Replay boundary marker: insert a divider system note
  setMessagesWithLogging(prev => [...prev, { id:`resume-${Date.now()}`, sender:'system', agentName:'System', content:`🔄 Session replay complete. Live events resumed.`, isStreaming:false }]);
        return;
      default:
        return;
    }
  }, [currentChatId, currentWorkflowName, setMessagesWithLogging, extractAgentName, isSidePanelOpen, showInitSpinner, setLayoutMode, isMobileView, mobileDrawerState, setConversationMode, setActiveGeneralChatId, setGeneralChatSummary, hydrateGeneralTranscript, refreshGeneralSessions, setActiveChatId, setActiveWorkflowName, setCurrentChatId]);

  // Debug: Log spinner state changes
  useEffect(() => {
    console.log('🧹 [SPINNER] State changed to:', showInitSpinner);
  }, [showInitSpinner]);

  // Workflow configuration & resume bootstrap (no direct startChat here; handled by preflight existence effect)
  useEffect(() => {
    if (!api) return;

    // Hard requirement: app_id must be set via route/query/env (no implicit fallback)
    if (!currentAppId) {
      console.error(
        "Missing app_id. Provide it via URL (/chat/<app_id>), query (?appId=...), or REACT_APP_DEFAULT_APP_ID."
      );
      setConnectionStatus('error');
      setLoading(false);
      // Allow retry once the user fixes config (don’t lock the flags)
      setConnectionInitialized(false);
      connectionInProgressRef.current = false;
      return;
    }
    setWorkflowConfigLoaded(true);
    if (!currentChatId) {
      let stored = null;
      try { stored = localStorage.getItem(LOCAL_STORAGE_KEY); } catch {}
      if (stored) {
        setCurrentChatId(stored);
        try {
          const seedStored = localStorage.getItem(`${LOCAL_STORAGE_KEY}.cache_seed.${stored}`);
          if (seedStored) {
            setCacheSeed(Number(seedStored));
            console.log('🧬 [RESUME] Loaded cached cache_seed for resumed chat', stored, seedStored);
          }
        } catch {}
      }
    }
  }, [api, currentChatId, currentAppId]);

  // NEW: Preflight chat existence + cache clearing logic
useEffect(() => {
  if (!api) return;
  if (!workflowConfigLoaded) return; // wait until registry is ready
  if (currentChatId) return; // existing logic handles resume or already started
  if (pendingStartRef.current) return;

  pendingStartRef.current = true;
  (async () => {
    try {
      const urlParams = new URLSearchParams(window.location.search);
      const chatIdParam = urlParams.get('chat_id');
      let reuseChatId = chatIdParam;

      if (reuseChatId) {
        console.log('[EXISTS] Checking existence of chat', reuseChatId);
        try {
          const wfName = currentWorkflowName;
          const resp = await fetch(`http://localhost:8000/api/chats/exists/${currentAppId}/${wfName}/${reuseChatId}`);
          if (resp.ok) {
            const data = await resp.json();
            if (data.exists) {
              console.log('[EXISTS] Chat exists; adopting chat_id and skipping startChat');
              setCurrentChatId(reuseChatId);
              setChatExists(true);
              
              // Update global chat context for persistent bubble
              setActiveChatId(reuseChatId);
              setActiveWorkflowName(currentWorkflowName);
              setChatMinimized(false);
              
              pendingStartRef.current = false;
              return;
            } else {
              console.log('[EXISTS] Chat does NOT exist; clearing any cached artifacts for that id');
              try {
                localStorage.removeItem(`mozaiks.last_artifact.${reuseChatId}`);
                localStorage.removeItem(`mozaiks.current_artifact.${reuseChatId}`);
              } catch {}
            }
          }
        } catch (e) {
          console.warn('[EXISTS] Existence check failed:', e);
        }
      }

      console.log('[INIT] Creating new chat via startChat');
      const result = await api.startChat(currentAppId, currentWorkflowName, currentUserId);
      if (result && (result.chat_id || result.id)) {
        const newId = result.chat_id || result.id;
        const reused = !!result.reused;
        setCurrentChatId(newId);
        setChatExists(reused);
        
        // Update global chat context for persistent bubble
        setActiveChatId(newId);
        setActiveWorkflowName(currentWorkflowName);
        setChatMinimized(false);
        try { localStorage.setItem(LOCAL_STORAGE_KEY, newId); } catch {}
        if (!reused) {
          try {
            localStorage.removeItem(`mozaiks.last_artifact.${newId}`);
            localStorage.removeItem(`mozaiks.current_artifact.${newId}`);
          } catch {}
        }
        console.log('[INIT] startChat complete', { newId, reused });
      }
    } catch (e) {
      console.error('[INIT] Failed to initialize chat:', e);
    } finally {
      pendingStartRef.current = false;
    }
  })();
  // eslint-disable-next-line react-hooks/exhaustive-deps
}, [api, workflowConfigLoaded, currentChatId, currentWorkflowName, currentAppId, currentUserId]);

  // Expose a helper to force-reset the current chat client-side (can be wired to a debug button later)
  const forceResetChat = useCallback(() => {
    try {
      const current = localStorage.getItem(LOCAL_STORAGE_KEY);
      if (current) {
        [
          `mozaiks.last_artifact.${current}`,
          `mozaiks.current_artifact.${current}`,
          `${LOCAL_STORAGE_KEY}.cache_seed.${current}`,
        ].forEach(k => { try { localStorage.removeItem(k); } catch {} });
      }
      console.log('🧼 [CACHE] Manual force reset invoked; clearing in-memory state');
    } catch {}
    setCurrentArtifactMessages([]);
    lastArtifactEventRef.current = null;
    artifactRestoredOnceRef.current = false;
    artifactCacheValidRef.current = false;
    setCurrentChatId(null);
  }, []);

  // Dev: expose reset helper & read cacheSeed to avoid unused warnings
  useEffect(() => {
    // Use cacheSeed in a benign way (log only when it changes)
    if (cacheSeed !== null) {
      // Minimal, low-noise log – toggle off by removing line if undesired
      console.debug('🧬 Active cacheSeed now', cacheSeed);
    }
    // Expose forceResetChat for manual debugging in console
    try { window.__mozaiksForceResetChat = forceResetChat; } catch {}
    
    // Expose artifact inspection helper
    try {
      window.__mozaiksInspectArtifacts = () => {
        const keys = [];
        const chatId = currentChatId || localStorage.getItem(LOCAL_STORAGE_KEY);
        console.log('🔍 [DEBUG] Inspecting artifact localStorage for chat:', chatId);
        
        for (let i = 0; i < localStorage.length; i++) {
          const key = localStorage.key(i);
          if (!key) continue;
          
          if (key.includes('artifact') || key.includes('cache_seed') || key.includes('current_chat_id')) {
            const value = localStorage.getItem(key);
            keys.push({ key, value: value?.slice(0, 200) + (value?.length > 200 ? '...' : '') });
          }
        }
        
        console.table(keys);
        return keys;
      };
    } catch {}
  }, [cacheSeed, forceResetChat, currentChatId]);

  // Connect to streaming when API becomes available and chat ID exists
  useEffect(() => {
    if (!api) return;
    
    // Wait for workflow configuration to be loaded before connecting
    if (!workflowConfigLoaded) {
  // console.debug('Waiting for workflow configuration to load...');
      return;
    }
    
    // Require chat ID to connect
    if (!currentChatId) {
  // console.debug('Waiting for chat ID to be available...');
      return;
    }
    
    // Prevent duplicate connections
    if (connectionInitialized || connectionInProgressRef.current) {
  // console.debug('Connection already initialized or in progress, skipping...');
      return;
    }
    
  // console.debug('Establishing WebSocket connection');
    
    // Mark connection as in progress immediately to prevent duplicates
    connectionInProgressRef.current = true;
    setConnectionInitialized(true);
    
    // Define connection functions inside useEffect to avoid dependency issues
    const connectWebSocket = () => {
      // WebSocket connection for chat communication
      if (!currentChatId) {
        console.error('WebSocket requires existing chat ID');
        return () => {};
      }
      
      setConnectionStatus('connecting');
      setTransportType('websocket');

      const workflowName = urlWorkflowName || getDefaultWorkflowFromRegistry();
      if (!workflowName) {
        console.warn('⚠️ No workflow available to connect');
        return () => {};
      }
      
  const connection = api.createWebSocketConnection(
        currentAppId,
        currentUserId,
        {
          onOpen: () => {
            // console.debug('WebSocket connection established');
            setConnectionStatus('connected');
            setLoading(false);
            // Show one-time spinner ONLY if it has never been shown and never been hidden
            if (!initSpinnerHiddenOnceRef.current && !initSpinnerShownRef.current) {
              console.log('🧹 [SPINNER] Showing initial spinner on WebSocket connect');
              setShowInitSpinner(true);
              initSpinnerShownRef.current = true;
            } else {
              console.log('🧹 [SPINNER] Skipping show on reconnect (hiddenOnce=', initSpinnerHiddenOnceRef.current, 'shownRef=', initSpinnerShownRef.current, ')');
              // Ensure we never regress into showing again this session
              if (initSpinnerHiddenOnceRef.current) {
                initSpinnerShownRef.current = true; // lock state
              }
            }
    try { localStorage.setItem(LOCAL_STORAGE_KEY, currentChatId); } catch {}
          },
          onMessage: handleIncoming,
          onError: (error) => {
            console.error("WebSocket error:", error);
            setConnectionStatus('error');
            setLoading(false);
          },
          onClose: () => {
            // console.debug('WebSocket connection closed');
            setConnectionStatus('disconnected');
            wsRef.current = null;
            setWs(null);
          }
        },
        workflowName,
        currentChatId // Pass the existing chat ID
      );

      setWs(connection);
      wsRef.current = connection;
  // console.debug('WebSocket connection established:', !!ws);
      return () => {
        if (connection) {
          connection.close();
          // console.debug('WebSocket connection closed');
        }
        wsRef.current = null;
      };
    };

    // Query the workflow transport type and use WebSocket connection
    const connectWithCorrectTransport = async () => {
      try {
        // Use URL workflow name first, then fall back to dynamic discovery or default
        const workflowName = urlWorkflowName || getDefaultWorkflowFromRegistry();
        if (!workflowName) {
          throw new Error('No workflow available');
        }
  // console.debug('Using workflow name:', workflowName);
        
        // Query transport info from backend and use it (was previously unused)
        const transportInfo = await api.getWorkflowTransport(workflowName);
        // transportInfo example: { transport: 'websocket' | 'sse' | 'poll', allow_resume: true }
        if (transportInfo && transportInfo.transport) {
          setTransportType(transportInfo.transport);
        } else {
          setTransportType('websocket');
        }
        // Expose transport flags or capabilities if provided
        if (transportInfo && transportInfo.allow_resume === false) {
          console.debug('Transport indicates resume is disabled for', workflowName);
        }
        setCurrentWorkflowName(workflowName);
        return connectWebSocket();
      } catch (error) {
        console.error('Error querying workflow transport:', error);
        // Fallback to WebSocket
        const fallbackWf = getDefaultWorkflowFromRegistry();
        if (!fallbackWf) {
          console.warn('⚠️ No default workflow available for fallback');
          return () => {};
        }
        setTransportType('websocket');
        setCurrentWorkflowName(fallbackWf);
        return connectWebSocket();
      }
    };
    
    // Execute the async function and handle cleanup
    let cleanup;
    connectWithCorrectTransport().then(cleanupFn => {
      cleanup = cleanupFn;
    }).catch(error => {
      console.error('Failed to connect with transport:', error);
      // Reset connection flags on error so user can retry
      setConnectionInitialized(false);
      connectionInProgressRef.current = false;
    });
    
    return () => {
      if (cleanup) cleanup();
      // Reset the in-progress flag when component unmounts
      connectionInProgressRef.current = false;
    };
  }, [api, currentAppId, currentUserId, handleIncoming, workflowConfigLoaded, currentChatId, connectionInitialized, urlWorkflowName, ws]);

  // Retry connection function
  const retryConnection = useCallback(() => {
  // console.debug('Retrying connection...');
    setConnectionInitialized(false);
    connectionInProgressRef.current = false;
    setConnectionStatus('disconnected');
    
    // Trigger reconnection by setting up the connection again
    setTimeout(() => {
      if (currentChatId && workflowConfigLoaded) {
        setConnectionStatus('connecting');
      }
    }, 1000);
  }, [currentChatId, workflowConfigLoaded]);

  // Subscribe to DynamicUIHandler updates and insert UI tool events into chat messages
  useEffect(() => {
    // Bridge dynamic UI events into the chat message stream
    const unsubscribe = dynamicUIHandler.onUIUpdate((update) => {
      try {
        if (!update || !update.type) return;
        // Only handle ui_tool_event here; other updates (status/component updates) are ignored for now
  if (update.type === 'ui_tool_event') {
    const { ui_tool_id, payload = {}, eventId, workflow_name, onResponse, display } = update;
          console.log('🧩 [UI] ChatPage received ui_tool_event -> inserting into messages', { ui_tool_id, eventId, workflow_name });
          // If this UI tool requests artifact display, auto-open the ArtifactPanel like OpenAI/Claude canvases
    const displayMode = (display || payload.display || payload.mode);
    if (displayMode === 'artifact') {
      console.log('🖼️ [UI] Auto-opening ArtifactPanel for artifact-mode event');
      setIsSidePanelOpen(true);
      const agentText = payload.agent_message || payload.description || null;
      if (agentText) {
        setMessagesWithLogging((prev) => {
          const withoutThinking = prev.filter(msg => !msg.isThinking);
          const hasExisting = withoutThinking.some(msg => msg?.metadata?.eventId === (eventId || ui_tool_id) && msg?.metadata?.type === 'ui_tool_agent_message');
          if (hasExisting) return withoutThinking;
          return [
            ...withoutThinking,
            {
              id: `ui-msg-${eventId || Date.now()}`,
              sender: 'agent',
              agentName: payload.agentName || payload.agent_name || update.agent_name || update.agent || 'Agent',
              content: agentText,
              isStreaming: false,
              metadata: { type: 'ui_tool_agent_message', eventId: eventId || ui_tool_id, ui_tool_id }
            }
          ];
        });
      }
      // Create artifact payload for ArtifactPanel to render
      try {
        const artifactMsg = {
          id: `ui-artifact-${eventId || Date.now()}`,
          sender: 'agent',
          agentName: payload.agentName || payload.agent_name || update.agent_name || update.agent || 'Agent',
          content: payload.structured_output || payload.content || payload || {},
          isStreaming: false,
          uiToolEvent: { ui_tool_id, payload, eventId, workflow_name, onResponse, display: displayMode }
        };
        console.log('🖼️ [UI] Setting currentArtifactMessages', artifactMsg.id);
        setCurrentArtifactMessages([artifactMsg]);
        artifactCacheValidRef.current = true;
        
        // Also cache to localStorage for persistence across panel open/close
        try {
          if (currentChatId) {
            const cacheKey = `mozaiks.current_artifact.${currentChatId}`;
            // Create a serializable version without the function
            const serializableArtifact = {
              ...artifactMsg,
              uiToolEvent: {
                ...artifactMsg.uiToolEvent,
                onResponse: null // Functions can't be serialized, will be reconstructed on restore
              }
            };
            localStorage.setItem(cacheKey, JSON.stringify(serializableArtifact));
            console.log('🖼️ [UI] Cached artifact to localStorage');
          }
        } catch (e) { console.warn('Failed to cache artifact', e); }
      } catch (e) { console.warn('Failed to set artifact message', e); }
            // Remember this artifact to collapse on next sequence
            lastArtifactEventRef.current = eventId || ui_tool_id || 'artifact';
            // Persist minimal artifact session state for graceful refresh restore
            try {
              if (currentChatId) {
                const key = `mozaiks.last_artifact.${currentChatId}`;
                const cache = {
                  ui_tool_id,
                  eventId: eventId || null,
                  workflow_name,
                  payload,
      display: displayMode || 'artifact',
                  ts: Date.now(),
                };
                localStorage.setItem(key, JSON.stringify(cache));
              }
            } catch {}
  // Don't inject artifact UIs into the chat feed; they'll render in ArtifactPanel only
  return;
          }
          setMessagesWithLogging((prev) => {
            const thinkingMessages = prev.filter(m => m.isThinking);
            if (thinkingMessages.length > 0) {
              console.log('💭 [THINKING] UI tool event (inline) received - removing', thinkingMessages.length, 'thinking bubble(s)');
            }
            
            const withoutThinking = prev.filter(m => !m.isThinking); // Remove thinking bubbles when UI tool event arrives
            return [
              ...withoutThinking,
              {
                id: `ui-${eventId || Date.now()}`,
                sender: 'agent',
                agentName: payload.agentName || payload.agent_name || update.agent_name || update.agent || 'Agent',
                content: (payload.agent_message || payload.description || ''), // Surface agent context alongside inline UI
                isStreaming: false,
                uiToolEvent: {
                  ui_tool_id,
                  payload,
                  eventId,
                  workflow_name,
                  onResponse,
                  // Surface display mode for inline Completed chip logic
      display: displayMode || 'inline',
                },
              },
            ];
          });
        }
      } catch (err) {
        console.error('❌ Failed to handle DynamicUIHandler update in ChatPage:', err);
      }
    });
    return () => {
      if (typeof unsubscribe === 'function') unsubscribe();
    };
  }, [setMessagesWithLogging, currentChatId]);

  const sendMessage = async (messageContent) => {
    console.log('🚀 [SEND] Sending message:', messageContent);
    console.log('🚀 [SEND] Current chat ID:', currentChatId);
    console.log('🚀 [SEND] Pending input request ID:', pendingInputRequestId);
    console.log('🚀 [SEND] Transport type:', transportType);
    console.log('🚀 [SEND] App ID:', currentAppId);
    console.log('🚀 [SEND] User ID:', currentUserId);
    console.log('🚀 [SEND] Workflow name:', currentWorkflowName);
    
    // If there's a pending input request, route to submitInputRequest instead of regular message flow
    if (pendingInputRequestId) {
      console.log('🎯 [SEND] Routing to submitInputRequest for pending request:', pendingInputRequestId);
      const success = submitInputRequest(pendingInputRequestId, messageContent.content);
      if (success) {
        setPendingInputRequestId(null); // Clear pending request after submission
        console.log('✅ [SEND] Input request submitted successfully');
      } else {
        console.error('❌ [SEND] Failed to submit input request');
      }
      return;
    }
    
    // Create a properly structured user message
    const userMessage = {
      id: Date.now().toString(),
      sender: 'user',  // Use 'user' to align message to the right
      agentName: 'You',
      content: messageContent.content,
      timestamp: Date.now(),
      isStreaming: false
    };
    
    console.log('💭 [THINKING] User message sent, adding thinking bubble');
    
    // Optimistic add: add user message to chat immediately, then add thinking indicator
    setMessagesWithLogging(prevMessages => {
      const existingThinking = prevMessages.filter(m => m.isThinking);
      if (existingThinking.length > 0) {
        console.log('💭 [THINKING] Removing', existingThinking.length, 'existing thinking bubble(s)');
      }
      
      const thinkingBubble = {
        id: `thinking-${Date.now()}`,
        sender: 'agent',
        agentName: 'Agent',
        content: '',
        isThinking: true,
        timestamp: Date.now()
      };
      
      console.log('💭 [THINKING] Adding thinking bubble:', thinkingBubble.id);
      
      return [
        ...prevMessages.filter(m => !m.isThinking), // Remove any existing thinking bubbles
        userMessage,
        thinkingBubble
      ];
    });
    
    if (conversationMode === 'ask') {
      const targetChatId = activeGeneralChatId || currentChatId;
      if (!targetChatId) {
        console.error('❌ [SEND] No chat available for ask-mode message');
        return;
      }

      const didSend = sendWsMessage({
        type: 'user.input.submit',
        chat_id: targetChatId,
        text: messageContent.content,
        context: {
          source: 'chat_interface',
          conversation_mode: 'ask',
          general_chat_id: activeGeneralChatId || undefined,
        },
      });
      if (!didSend) {
        console.error('❌ [SEND] Failed to send ask-mode message (socket unavailable)');
      } else {
        setLoading(true);
      }
      return;
    }

    // Send directly to backend workflow via WebSocket
    try {
      if (!currentChatId) {
        console.error('❌ [SEND] No chat ID available for sending message');
        return;
      }
      
      console.log('📤 [SEND] Sending via WebSocket to workflow...');
      const success = await api.sendMessageToWorkflow(
        messageContent.content, 
        currentAppId, 
        currentUserId, 
        currentWorkflowName,
        currentChatId // Pass the chat ID
      );
      console.log('📤 [SEND] WebSocket send result:', success);
      if (success) {
        setLoading(true);
      }
    } catch (error) {
      console.error('❌ [SEND] Failed to send message via WebSocket:', error);
    }
  };

  // Submit a pending input request via WebSocket control message
  const submitInputRequest = useCallback((input_request_id, text) => {
    const activeWs = wsRef.current;
    const targetChatId = currentChatId || activeGeneralChatId;
    if (!activeWs || !activeWs.socket || activeWs.socket.readyState !== WebSocket.OPEN) {
      console.warn('⚠️ Cannot submit input request; socket not open');
      return false;
    }
    if (!targetChatId) {
      console.warn('⚠️ Cannot submit input request; no active chat id');
      return false;
    }
    return activeWs.send({
      type: 'user.input.submit',
      chat_id: targetChatId,
      input_request_id,
      text,
      context: {
        source: 'chat_interface',
        conversation_mode: conversationMode,
        general_chat_id: activeGeneralChatId || undefined,
      },
    });
  }, [currentChatId, activeGeneralChatId, conversationMode]);

  const sendWsMessage = useCallback((payload) => {
    const activeWs = wsRef.current;
    if (!activeWs || typeof activeWs.send !== 'function') {
      console.warn('⚠️ No websocket connection available for payload', payload?.type || payload);
      return false;
    }
    try {
      activeWs.send(payload);
      return true;
    } catch (err) {
      console.error('Failed to send websocket payload', payload, err);
      return false;
    }
  }, []);

  const ensureGeneralMode = useCallback(() => {
    if (conversationMode === 'ask') {
      return true;
    }
    console.log('🧠 [MODE_TOGGLE] Switching to ask mode (sending chat.enter_general_mode)');
    const sent = sendWsMessage({ type: 'chat.enter_general_mode' });
    if (sent) {
      setConversationMode('ask');
      // Cache workflow messages and snapshot artifact panel state
      console.log('🧹 [MODE_TOGGLE] Caching workflow messages + artifact state, closing artifact panel, restoring ask-mode messages');
      workflowMessagesCacheRef.current = messagesRef.current;
      workflowArtifactSnapshotRef.current = {
        isOpen: isSidePanelOpen,
        layoutMode: layoutMode || 'split',
        messages: isSidePanelOpen ? [...currentArtifactMessages] : []
      };
      console.log('📸 [ARTIFACT_SNAPSHOT] Saved artifact state before switching to Ask:', workflowArtifactSnapshotRef.current);
      // Close artifact panel when entering Ask mode
      setIsSidePanelOpen(false);
      setCurrentArtifactMessages([]);
      setPendingInputRequestId(null); // Clear any pending workflow input requests
      
      // Restore cached general messages if available
      if (generalMessagesCacheRef.current && generalMessagesCacheRef.current.length > 0) {
        console.log(`📦 [MODE_TOGGLE] Restoring ${generalMessagesCacheRef.current.length} cached ask-mode messages`);
        setMessagesWithLogging(generalMessagesCacheRef.current);
      } else {
        console.log('📭 [MODE_TOGGLE] No cached ask-mode messages, starting fresh');
        setMessagesWithLogging([]);
      }
      
      refreshGeneralSessions();
    }
    return sent;
  }, [conversationMode, refreshGeneralSessions, sendWsMessage, setConversationMode, setMessagesWithLogging, currentArtifactMessages, isSidePanelOpen, layoutMode]);

  const startNewGeneralSession = useCallback(() => {
    const sent = sendWsMessage({ type: 'chat.start_general_chat' });
    if (sent) {
      setConversationMode('ask');
      refreshGeneralSessions();
      generalMessagesCacheRef.current = [];
      setMessagesWithLogging([]);
    }
    return sent;
  }, [refreshGeneralSessions, sendWsMessage, setConversationMode, setMessagesWithLogging]);

  const handleSelectGeneralChat = useCallback((chatId) => {
    if (!chatId || generalHydrationPendingRef.current) {
      return;
    }
    ensureGeneralMode();
    generalHydrationPendingRef.current = true;
    setActiveGeneralChatId(chatId);
    const session = (generalChatSessions || []).find((item) => item?.chat_id === chatId);
    if (session) {
      setGeneralChatSummary({
        chatId,
        label: session.label || session.chat_id,
        lastUpdatedAt: session.last_updated_at || session.updated_at,
        lastSequence: session.last_sequence ?? session.sequence,
      });
    }
    setMessagesWithLogging([]);
    Promise.resolve(hydrateGeneralTranscript(chatId)).finally(() => {
      generalHydrationPendingRef.current = false;
    });
  }, [ensureGeneralMode, generalChatSessions, hydrateGeneralTranscript, setActiveGeneralChatId, setGeneralChatSummary, setMessagesWithLogging]);

  const handleRefreshGeneralSessions = useCallback(() => {
    refreshGeneralSessions();
  }, [refreshGeneralSessions]);

  const ensureWorkflowMode = useCallback(() => {
    if (conversationMode === 'workflow') {
      return true;
    }
    if (!currentChatId) {
      console.warn('⚠️ Cannot resume workflow mode without chat id');
      return false;
    }
    console.log('🤖 [MODE_TOGGLE] Switching to workflow mode (sending chat.switch_workflow)');
    const sent = sendWsMessage({ type: 'chat.switch_workflow', chat_id: currentChatId });
    if (sent) {
      setConversationMode('workflow');
      // Cache general messages and restore workflow messages + artifact panel state
      console.log('🧹 [MODE_TOGGLE] Caching ask-mode messages, restoring workflow messages + artifact panel state');
      generalMessagesCacheRef.current = messagesRef.current;
      
      // Restore cached workflow messages
      if (workflowMessagesCacheRef.current && workflowMessagesCacheRef.current.length > 0) {
        console.log(`📦 [MODE_TOGGLE] Restoring ${workflowMessagesCacheRef.current.length} cached workflow messages`);
        setMessagesWithLogging(workflowMessagesCacheRef.current);
        
        setTimeout(() => {
          const snapshot = workflowArtifactSnapshotRef.current;
          let restoredFromSnapshot = false;
          if (snapshot?.isOpen) {
            console.log('🎨 [ARTIFACT_SNAPSHOT_RESTORE] Restoring artifact panel from snapshot');
            setIsSidePanelOpen(true);
            if (snapshot.layoutMode && setLayoutMode) {
              setLayoutMode(snapshot.layoutMode);
            }
            if (snapshot.messages?.length) {
              setCurrentArtifactMessages(snapshot.messages);
              console.log(`📦 [ARTIFACT_SNAPSHOT_RESTORE] Restored ${snapshot.messages.length} artifact messages from snapshot`);
            }
            restoredFromSnapshot = true;
          }

          if (!restoredFromSnapshot) {
            // Auto-detect if we should open artifact panel based on message content
            const hasArtifacts = workflowMessagesCacheRef.current.some(msg => {
              if (msg.ui_mode) return true;
              if (msg.tool_calls && Array.isArray(msg.tool_calls)) {
                return msg.tool_calls.some(tc => tc.function?.name === 'render_ui_component');
              }
              return false;
            });
            
            if (hasArtifacts) {
              console.log('🎨 [ARTIFACT_AUTO_OPEN] Detected UI artifacts in workflow messages, opening artifact panel');
              setIsSidePanelOpen(true);
              const artifactMsgs = workflowMessagesCacheRef.current.filter(msg => 
                msg.ui_mode || (msg.tool_calls && msg.tool_calls.some(tc => tc.function?.name === 'render_ui_component'))
              );
              if (artifactMsgs.length > 0) {
                setCurrentArtifactMessages(artifactMsgs);
                console.log(`📦 [ARTIFACT_AUTO_OPEN] Restored ${artifactMsgs.length} artifact messages`);
              }
            } else {
              console.log('📭 [ARTIFACT_AUTO_OPEN] No UI artifacts detected, keeping panel closed');
              setIsSidePanelOpen(false);
              setCurrentArtifactMessages([]);
            }
          }

          // Reset snapshot once we've attempted restore
          workflowArtifactSnapshotRef.current = { isOpen: false, messages: [], layoutMode: snapshot?.layoutMode || 'split' };
        }, 150);
      } else {
        console.log('📭 [MODE_TOGGLE] No cached workflow messages, starting fresh');
        setMessagesWithLogging([]);
      }
    }
    return sent;
  }, [conversationMode, currentChatId, sendWsMessage, setConversationMode, setMessagesWithLogging, setLayoutMode]);

  const resumeWorkflowSession = useCallback((targetChatId, targetWorkflow = null) => {
    if (!targetChatId) {
      console.warn('🔁 [WORKFLOW_RESUME] Missing chat_id, cannot resume');
      return false;
    }

    const resolvedWorkflow = targetWorkflow || currentWorkflowName || defaultWorkflow;
    console.log('🔁 [WORKFLOW_RESUME] Attempting resume for chat:', targetChatId, 'workflow:', resolvedWorkflow);

    setCurrentChatId(targetChatId);
    setActiveChatId(targetChatId);
    setActiveWorkflowName(resolvedWorkflow);
    setCurrentWorkflowName(resolvedWorkflow);

    const sent = sendWsMessage({ type: 'chat.switch_workflow', chat_id: targetChatId });
    console.log('🔁 [WORKFLOW_RESUME] chat.switch_workflow sent:', sent);

    if (sent) {
      setConversationMode('workflow');
      generalMessagesCacheRef.current = messagesRef.current;
      console.log('🔁 [WORKFLOW_RESUME] Workflow mode restored, cached general messages count:', messagesRef.current.length);
      return true;
    }

    return false;
  }, [currentWorkflowName, defaultWorkflow, sendWsMessage, setActiveChatId, setActiveWorkflowName, setConversationMode, setCurrentWorkflowName, setCurrentChatId]);

  const handleConversationModeChange = useCallback(async (mode) => {
    console.log('🔄 [MODE_CHANGE] handleConversationModeChange called with mode:', mode);
    console.log('🔄 [MODE_CHANGE] Current conversationMode:', conversationMode);
    console.log('🔄 [MODE_CHANGE] Current activeChatId (from context):', activeChatId);
    console.log('🔄 [MODE_CHANGE] Current activeWorkflowName (from context):', activeWorkflowName);
    console.log('🔄 [MODE_CHANGE] Current currentChatId (local state):', currentChatId);
    
    if (mode === 'ask') {
      // ensureGeneralMode will cache artifact state and close panel
      console.log('🧠 [MODE_CHANGE] Switching to Ask mode');
      ensureGeneralMode();
      // Ensure layout is reset to full (no artifact panel)
      if (setLayoutMode) setLayoutMode('full');
      // Reset mobile drawer to peek state (no artifacts in Ask mode)
      if (isMobileView) setMobileDrawerState('peek');
    } else {
      // Switching to workflow mode from Ask - ALWAYS fetch oldest IN_PROGRESS workflow
      console.log('🤖 [MODE_CHANGE] Switching to workflow mode, fetching oldest IN_PROGRESS workflow');
      console.log('🤖 [MODE_CHANGE] isInWidgetMode:', isInWidgetMode);
      console.log('🤖 [MODE_CHANGE] API available?', !!api);
      console.log('🤖 [MODE_CHANGE] App ID:', currentAppId);
      console.log('🤖 [MODE_CHANGE] User ID:', currentUserId);

      // If in widget mode, navigate to /chat first
      if (isInWidgetMode) {
        console.log('🚀 [MODE_CHANGE] In widget mode - navigating to /chat');
        navigate('/chat');
        // Exit widget mode
        setIsInWidgetMode(false);
      }
      
      try {
        const endpoint = `/api/sessions/oldest/${currentAppId}/${currentUserId}`;
        console.log('🌐 [MODE_CHANGE] Calling endpoint:', endpoint);
        const response = await api.get(endpoint);
        console.log('✅ [MODE_CHANGE] API response:', JSON.stringify(response, null, 2));
        
        if (!response || !response.found) {
          console.warn('⚠️ [MODE_CHANGE] No IN_PROGRESS workflows available to resume');
          console.warn('⚠️ [MODE_CHANGE] Response found flag:', response?.found);
          return;
        }
        
        const targetChatId = response.chat_id;
        const targetWorkflowName = response.workflow_name;
        
        console.log(`🎯 [MODE_CHANGE] Resuming oldest workflow: ${targetWorkflowName} (${targetChatId})`);
        console.log('🎯 [MODE_CHANGE] Setting chat context...');
        
        // Update current chat context before sending switch command
        setCurrentChatId(targetChatId);
        console.log('✅ [MODE_CHANGE] setCurrentChatId called with:', targetChatId);
        setActiveChatId(targetChatId);
        console.log('✅ [MODE_CHANGE] setActiveChatId called with:', targetChatId);
        setActiveWorkflowName(targetWorkflowName);
        console.log('✅ [MODE_CHANGE] setActiveWorkflowName called with:', targetWorkflowName);
        
        console.log('📤 [MODE_CHANGE] Sending chat.switch_workflow message...');
        const sent = sendWsMessage({ type: 'chat.switch_workflow', chat_id: targetChatId });
        console.log('📤 [MODE_CHANGE] WebSocket send result:', sent);
        
        if (sent) {
          console.log('✅ [MODE_CHANGE] Setting conversation mode to workflow');
          setConversationMode('workflow');
          // Cache general messages - workflow messages will be restored via auto-resume
          generalMessagesCacheRef.current = messagesRef.current;
          console.log('✅ [MODE_CHANGE] Cached general messages, count:', messagesRef.current.length);
        } else {
          console.error('❌ [MODE_CHANGE] Failed to send WebSocket message');
        }
      } catch (err) {
        console.error('❌ [MODE_CHANGE] Error fetching oldest workflow:', err);
        console.error('❌ [MODE_CHANGE] Error stack:', err.stack);
        console.log('🔄 [MODE_CHANGE] Falling back to ensureWorkflowMode');
        // Fallback to existing ensureWorkflowMode if fetch fails
        ensureWorkflowMode();
      }
    }
    console.log('✅ [MODE_CHANGE] handleConversationModeChange completed');
  }, [ensureGeneralMode, ensureWorkflowMode, setLayoutMode, api, currentAppId, currentUserId, sendWsMessage, setConversationMode, setCurrentChatId, setActiveChatId, setActiveWorkflowName, conversationMode, activeChatId, activeWorkflowName, isInWidgetMode, navigate, setIsInWidgetMode, currentChatId, isMobileView, setMobileDrawerState]);

  useEffect(() => {
    if (!queryChatId) {
      return;
    }
    if (!isPrimaryChatRoute) {
      return;
    }
    if (connectionStatus !== 'connected') {
      return;
    }

    const workflowFromQuery = urlWorkflowName || currentWorkflowName;
    const cacheKey = `${queryChatId}:${workflowFromQuery || ''}`;
    if (conversationMode === 'workflow' && queryResumeHandledRef.current === cacheKey) {
      return;
    }

    console.log('🧭 [ROUTE_RESUME] Detected chat_id in URL, attempting resume:', { queryChatId, workflowFromQuery });
    if (isInWidgetMode) {
      console.log('🧭 [ROUTE_RESUME] Exiting widget mode for direct resume');
      setIsInWidgetMode(false);
    }

    const success = resumeWorkflowSession(queryChatId, workflowFromQuery);
    if (success) {
      queryResumeHandledRef.current = cacheKey;
    }
  }, [queryChatId, urlWorkflowName, currentWorkflowName, resumeWorkflowSession, conversationMode, connectionStatus, isInWidgetMode, setIsInWidgetMode, isPrimaryChatRoute]);

  const handleStartGeneralChat = useCallback(() => {
    startNewGeneralSession();
  }, [startNewGeneralSession]);

  // Force Ask mode once when transitioning away from the primary chat routes
  useEffect(() => {
    const wasPrimary = lastPrimaryRouteRef.current;
    if (!isPrimaryChatRoute && wasPrimary) {
      ensureGeneralMode();
    }
    lastPrimaryRouteRef.current = isPrimaryChatRoute;
  }, [ensureGeneralMode, isPrimaryChatRoute]);

  // Handle agent UI actions
  const handleAgentAction = async (action) => {
    console.log('Agent action received in chat page:', action);
    
    try {
      // Handle UI tool responses for the dynamic UI system
      if (action.type === 'ui_tool_response') {
        console.log('🎯 Processing UI tool response:', action);

        // If this response corresponds to the most recent artifact event, close the panel immediately
        if (lastArtifactEventRef.current && (!action.eventId || action.eventId === lastArtifactEventRef.current)) {
          try { console.log('🧹 [UI] Artifact response received; collapsing ArtifactPanel now'); } catch {}
          setIsSidePanelOpen(false);
            lastArtifactEventRef.current = null;
          console.log('🖼️ [UI] Clearing currentArtifactMessages due to response');
          setCurrentArtifactMessages([]);
          // Clear persisted artifact cache for this chat
          try { if (currentChatId) localStorage.removeItem(`mozaiks.last_artifact.${currentChatId}`); } catch {}
        }
        // If we lack a real eventId (e.g., restored artifact), don't submit to backend; just close locally
        if (!action.eventId) {
          console.log('ℹ️ Skipping backend submission for restored or legacy UI tool response (no eventId)');
          return;
        }

        const payload = {
          event_id: action.eventId,
          response_data: action.response
        };

        // Send the UI tool response to the backend
        try {
          const response = await fetch('http://localhost:8000/api/ui-tool/submit', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
            },
            body: JSON.stringify(payload)
          });
          if (response.ok) {
            const result = await response.json();
            console.log('✅ UI tool response submitted successfully:', result);
          } else {
            console.error('❌ Failed to submit UI tool response:', response.statusText);
          }
        } catch (e) {
          console.error('❌ Network error submitting UI tool response:', e);
        }
        
        return;
      }
      
      // Handle other agent action types
    // console.debug('Agent action handled through workflow system');
      // Other response types will come through WebSocket from backend
    } catch (error) {
      console.error('❌ Error handling agent action:', error);
    }
  };

  const handleAppClick = () => {
  // console.debug('Navigate to App');
  };

  const handleNotificationClick = () => {
  // console.debug('Show notifications');
  };

  const handleDiscoverClick = () => {
    try {
      setPreviousLayoutMode(layoutMode);
      setIsInWidgetMode(true);
      navigate('/workflows');
    } catch (err) {
      console.warn('Failed to navigate to workflows', err);
    }
  };

  const handleReturnToChat = useCallback(() => {
    navigate('/chat');
  }, [navigate]);

  const toggleDiscoveryChatMinimized = useCallback(() => {
    setDiscoveryChatMinimized(prev => !prev);
  }, []);

  useEffect(() => {
    if (!isInWidgetMode && discoveryChatMinimized) {
      setDiscoveryChatMinimized(false);
    }
  }, [isInWidgetMode, discoveryChatMinimized]);

  const toggleSidePanel = () => {
    setIsSidePanelOpen((open) => {
      const next = !open;
      
      // Update fluid layout state
      if (next) {
        // Opening artifact - switch to split view
        setLayoutMode('split');
      } else {
        // Closing artifact - back to full chat
        setLayoutMode('full');
      }

      if (isMobileView) {
        setMobileDrawerState(next ? 'expanded' : 'peek');
      }
      
      if (next && currentArtifactMessages.length === 0 && artifactCacheValidRef.current) {
        // Panel opening and no current artifact - try to restore from cache
        try {
          const cacheKey = `mozaiks.current_artifact.${currentChatId}`;
          const cached = localStorage.getItem(cacheKey);
          if (cached) {
            const artifactMsg = JSON.parse(cached);
            
            // Reconstruct the onResponse function since it can't be serialized
            if (artifactMsg.uiToolEvent && !artifactMsg.uiToolEvent.onResponse) {
              artifactMsg.uiToolEvent.onResponse = (response) => {
                console.log('🔌 [UI] Cached artifact response (no longer functional):', response);
                console.warn('⚠️ This is a restored artifact - responses may not work until next interaction');
              };
            }
            
            console.log('🖼️ [UI] Restored artifact from cache on panel open');
            setCurrentArtifactMessages([artifactMsg]);
            lastArtifactEventRef.current = artifactMsg.uiToolEvent?.eventId || 'cached';
          } else {
            artifactCacheValidRef.current = false;
          }
        } catch (e) {
          artifactCacheValidRef.current = false;
          console.warn('Failed to restore artifact from cache', e);
        }
      } else if (next && currentArtifactMessages.length === 0) {
        artifactCacheValidRef.current = false;
      }
      
      console.log(`🖼️ [UI] Panel ${next ? 'opening' : 'closing'} - keeping artifact cached`);
      return next;
    });
  };

  // Simplified artifact restore effect: only restore when chatExists === true and connection is open
  // last_artifact semantics:
  //   - Cached locally on each artifact-mode ui_tool_event
  //   - Server persists ONLY the most recent artifact (overwrite strategy)
  //   - On refresh / second user: websocket chat_meta may include last_artifact; if not, we fetch /api/chats/meta
  //   - We avoid speculative restores for brand new chats (chat_exists === false)
  useEffect(() => {
    if (connectionStatus !== 'connected') return;
    if (!currentChatId) return;
    if (!chatExists) return; // only restore for persisted chats
    if (artifactRestoredOnceRef.current) return;

    try {
      const key = `mozaiks.last_artifact.${currentChatId}`;
      const raw = localStorage.getItem(key);
      if (!raw) return;
      const cached = JSON.parse(raw);
      if (!cached || !cached.ui_tool_id) return;

      console.log('[RESTORE] Restoring cached artifact for chat', currentChatId, cached.ui_tool_id);
      setIsSidePanelOpen(true);
      const restoredMsg = {
        id: `ui-restored-${Date.now()}`,
        sender: 'agent',
        agentName: cached.payload?.agentName || cached.payload?.agent_name || cached.agentName || cached.agent_name || 'Agent',
        content: cached.payload?.structured_output || cached.payload || {},
        isStreaming: false,
        uiToolEvent: {
          ui_tool_id: cached.ui_tool_id,
          payload: cached.payload || {},
          eventId: cached.eventId || null,
          workflow_name: cached.workflow_name || currentWorkflowName,
          onResponse: undefined,
          display: cached.display || 'artifact',
          restored: true,
        },
      };
      setCurrentArtifactMessages([restoredMsg]);
      artifactRestoredOnceRef.current = true;
    } catch (e) {
      console.warn('[RESTORE] Failed to restore artifact:', e);
    }
  }, [connectionStatus, currentChatId, chatExists, currentWorkflowName]);

  // Mobile detection and layout adaptation
  useEffect(() => {
    const compute = () => {
      try {
        const w = window.innerWidth;
        const h = window.innerHeight;
        const isMobile = w < 768; // md breakpoint
        const isShort = h < 500; // landscape phones/tablets
        
        console.log('📱 [MOBILE] Detection:', { width: w, height: h, isMobile, isShort });
        setIsMobileView(isMobile);
        setForceOverlay(isMobile || isShort);
        
        // On mobile, avoid split mode - use tabs instead
        if (isMobile && layoutMode === 'split') {
          setLayoutMode('full');
        }
      } catch {}
    };
    compute();
    window.addEventListener('resize', compute);
    window.addEventListener('orientationchange', compute);
    return () => {
      window.removeEventListener('resize', compute);
      window.removeEventListener('orientationchange', compute);
    };
  }, [layoutMode, setLayoutMode]);

  // Keep drawer state in sync as viewport or artifact availability changes
  useEffect(() => {
    if (!isMobileView) {
      if (mobileDrawerState !== 'peek') {
        setMobileDrawerState('peek');
      }
      return;
    }
  }, [isMobileView, mobileDrawerState]);

  useEffect(() => {
    if (!isSidePanelOpen) {
      setHasUnseenArtifact(false);
      return;
    }
    setHasUnseenArtifact(mobileDrawerState !== 'expanded');
  }, [mobileDrawerState, isSidePanelOpen]);

  useEffect(() => {
    if (mobileDrawerState !== 'expanded' && hasUnseenChat) {
      setHasUnseenChat(false);
    }
  }, [mobileDrawerState, hasUnseenChat]);

  // Lock body scroll when overlay is open
  useEffect(() => {
    if (isSidePanelOpen && forceOverlay) {
      const { overflow } = document.body.style;
      document.body.style.overflow = 'hidden';
      return () => { document.body.style.overflow = overflow; };
    }
  }, [isSidePanelOpen, forceOverlay]);

  // Only show artifact toggle in workflow mode (Ask mode has no artifacts)
  const artifactToggleHandler = conversationMode === 'ask'
    ? null
    : isInWidgetMode
      ? handleReturnToChat
      : (isMobileView
          ? () => {
              setIsSidePanelOpen(true);
              setMobileDrawerState((prev) => (prev === 'expanded' ? 'peek' : 'expanded'));
            }
          : toggleSidePanel);

  const artifactToggleLabel = conversationMode === 'ask'
    ? undefined
    : isInWidgetMode
      ? 'Return to chat'
      : (isMobileView ? 'Artifact drawer' : undefined);

  const shouldReserveArtifactSpace = conversationMode === 'workflow';
  const mobileChatPaddingBottomClass = shouldReserveArtifactSpace
    ? (mobileDrawerState === 'expanded' ? 'pb-[0.75rem]' : 'pb-[4.5em]')
    : 'pb-2';
  const mobileChatTopMarginClass = shouldReserveArtifactSpace ? 'mt-[1.25rem]' : 'mt-[1.25rem]';

  const isChatPageSurface = isPrimaryChatRoute && !isInWidgetMode;
  const showAskHistorySidebar = isChatPageSurface && !isMobileView && conversationMode === 'ask';
  const showMobileAskHistoryMenu = isChatPageSurface && isMobileView && conversationMode === 'ask';

  useEffect(() => {
    if (!showMobileAskHistoryMenu && isAskHistoryDrawerOpen) {
      setIsAskHistoryDrawerOpen(false);
    }
  }, [showMobileAskHistoryMenu, isAskHistoryDrawerOpen]);

  useEffect(() => {
    if (!isAskHistoryDrawerOpen) {
      return undefined;
    }
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = previous;
    };
  }, [isAskHistoryDrawerOpen]);

  const chatInterface = (
    <ChatInterface 
      messages={messages} 
      onSendMessage={sendMessage} 
      loading={loading}
      onAgentAction={handleAgentAction}
      onArtifactToggle={artifactToggleHandler}
      artifactToggleLabel={artifactToggleLabel}
      connectionStatus={connectionStatus}
      transportType={transportType}
      workflowName={currentWorkflowName}
      structuredOutputs={getWorkflow(currentWorkflowName)?.structuredOutputs || {}}
      startupMode={workflowConfig?.getWorkflowConfig(currentWorkflowName)?.startup_mode}
      initialMessageToUser={workflowConfig?.getWorkflowConfig(currentWorkflowName)?.initial_message_to_user}
      onRetry={retryConnection}
      submitInputRequest={submitInputRequest}
      onBrandClick={undefined}
      conversationMode={conversationMode}
      onConversationModeChange={handleConversationModeChange}
      onStartGeneralChat={handleStartGeneralChat}
      generalChatSummary={generalChatSummary}
      isOnChatPage={isChatPageSurface}
      generalSessionsLoading={generalSessionsLoading}
      showAskHistoryMenu={showMobileAskHistoryMenu}
      onAskHistoryToggle={() => setIsAskHistoryDrawerOpen((prev) => !prev)}
    />
  );

  console.log('📱 [RENDER] ChatPage render:', { isInWidgetMode, isMobileView, workflowCompleted, mobileDrawerState });

  // Widget mode has its own UI (persistent widget on non-ChatPage routes), so render that
  if (isInWidgetMode) {
    if (discoveryChatMinimized) {
      return (
        <div className="fixed right-4 z-50 widget-safe-bottom">
          <button
            type="button"
            onClick={toggleDiscoveryChatMinimized}
            className="group relative w-20 h-20 rounded-2xl bg-gradient-to-br from-[var(--color-primary)] to-[var(--color-secondary)] shadow-[0_8px_32px_rgba(15,23,42,0.6)] border-2 border-[rgba(var(--color-primary-light-rgb),0.5)] hover:shadow-[0_16px_48px_rgba(51,240,250,0.4)] hover:scale-105 transition-all duration-300 flex items-center justify-center"
            title="Expand chat"
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

    return (
      <div className="fixed right-4 z-50 flex flex-col items-end gap-0 pointer-events-none widget-safe-bottom">
        <button
          type="button"
          onClick={toggleDiscoveryChatMinimized}
          className="pointer-events-auto relative group mb-[-1px] z-20"
          title="Minimize chat"
        >
          <div className="w-32 h-8 rounded-t-2xl bg-gradient-to-r from-[rgba(var(--color-primary-rgb),0.4)] to-[rgba(var(--color-secondary-rgb),0.4)] border-t border-l border-r border-[rgba(var(--color-primary-light-rgb),0.4)] backdrop-blur-sm flex items-center justify-center group-hover:bg-gradient-to-r group-hover:from-[rgba(var(--color-primary-rgb),0.6)] group-hover:to-[rgba(var(--color-secondary-rgb),0.6)] transition-all">
            <svg className="w-5 h-5 text-[var(--color-primary-light)] group-hover:text-white transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth="2.5">
              <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
            </svg>
          </div>
        </button>
        <div className="pointer-events-auto w-[26rem] max-w-[calc(100vw-2.5rem)] h-[50vh] md:h-[70vh] min-h-[360px]">
          <div className="h-full">
            {chatInterface}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-screen overflow-hidden relative">
      {showInitSpinner && (
        // Make the overlay visually blocking but non-interactive so background UI can still receive events
        <div className="fixed inset-0 flex items-center justify-center bg-black/20 backdrop-blur-sm z-50 pointer-events-none">
          <div className="pointer-events-auto">
            <LoadingSpinner />
          </div>
        </div>
      )}
      <img
        src="/existing-flow-bg.png"
        alt=""
        className="z-[-10] fixed sm:-w-auto w-full h-full top-0 object-cover"
      />
      <Header 
        user={user}
        workflowName={currentWorkflowName}
        onAppClick={handleAppClick}
        onNotificationClick={handleNotificationClick}
        onDiscoverClick={handleDiscoverClick}
      />
      
      {/* Main content area that fills remaining screen height - no scrolling */}
      <div className="flex-1 flex flex-col min-h-0 overflow-hidden pt-16 md:pt-16">{/* Padding for header */}
        {workflowCompleted ? (
          /* Workflow Completion Screen */
          <div className="flex-1 flex items-center justify-center px-4">
            <WorkflowCompletion
              workflowName={currentWorkflowName}
              completionMessage={`Your ${currentWorkflowName} workflow has completed successfully!`}
              summary={{
                duration: completionData?.duration,
                tokensUsed: completionData?.tokensUsed,
              }}
              onContinue={() => {
                console.log('🎉 [COMPLETION] User continuing to Mozaiks');
                // Optional: Send analytics event here
              }}
            />
          </div>
        ) : isMobileView ? (
          <div className="relative flex-1 flex flex-col">
            <div className={`flex-1 flex flex-col transition-[padding-bottom] duration-300 ${mobileChatTopMarginClass} ${mobileChatPaddingBottomClass}`}>
              {chatInterface}
            </div>

            {isSidePanelOpen && mobileDrawerState === 'expanded' && conversationMode === 'workflow' && (
              <button
                type="button"
                aria-label="Collapse artifact workspace"
                className="absolute inset-x-0 top-0 z-30 h-16 bg-gradient-to-b from-black/40 to-transparent"
                onClick={() => setMobileDrawerState('peek')}
              ></button>
            )}

            {/* Only show mobile artifact drawer in workflow mode (Ask mode has no artifacts) */}
            {conversationMode === 'workflow' && (
              <MobileArtifactDrawer
                state={mobileDrawerState}
                onStateChange={setMobileDrawerState}
                onClose={() => {
                  setMobileDrawerState('peek');
                  setIsSidePanelOpen(false);
                }}
                artifactContent={
                  <ArtifactPanel
                    onClose={() => {
                      setMobileDrawerState('peek');
                      setIsSidePanelOpen(false);
                    }}
                    isMobile
                    isEmbedded
                    messages={currentArtifactMessages}
                    chatId={currentChatId}
                    workflowName={currentWorkflowName}
                  />
                }
                hasUnseenChat={hasUnseenChat}
                hasUnseenArtifact={hasUnseenArtifact}
              />
            )}

            {showMobileAskHistoryMenu && (
              <MobileAskHistoryDrawer
                open={isAskHistoryDrawerOpen}
                sessions={generalChatSessions}
                activeChatId={activeGeneralChatId}
                loading={generalSessionsLoading}
                onSelectChat={handleSelectGeneralChat}
                onStartNewChat={handleStartGeneralChat}
                onRefresh={handleRefreshGeneralSessions}
                onClose={() => setIsAskHistoryDrawerOpen(false)}
              />
            )}
          </div>
        ) : (
          <div className="flex flex-1 min-h-0 gap-4 px-3 md:px-6 pb-4">
            {showAskHistorySidebar && (
              <AskHistorySidebar
                sessions={generalChatSessions}
                activeChatId={activeGeneralChatId}
                loading={generalSessionsLoading}
                onSelectChat={handleSelectGeneralChat}
                onStartNewChat={handleStartGeneralChat}
                onRefresh={handleRefreshGeneralSessions}
              />
            )}
            <div className="flex-1 min-h-0">
              <FluidChatLayout
                layoutMode={layoutMode}
                onLayoutChange={setLayoutMode}
                isArtifactAvailable={true}
                hasActiveChat={!!currentChatId}
                onToggleArtifact={() => {
                  if (layoutMode === 'full') {
                    setLayoutMode('split');
                    setIsSidePanelOpen(true);
                  } else {
                    setLayoutMode('full');
                    setIsSidePanelOpen(false);
                  }
                }}
                onToggleChat={() => {
                  if (layoutMode === 'minimized') {
                    setLayoutMode('split');
                  }
                }}
                chatContent={chatInterface}
                artifactContent={
                  <ArtifactPanel 
                    onClose={toggleSidePanel} 
                    messages={currentArtifactMessages} 
                    chatId={currentChatId}
                    workflowName={currentWorkflowName}
                  />
                }
              />
            </div>
          </div>
        )}
      </div>

    </div>
  );
};

export default ChatPage;

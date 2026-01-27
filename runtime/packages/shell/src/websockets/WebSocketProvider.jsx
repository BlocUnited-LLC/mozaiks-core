// /src/websockets/WebSocketProvider.jsx
// SECURITY: WebSocket authentication uses JWT token via query param.
// The server MUST validate the token and derive user identity from JWT sub claim.
// Client-supplied user_id in the URL path is a routing hint only.
import React, { createContext, useEffect, useRef, useContext, useState, useCallback } from 'react';
import { useAuth } from '../auth/AuthContext';

const WebSocketContext = createContext(null);

/**
 * Build a secure WebSocket URL with JWT authentication.
 * 
 * SECURITY NOTES:
 * - Token is passed via query param (browser-safe, no custom headers in WS)
 * - user_id in path is a ROUTING HINT only; server derives identity from JWT sub
 * - MozaiksAI will reject connections where path user_id does not match JWT sub
 * 
 * BOUNDARY NOTE:
 * - MozaiksCore WebSockets are for notifications and plugin UI events ONLY
 * - Chat streaming and workflow execution are runtime-owned (MozaiksAI)
 * 
 * @param {string} path - The WebSocket endpoint path (e.g., 'notifications')
 * @param {string} token - The JWT access token (delegated user token)
 * @param {string} [userIdHint] - Routing hint (NOT used for identity)
 * @returns {string|null} - The WebSocket URL or null if token is missing
 */
const buildSecureWsUrl = (path, token, userIdHint) => {
  if (!token) {
    console.warn('WebSocket connection blocked: no access token available');
    return null;
  }

  // Determine WebSocket protocol based on page protocol
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  const host = window.location.hostname;
  const port = import.meta.env.VITE_WS_PORT || '8080';

  // Build base URL with user_id path segment (required by current backend routes)
  // NOTE: The server MUST validate JWT and use sub claim for identity, not this path param
  const baseUrl = `${protocol}//${host}:${port}/ws/${path}/${userIdHint || '_'}`;

  // Append token as query param (the only browser-safe method for WS auth)
  // SECURITY: Never log this URL as it contains the token
  const url = new URL(baseUrl);
  url.searchParams.set('access_token', token);

  return url.toString();
};

export const WebSocketProvider = ({ children, path = 'notifications' }) => {
  const { user, isAuthenticated, getAccessToken } = useAuth();
  const wsRef = useRef(null);
  const subscribersRef = useRef([]);
  const [status, setStatus] = useState("disconnected");
  const [connectionError, setConnectionError] = useState(null);

  useEffect(() => {
    // SECURITY: Require authentication, but identity comes from token, not user object
    if (!isAuthenticated) {
      setStatus("disconnected");
      return;
    }

    let socket = null;
    let mounted = true;

    const connect = async () => {
      try {
        // Get the delegated access token (same token used for HTTP calls)
        const token = await getAccessToken();
        
        if (!token) {
          console.warn('WebSocket: No access token available, connection not established');
          setConnectionError('No access token');
          setStatus("disconnected");
          return;
        }

        // Use user.user_id as routing hint only (server validates via JWT sub)
        const userIdHint = user?.user_id || '_';
        const wsUrl = buildSecureWsUrl(path, token, userIdHint);

        if (!wsUrl) {
          setStatus("disconnected");
          return;
        }

        socket = new WebSocket(wsUrl);
        wsRef.current = socket;

        socket.onopen = () => {
          if (!mounted) return;
          // SECURITY: Do not log URL (contains token)
          console.log(`✅ WebSocket connected to /ws/${path} (authenticated)`);
          setStatus("connected");
          setConnectionError(null);
        };

        socket.onmessage = (event) => {
          if (!mounted) return;
          try {
            const data = JSON.parse(event.data);
            subscribersRef.current.forEach(cb => cb(data));
          } catch (err) {
            console.error("WebSocket message error:", err);
          }
        };

        socket.onerror = (error) => {
          if (!mounted) return;
          console.error("WebSocket error:", error);
          setStatus("error");
          setConnectionError('Connection error');
        };

        socket.onclose = (event) => {
          if (!mounted) return;
          // Handle auth rejection specifically
          if (event.code === 1008 || event.code === 4001 || event.code === 4003) {
            console.warn("🔌 WebSocket closed: authentication failed");
            setConnectionError('Authentication failed');
          } else {
            console.warn("🔌 WebSocket disconnected");
          }
          setStatus("disconnected");
        };
      } catch (err) {
        console.error("WebSocket connection error:", err);
        setConnectionError(err.message);
        setStatus("error");
      }
    };

    connect();

    return () => {
      mounted = false;
      if (socket) {
        socket.close();
      }
    };
  }, [isAuthenticated, getAccessToken, path, user?.user_id]);

  const subscribe = (callback) => {
    subscribersRef.current.push(callback);
    return () => {
      subscribersRef.current = subscribersRef.current.filter(cb => cb !== callback);
    };
  };

  const sendMessage = (message) => {
    if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
      wsRef.current.send(JSON.stringify(message));
    } else {
      console.warn("WebSocket is not open. Message not sent.");
    }
  };

  return (
    <WebSocketContext.Provider value={{ status, connectionError, subscribe, sendMessage }}>
      {children}
    </WebSocketContext.Provider>
  );
};

export const useWebSocket = () => {
  const context = useContext(WebSocketContext);
  if (!context) throw new Error("useWebSocket must be used within a WebSocketProvider");
  return context;
};

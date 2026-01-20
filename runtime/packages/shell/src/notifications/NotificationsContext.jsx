// /src/notifications/NotificationsContext.jsx
import React, { createContext, useState, useEffect, useContext } from 'react';
import { useAuth } from '../auth/AuthContext';
import { WebSocketProvider, useWebSocket } from '../websockets/WebSocketProvider';

const NotificationsContext = createContext(null);

// 👇 Core logic separated into inner provider to allow websocket wrapping
const NotificationsCoreProvider = ({ children }) => {
  const { isAuthenticated, authFetch } = useAuth();
  const { subscribe } = useWebSocket();

  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [config, setConfig] = useState({});
  const [preferences, setPreferences] = useState({});
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  // Fetch notifications and preferences
  const fetchNotificationsData = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const configRes = await authFetch('/api/notifications/config');
      if (!configRes.ok) throw new Error('Failed to fetch notification configuration');
      const configData = await configRes.json();
      setConfig(configData.config);
      setPreferences(configData.preferences);

      const notiRes = await authFetch('/api/notifications');
      if (!notiRes.ok) throw new Error('Failed to fetch notifications');
      const notiData = await notiRes.json();
      setNotifications(notiData.notifications);

      const unread = notiData.notifications.filter(n => !n.read).length;
      setUnreadCount(unread);
    } catch (err) {
      console.error('Error fetching notifications:', err);
      setError(err.message);
    } finally {
      setIsLoading(false);
    }
  };

  // Initial fetch and polling
  useEffect(() => {
    if (!isAuthenticated) {
      setNotifications([]);
      setUnreadCount(0);
      setIsLoading(false);
      return;
    }
    fetchNotificationsData();
    const interval = setInterval(fetchNotificationsData, 60000);
    return () => clearInterval(interval);
  }, [isAuthenticated, authFetch]);

  // WebSocket: listen for real-time messages
  useEffect(() => {
    if (!isAuthenticated) return;

    const handleMessage = (message) => {
      if (message.type === 'notification' && message.subtype === 'new') {
        console.log('📨 Real-time notification received:', message.data);
        setNotifications(prev => [message.data, ...prev]);
        setUnreadCount(prev => prev + 1);
      }
    };

    const unsubscribe = subscribe(handleMessage);
    return unsubscribe;
  }, [isAuthenticated, subscribe]);

  const markAsRead = async (id) => {
    try {
      const res = await authFetch(`/api/notifications/${id}/read`, { method: 'POST' });
      if (!res.ok) throw new Error('Failed to mark notification as read');
      setNotifications(prev => prev.map(n => n.id === id ? { ...n, read: true } : n));
      setUnreadCount(prev => Math.max(0, prev - 1));
    } catch (err) {
      console.error('Error marking notification as read:', err);
    }
  };

  const markAllAsRead = async () => {
    try {
      const res = await authFetch('/api/notifications/mark-all-read', { method: 'POST' });
      if (!res.ok) throw new Error('Failed to mark all as read');
      setNotifications(prev => prev.map(n => ({ ...n, read: true })));
      setUnreadCount(0);
    } catch (err) {
      console.error('Error marking all as read:', err);
    }
  };

  const deleteNotification = async (id) => {
    try {
      const res = await authFetch(`/api/notifications/${id}`, { method: 'DELETE' });
      if (!res.ok) throw new Error('Failed to delete notification');
      const wasUnread = notifications.find(n => n.id === id)?.read === false;
      setNotifications(prev => prev.filter(n => n.id !== id));
      if (wasUnread) {
        setUnreadCount(prev => Math.max(0, prev - 1));
      }
    } catch (err) {
      console.error('Error deleting notification:', err);
    }
  };

  const updatePreferences = async (newPreferences) => {
    try {
      const res = await authFetch('/api/notifications/preferences', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(newPreferences)
      });
      if (!res.ok) throw new Error('Failed to update preferences');
      const data = await res.json();
      setPreferences(data.preferences);
      return true;
    } catch (err) {
      console.error('Error updating preferences:', err);
      return false;
    }
  };

  const value = {
    notifications,
    unreadCount,
    config,
    preferences,
    isLoading,
    error,
    markAsRead,
    markAllAsRead,
    deleteNotification,
    updatePreferences
  };

  return (
    <NotificationsContext.Provider value={value}>
      {children}
    </NotificationsContext.Provider>
  );
};

// 👇 Wrap the core logic with WebSocketProvider scoped to notifications
export const NotificationsProvider = ({ children }) => (
  <WebSocketProvider path="notifications">
    <NotificationsCoreProvider>
      {children}
    </NotificationsCoreProvider>
  </WebSocketProvider>
);

export const useNotifications = () => {
  const context = useContext(NotificationsContext);
  if (!context) {
    throw new Error('useNotifications must be used within a NotificationsProvider');
  }
  return context;
};

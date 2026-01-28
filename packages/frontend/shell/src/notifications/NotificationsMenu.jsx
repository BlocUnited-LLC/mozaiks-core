// /src/notifications/NotificationsMenu.jsx
import React, { useState, useEffect, useRef } from 'react';
import { useNotifications } from './NotificationsContext';

// Format relative time
const formatRelativeTime = (isoDateString) => {
  try {
    const date = new Date(isoDateString);
    const now = new Date();
    const diffMs = now - date;
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHour = Math.floor(diffMin / 60);
    const diffDay = Math.floor(diffHour / 24);

    if (diffSec < 60) return 'just now';
    if (diffMin < 60) return `${diffMin} min ago`;
    if (diffHour < 24) return `${diffHour} hr ago`;
    if (diffDay < 7) return `${diffDay} day ago`;
    return date.toLocaleDateString();
  } catch {
    return 'unknown date';
  }
};

const NotificationsMenu = () => {
  const {
    notifications,
    unreadCount,
    isLoading,
    markAsRead,
    markAllAsRead,
    deleteNotification
  } = useNotifications();

  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef(null);

  const toggleMenu = () => {
    setIsOpen(!isOpen);
    // Removed the automatic markAllAsRead when opening
  };

  // Close menu if clicking outside
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  const groupedNotifications = () => {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);

    const groups = { today: [], yesterday: [], older: [] };

    notifications.forEach(notification => {
      const date = new Date(notification.created_at);
      if (date >= today) groups.today.push(notification);
      else if (date >= yesterday) groups.yesterday.push(notification);
      else groups.older.push(notification);
    });

    return groups;
  };

  const renderNotification = (notification) => (
    <div
      key={notification.id}
      className={`p-3 border-b border-gray-200 hover:bg-secondary transition-colors ${
        !notification.read ? 'bg-blue-50 dark:bg-blue-900 dark:bg-opacity-20 border-l-4 border-l-accent' : 'opacity-75'
      }`}
    >
      <div className="flex justify-between items-start">
        <h4 className={`text-sm ${!notification.read ? 'font-bold text-text-primary' : 'font-medium text-text-primary'}`}>
          {notification.title}
          {!notification.read && (
            <span className="ml-2 inline-block px-1.5 py-0.5 text-xs bg-accent text-white rounded-full">
              New
            </span>
          )}
        </h4>
        <div className="flex items-center space-x-2">
          {!notification.read && (
            <button
              onClick={() => markAsRead(notification.id)}
              className="text-xs text-accent hover:text-opacity-80"
              aria-label="Mark as read"
            >
              Read
            </button>
          )}
          <button
            onClick={() => deleteNotification(notification.id)}
            className="text-gray-400 hover:text-red-500"
            aria-label="Delete notification"
          >
            <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path d="M18 6L6 18M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>
      <p className="text-text-secondary text-xs mt-1">{notification.message}</p>
      <div className="text-xs text-accent mt-2">{formatRelativeTime(notification.created_at)}</div>
    </div>
  );

  const groups = groupedNotifications();

  return (
    <div className="relative">
      <button
        onClick={toggleMenu}
        className="relative p-2 text-text-primary hover:bg-secondary rounded-full focus:outline-none focus:ring-2 focus:ring-accent"
        aria-label="Notifications"
      >
        <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
          <path d="M13.73 21a2 2 0 0 1-3.46 0" />
        </svg>

        {unreadCount > 0 && (
          <span className="absolute top-0 right-0 bg-red-500 text-white text-xs rounded-full h-5 w-5 flex items-center justify-center">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div
          ref={menuRef}
          className="absolute right-0 mt-2 w-80 bg-primary border border-gray-200 rounded-md shadow-lg z-20 max-h-96 overflow-y-auto"
        >
          <div className="p-3 border-b border-gray-200 flex justify-between items-center">
            <h3 className="font-bold text-text-primary">
              Notifications
              {unreadCount > 0 && (
                <span className="ml-2 text-xs bg-red-500 text-white px-2 py-0.5 rounded-full">
                  {unreadCount} new
                </span>
              )}
            </h3>
            {unreadCount > 0 && (
              <button onClick={markAllAsRead} className="text-xs text-accent hover:text-opacity-80">
                Mark all as read
              </button>
            )}
          </div>

          {isLoading ? (
            <div className="p-4 text-center">
              <div className="animate-spin h-5 w-5 border-2 border-accent border-t-transparent rounded-full mx-auto"></div>
              <p className="mt-2 text-text-secondary text-sm">Loading notifications...</p>
            </div>
          ) : notifications.length === 0 ? (
            <div className="p-4 text-center text-text-secondary">
              <svg xmlns="http://www.w3.org/2000/svg" className="h-8 w-8 mx-auto text-gray-400 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                <path d="M13.73 21a2 2 0 0 1-3.46 0" />
              </svg>
              <p>No notifications yet</p>
            </div>
          ) : (
            <div>
              {groups.today.length > 0 && (
                <>
                  <div className="px-3 py-1 bg-secondary bg-opacity-50 text-xs font-medium text-text-secondary">Today</div>
                  {groups.today.map(renderNotification)}
                </>
              )}

              {groups.yesterday.length > 0 && (
                <>
                  <div className="px-3 py-1 bg-secondary bg-opacity-50 text-xs font-medium text-text-secondary">Yesterday</div>
                  {groups.yesterday.map(renderNotification)}
                </>
              )}

              {groups.older.length > 0 && (
                <>
                  <div className="px-3 py-1 bg-secondary bg-opacity-50 text-xs font-medium text-text-secondary">Older</div>
                  {groups.older.map(renderNotification)}
                </>
              )}
            </div>
          )}

          <div className="p-2 border-t border-gray-200 text-center">
            <button onClick={() => setIsOpen(false)} className="text-xs text-accent hover:text-opacity-80">
              Close
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default NotificationsMenu;
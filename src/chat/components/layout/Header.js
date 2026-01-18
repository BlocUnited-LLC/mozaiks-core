import React, { useState, useRef, useEffect } from "react";
import "./header-styles.css";

const Header = ({ 
  user = null, 
  workflowName = null,
  onNotificationClick = () => {},
  onDiscoverClick = () => {}
}) => {
  // Default user if none provided (for standalone mode)
  const defaultUser = {
    id: "56132",
    firstName: "John Doe",
    userPhoto: null
  };

  const currentUser = user || defaultUser;
  const [isProfileDropdownOpen, setIsProfileDropdownOpen] = useState(false);
  const [isNotificationDropdownOpen, setIsNotificationDropdownOpen] = useState(false);
  const [notificationCount, setNotificationCount] = useState(3); // TODO: Mock notification count
  const [isScrolled, setIsScrolled] = useState(false);
  const dropdownRef = useRef(null);

  // Handle scroll effect for dynamic blur
  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 10);
    };
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  // Mock notification count updates (can be replaced with real notification system)
  useEffect(() => {
    const interval = setInterval(() => {
      // Simulate notification count changes based on activity
      setNotificationCount(prev => Math.max(0, prev + Math.floor(Math.random() * 3) - 1));
    }, 30000); // Update every 30 seconds
    
    return () => clearInterval(interval);
  }, []);

  // Close dropdowns when clicking outside or pressing Escape
  useEffect(() => {
    const handleGlobalPointer = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        if (isProfileDropdownOpen) setIsProfileDropdownOpen(false);
      }
    };
    const handleKeyDown = (e) => {
      if (e.key === 'Escape') {
        if (isProfileDropdownOpen) setIsProfileDropdownOpen(false);
        if (isNotificationDropdownOpen) setIsNotificationDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handleGlobalPointer);
    document.addEventListener('touchstart', handleGlobalPointer, { passive: true });
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handleGlobalPointer);
      document.removeEventListener('touchstart', handleGlobalPointer);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isProfileDropdownOpen, isNotificationDropdownOpen]);

  const toggleProfileDropdown = () => {
    setIsProfileDropdownOpen(!isProfileDropdownOpen);
    if (isNotificationDropdownOpen) {
      setIsNotificationDropdownOpen(false);
    }
  };

  const toggleNotificationDropdown = () => {
    setIsNotificationDropdownOpen(!isNotificationDropdownOpen);
    if (isProfileDropdownOpen) {
      setIsProfileDropdownOpen(false);
    }
    onNotificationClick();
  };

  const handleDiscoverClick = () => {
    onDiscoverClick();
  };

  return (
    <header className={`
      fixed top-0 left-0 right-0 z-50 transition-all duration-300
      ${isScrolled ? 'backdrop-blur-md bg-black/25' : 'backdrop-blur-md bg-black/15'}
      border-b border-[rgba(var(--color-primary-rgb),0.1)]
    `}>
      {/* Main header content - single compact row */}
      <div className="relative h-14 md:h-16 flex items-center justify-between px-4 md:px-6 lg:px-8">
        {/* LEFT: Brand */}
        <div className="flex items-center gap-3 md:gap-4">
          <a href="https://mozaiks.ai" target="_blank" rel="noopener noreferrer" className="flex items-center gap-2">
            <img src="/mozaik_logo.svg" className="h-7 w-7" alt="Mozaiks logo" />
            <img src="/mozaik.png" className="h-7 opacity-90" alt="Mozaiks brand" />
          </a>
        </div>

        {/* RIGHT: Commander, notifications, discover */}
        <div className="flex items-center gap-2 md:gap-3">
          {/* Commander */}
          <div className="relative" ref={dropdownRef}>
            <button onClick={toggleProfileDropdown} className="flex items-center gap-2 p-1.5 rounded-lg hover:bg-white/10 transition-colors" title="Command Profile">
              <div className="relative">
                <div className="w-8 h-8 rounded-full overflow-hidden border border-[rgba(var(--color-primary-light-rgb),0.3)]">
                  {currentUser.userPhoto ? (
                    <img src={currentUser.userPhoto} alt="User" className="w-full h-full object-cover" />
                  ) : (
                    <img src="/profile.svg" alt="profileicon" className="w-full h-full object-cover" />
                  )}
                </div>
                <div className="absolute -bottom-0.5 -right-0.5 w-3 h-3 bg-[var(--color-success)] rounded-full border border-slate-900"></div>
              </div>
              <div className="hidden lg:block text-left">
                <div className="text-[var(--color-primary-light)] text-slate-200 text-xs font-medium oxanium">{currentUser.firstName || 'Commander'}</div>
              </div>
              <svg className="w-3 h-3 text-[rgba(var(--color-primary-light-rgb),0.6)]" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
              </svg>
            </button>
            {isProfileDropdownOpen && (
              <div className="absolute right-0 top-full mt-2 w-64 bg-slate-900/95 border border-[rgba(var(--color-primary-light-rgb),0.4)] rounded-2xl backdrop-blur-xl overflow-hidden z-50">
                <div className="relative p-4 border-b border-[rgba(var(--color-primary-light-rgb),0.2)]">
                  <div className="flex items-center space-x-3">
                    <div className="relative">
                      <div className="w-12 h-12 rounded-full overflow-hidden border-2 border-[rgba(var(--color-primary-light-rgb),0.4)]">
                        {currentUser.userPhoto ? (
                          <img src={currentUser.userPhoto} alt="User" className="w-full h-full object-cover" />
                        ) : (
                          <img src="/profile.svg" alt="profileicon" className="w-full h-full object-cover" />
                        )}
                      </div>
                      <div className="absolute -bottom-1 -right-1 w-4 h-4 bg-[var(--color-success)] rounded-full border-2 border-slate-900"></div>
                    </div>
                    <div>
                      <div className="text-[var(--color-primary-light)] text-white font-semibold oxanium">{currentUser.firstName || 'Commander'}</div>
                      <div className="text-[rgba(var(--color-primary-light-rgb),0.7)] text-xs oxanium">Mission Control</div>
                    </div>
                  </div>
                </div>
                <div className="relative p-2">
                  <button className="w-full px-3 py-2.5 text-left text-[var(--color-primary-light)] text-white hover:bg-[rgba(var(--color-primary-light-rgb),0.1)] rounded-xl transition-colors flex items-center gap-3" onClick={() => console.log('Navigate to profile')}>
                    <svg className="w-4 h-4 text-[var(--color-primary-light)]" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                    </svg>
                    <span className="oxanium text-sm">Profile Settings</span>
                  </button>
                  <button className="w-full px-3 py-2.5 text-left text-[var(--color-primary-light)] text-white hover:bg-[rgba(var(--color-primary-light-rgb),0.1)] rounded-xl transition-colors flex items-center gap-3" onClick={() => console.log('Navigate to preferences')}>
                    <svg className="w-4 h-4 text-[var(--color-primary-light)]" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                      <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    </svg>
                    <span className="oxanium text-sm">Preferences</span>
                  </button>
                  <div className="border-t border-[rgba(var(--color-primary-light-rgb),0.2)] mt-2 pt-2">
                    <button onClick={() => console.log('Logout')} className="w-full px-3 py-2.5 text-left text-[var(--color-error)] hover:bg-[rgba(var(--color-error-rgb),0.1)] rounded-xl transition-colors flex items-center gap-3">
                      <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                      </svg>
                      <span className="oxanium text-sm">Sign Out</span>
                    </button>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Notifications */}
          <button onClick={toggleNotificationDropdown} className="relative p-1.5 rounded-lg hover:bg-white/10 transition-colors flex items-center justify-center" title="Mission Alerts">
            <svg className="w-6 h-6 text-[var(--color-primary-light)]" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={1.8}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M14.857 17.082a23.848 23.848 0 005.454-1.31A8.967 8.967 0 0118 9.75v-.7V9A6 6 0 006 9v.75a8.967 8.967 0 01-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 01-5.714 0m5.714 0a3 3 0 11-5.714 0" />
            </svg>
            {notificationCount > 0 && (
              <div className="absolute top-0 right-0">
                <div className="w-4 h-4 bg-[var(--color-error)] rounded-full flex items-center justify-center border border-slate-900/60">
                  <span className="text-white text-[10px] font-bold oxanium">{notificationCount}</span>
                </div>
              </div>
            )}
          </button>

          {/* Discover */}
          <button 
            onClick={handleDiscoverClick} 
            className="hidden md:flex items-center gap-2 px-4 py-2 rounded-lg bg-gradient-to-r from-[var(--color-primary)] to-[var(--color-secondary)] border-2 border-[var(--color-primary-light)] text-white oxanium hover:shadow-[0_0_20px_rgba(51,240,250,0.5)] transition-all duration-300 text-sm font-bold" 
            title="Discover New Features"
            style={{ boxShadow: '0 0 10px rgba(51,240,250,0.3)' }}
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 3v2m0 14v2m9-9h-2M5 12H3m15.364-6.364l-1.414 1.414M6.343 17.657l-1.414 1.414m12.728 0l-1.414-1.414M6.343 6.343L4.929 4.929" />
              <circle cx="12" cy="12" r="5" stroke="currentColor" strokeWidth="2.5" />
            </svg>
            <span className="font-bold tracking-wide">Discover</span>
          </button>
          {/* Discover mobile button */}
          <button
            onClick={handleDiscoverClick}
            className="md:hidden inline-flex items-center justify-center w-11 h-11 rounded-2xl bg-gradient-to-br from-[var(--color-primary)] to-[var(--color-secondary)] border border-[rgba(var(--color-primary-light-rgb),0.5)] text-white hover:shadow-[0_8px_30px_rgba(var(--color-primary-light-rgb),0.4)] transition-all"
            title="Go to Discover"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 3v2m0 14v2m9-9h-2M5 12H3m15.364-6.364l-1.414 1.414M6.343 17.657l-1.414 1.414m12.728 0l-1.414-1.414M6.343 6.343L4.929 4.929" />
              <circle cx="12" cy="12" r="5" stroke="currentColor" strokeWidth="2" />
            </svg>
          </button>
        </div>
      </div>

      {/* Mobile spacing placeholder */}
      <div className="md:hidden px-4 pb-3" />
    </header>
  );
};

export default Header;

// src/auth/ProtectedRoute.jsx
import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';

const ProtectedRoute = ({ children }) => {
  const { user, isLoading, authMode } = useAuth();
  const location = useLocation();

  if (isLoading) {
    // Show a loading spinner that respects the theme
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="text-center">
          <svg className="animate-spin h-10 w-10 text-accent mx-auto" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <p className="mt-2 text-text-primary">Loading...</p>
        </div>
      </div>
    );
  }

  if (!user) {
    // Redirect to login with the attempted location stored
    const target = authMode === 'local' ? '/login' : '/auth/login';
    return <Navigate to={target} state={{ from: location }} replace />;
  }

  return children;
};

export default ProtectedRoute;

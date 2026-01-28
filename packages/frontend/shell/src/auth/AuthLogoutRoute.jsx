import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

const AuthLogoutRoute = () => {
  const { logout } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    const run = async () => {
      await logout();
      navigate('/login', { replace: true });
    };
    run();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="text-center text-text-primary">Signing out…</div>
    </div>
  );
};

export default AuthLogoutRoute;


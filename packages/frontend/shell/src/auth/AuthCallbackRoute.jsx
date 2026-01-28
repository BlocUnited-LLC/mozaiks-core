import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

const AuthCallbackRoute = () => {
  const { authMode, completeLogin } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    const run = async () => {
      if (authMode === 'local') {
        navigate('/login', { replace: true });
        return;
      }

      const { user, returnTo } = await completeLogin();
      if (user) {
        navigate(returnTo || '/profile', { replace: true });
        return;
      }
      navigate('/login', { replace: true });
    };
    run();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="text-center text-text-primary">Completing sign-in…</div>
    </div>
  );
};

export default AuthCallbackRoute;


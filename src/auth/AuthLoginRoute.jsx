import React, { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

const AuthLoginRoute = () => {
  const { authMode, beginLogin } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    const run = async () => {
      if (authMode === 'local') {
        navigate('/login', { replace: true, state: location.state });
        return;
      }
      const returnTo = location.state?.from?.pathname;
      await beginLogin(typeof returnTo === 'string' ? returnTo : undefined);
      // If we get here, the redirect didn't happen (likely misconfigured).
      navigate('/login', { replace: true, state: location.state });
    };
    run();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="text-center text-text-primary">Redirecting to sign-in…</div>
    </div>
  );
};

export default AuthLoginRoute;

// src/auth/RegisterPage.jsx
import React, { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';
import { useTheme } from '../core/theme/useTheme';

const RegisterPage = () => {
  const { authMode, register, isLoading, error, isAuthenticated } = useAuth();
  const { branding } = useTheme();
  const navigate = useNavigate();

  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    confirmPassword: '',
    full_name: '',
  });
  const [passwordError, setPasswordError] = useState('');

  useEffect(() => {
    if (authMode !== 'local') {
      navigate('/login', { replace: true });
      return;
    }
    if (isAuthenticated) {
      navigate('/profile', { replace: true });
    }
  }, [authMode, isAuthenticated, navigate]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (name === 'password' || name === 'confirmPassword') setPasswordError('');
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (formData.password !== formData.confirmPassword) {
      setPasswordError('Passwords do not match');
      return;
    }

    const ok = await register(formData.username, formData.password, formData.email, formData.full_name, { remember: true });
    if (ok) navigate('/profile', { replace: true });
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="max-w-md w-full space-y-8 p-10 bg-primary rounded-lg shadow-lg">
        <div className="text-center">
          {branding.logo_url && (
            <img className="mx-auto h-16 w-auto mb-4" src={branding.logo_url} alt={branding.app_name || 'Mozaiks'} />
          )}
          <h2 className="text-3xl font-bold text-text-primary">Create your account</h2>
          <p className="mt-2 text-text-secondary">Local development mode</p>
        </div>

        <div className="bg-yellow-50 border border-yellow-200 text-yellow-800 px-4 py-3 rounded text-sm" role="alert">
          <strong>Local Development Mode</strong>
          <p className="mt-1">Registration is only available in local mode. Not recommended for production.</p>
        </div>

        {error && (
          <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded relative" role="alert">
            <span className="block sm:inline">{error}</span>
          </div>
        )}

        <form className="mt-8 space-y-4" onSubmit={handleSubmit}>
          <div>
            <label htmlFor="username" className="block text-sm font-medium text-text-secondary mb-1">
              Username
            </label>
            <input
              id="username"
              name="username"
              type="text"
              required
              className="appearance-none rounded relative block w-full px-3 py-2 border border-gray-300 bg-secondary placeholder-text-secondary text-text-primary focus:outline-none focus:ring-accent focus:border-accent focus:z-10 sm:text-sm"
              placeholder="Choose a username"
              value={formData.username}
              onChange={handleChange}
            />
          </div>

          <div>
            <label htmlFor="full_name" className="block text-sm font-medium text-text-secondary mb-1">
              Full Name
            </label>
            <input
              id="full_name"
              name="full_name"
              type="text"
              required
              className="appearance-none rounded relative block w-full px-3 py-2 border border-gray-300 bg-secondary placeholder-text-secondary text-text-primary focus:outline-none focus:ring-accent focus:border-accent focus:z-10 sm:text-sm"
              placeholder="Your full name"
              value={formData.full_name}
              onChange={handleChange}
            />
          </div>

          <div>
            <label htmlFor="email" className="block text-sm font-medium text-text-secondary mb-1">
              Email
            </label>
            <input
              id="email"
              name="email"
              type="email"
              required
              className="appearance-none rounded relative block w-full px-3 py-2 border border-gray-300 bg-secondary placeholder-text-secondary text-text-primary focus:outline-none focus:ring-accent focus:border-accent focus:z-10 sm:text-sm"
              placeholder="Your email address"
              value={formData.email}
              onChange={handleChange}
            />
          </div>

          <div>
            <label htmlFor="password" className="block text-sm font-medium text-text-secondary mb-1">
              Password
            </label>
            <input
              id="password"
              name="password"
              type="password"
              required
              className="appearance-none rounded relative block w-full px-3 py-2 border border-gray-300 bg-secondary placeholder-text-secondary text-text-primary focus:outline-none focus:ring-accent focus:border-accent focus:z-10 sm:text-sm"
              placeholder="Create a password"
              value={formData.password}
              onChange={handleChange}
            />
          </div>

          <div>
            <label htmlFor="confirmPassword" className="block text-sm font-medium text-text-secondary mb-1">
              Confirm Password
            </label>
            <input
              id="confirmPassword"
              name="confirmPassword"
              type="password"
              required
              className={`appearance-none rounded relative block w-full px-3 py-2 border ${
                passwordError ? 'border-red-500' : 'border-gray-300'
              } bg-secondary placeholder-text-secondary text-text-primary focus:outline-none focus:ring-accent focus:border-accent focus:z-10 sm:text-sm`}
              placeholder="Confirm your password"
              value={formData.confirmPassword}
              onChange={handleChange}
            />
            {passwordError && <p className="mt-1 text-sm text-red-600">{passwordError}</p>}
          </div>

          <div className="mt-8">
            <button
              type="submit"
              disabled={isLoading}
              className="group relative w-full flex justify-center py-2 px-4 border border-transparent text-sm font-medium rounded-md text-white bg-accent hover:opacity-90 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-accent"
            >
              {isLoading ? 'Creating account…' : 'Create Account'}
            </button>
          </div>

          <div className="text-sm text-center mt-4">
            <span className="text-text-secondary">Already have an account? </span>
            <Link to="/login" className="font-medium text-accent hover:opacity-80">
              Sign in
            </Link>
          </div>
        </form>
      </div>
    </div>
  );
};

export default RegisterPage;


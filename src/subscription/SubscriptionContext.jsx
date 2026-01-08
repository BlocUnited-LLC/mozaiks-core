// src/subscription/SubscriptionContext.jsx
import React, { createContext, useState, useEffect, useContext } from 'react';
import { useAuth } from '../auth/AuthContext';

const SubscriptionContext = createContext(null);

export const SubscriptionProvider = ({ children }) => {
  const { isAuthenticated, user, authFetch } = useAuth();
  const [plans, setPlans] = useState([]);
  const [userSubscription, setUserSubscription] = useState(null);
  const [settings, setSettings] = useState({
    show_subscription_status: true,
    show_cancel_button: true,
    trial_period_days: 14,
    default_plan: "free"
  });
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [processingAction, setProcessingAction] = useState(false);

  // Load subscription plans and user's current subscription
  useEffect(() => {
    const fetchSubscriptionData = async () => {
      if (!isAuthenticated) {
        setIsLoading(false);
        return;
      }

      setIsLoading(true);
      setError(null);

      try {
        // Fetch available plans
        const plansResponse = await authFetch('/api/subscription-plans');

        if (!plansResponse.ok) {
          throw new Error('Failed to fetch subscription plans');
        }

        const plansData = await plansResponse.json();
        
        // Extract settings if they exist
        if (plansData.settings) {
          setSettings(plansData.settings);
        }
        
        // Set plans
        setPlans(plansData.subscription_plans || plansData);

        // Fetch user's current subscription
        const subscriptionResponse = await authFetch('/api/user-subscription');
        
        if (!subscriptionResponse.ok) {
          throw new Error('Failed to fetch user subscription');
        }

        const subscriptionData = await subscriptionResponse.json();
        console.log('FULL API RESPONSE:', JSON.stringify(subscriptionData, null, 2));
        
        // Add this simple patch if the API still doesn't return trial_info
        if (subscriptionData.status === 'trialing' && !subscriptionData.trial_info) {
          console.log('Adding trial_info in frontend');
          subscriptionData.is_trial = true;
          subscriptionData.trial_info = {
            days_remaining: 14,
            end_date: new Date(Date.now() + 14 * 24 * 60 * 60 * 1000).toISOString()
          };
        }
        console.log('Is trial field present?', 'is_trial' in subscriptionData);
        console.log('Is trial value:', subscriptionData.is_trial);
        console.log('Trial info field present?', 'trial_info' in subscriptionData);
        setUserSubscription(subscriptionData);
      } catch (err) {
        console.error('Error fetching subscription data:', err);
        setError(err.message);
      } finally {
        setIsLoading(false);
      }
    };

    fetchSubscriptionData();
  }, [isAuthenticated, user, authFetch]);

  // Function to change subscription
  const changeSubscription = async (newPlan) => {
    if (!isAuthenticated) return false;

    setProcessingAction(true);
    setError(null);

    try {
      const response = await authFetch('/api/update-subscription', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ new_plan: newPlan })
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.detail || 'Failed to update subscription');
      }

      // Refresh user subscription data
      const subscriptionResponse = await authFetch('/api/user-subscription');

      if (!subscriptionResponse.ok) {
        throw new Error('Failed to fetch updated subscription');
      }

      const subscriptionData = await subscriptionResponse.json();
      setUserSubscription(subscriptionData);

      // Force reload the page to refresh navigation and plugin access
      window.location.reload();

      return true;
    } catch (err) {
      console.error('Error changing subscription:', err);
      setError(err.message);
      return false;
    } finally {
      setProcessingAction(false);
    }
  };

  // Function to cancel subscription
  const cancelSubscription = async () => {
    if (!isAuthenticated) return false;

    setProcessingAction(true);
    setError(null);

    try {
      const response = await authFetch('/api/cancel-subscription', {
        method: 'POST',
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.detail || 'Failed to cancel subscription');
      }

      // Refresh user subscription data
      const subscriptionResponse = await authFetch('/api/user-subscription');

      if (subscriptionResponse.ok) {
        const subscriptionData = await subscriptionResponse.json();
        setUserSubscription(subscriptionData);
      }

      return true;
    } catch (err) {
      console.error('Error canceling subscription:', err);
      setError(err.message);
      return false;
    } finally {
      setProcessingAction(false);
    }
  };

  // Check if user has access to a plugin
  const hasPluginAccess = async (pluginName) => {
    if (!isAuthenticated) return false;

    try {
      const response = await authFetch(`/api/check-plugin-access/${pluginName}`);

      if (!response.ok) {
        throw new Error('Failed to check plugin access');
      }

      const data = await response.json();
      return data.access;
    } catch (err) {
      console.error('Error checking plugin access:', err);
      return false;
    }
  };

  const value = {
    plans,
    userSubscription,
    settings,
    isLoading,
    error,
    processingAction,
    changeSubscription,
    cancelSubscription,
    hasPluginAccess,
    getCurrentPlan: () => plans.find(p => p.name === userSubscription?.plan),
  };

  return <SubscriptionContext.Provider value={value}>{children}</SubscriptionContext.Provider>;
};

export const useSubscription = () => {
  const context = useContext(SubscriptionContext);
  if (!context) {
    throw new Error('useSubscription must be used within a SubscriptionProvider');
  }
  return context;
};

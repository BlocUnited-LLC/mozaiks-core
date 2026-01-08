// src/subscription/SubscriptionPage.jsx
import React, { useState } from 'react';
import { useSubscription } from './SubscriptionContext';
import { useTheme } from '../core/theme/useTheme';

const SubscriptionPage = () => {
  const { 
    plans,
    userSubscription, 
    settings,
    isLoading, 
    error, 
    processingAction,
    changeSubscription,
    cancelSubscription,
    getCurrentPlan
  } = useSubscription();
  
  // Debug logging
  console.log('SUBSCRIPTION PAGE RENDERING');
  console.log('userSubscription object:', userSubscription);
  console.log('Complete userSubscription JSON:', JSON.stringify(userSubscription, null, 2));
  
  // Add trial status check
  console.log('Trial status check:', {
    plan: userSubscription?.plan,
    status: userSubscription?.status,
    is_trial: userSubscription?.is_trial,
    trial_info: userSubscription?.trial_info
  });
  
  const { branding } = useTheme();
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [confirmChangePlan, setConfirmChangePlan] = useState(null); // New state for plan change confirmation
  const [actionError, setActionError] = useState(null);
  const [actionSuccess, setActionSuccess] = useState(null);

  const currentPlan = getCurrentPlan();

  const initiateChangePlan = (planName) => {
    // Don't do anything if it's the same plan
    if (planName === userSubscription?.plan && userSubscription?.status !== 'trialing') return;
    
    // Set which plan we're confirming
    setConfirmChangePlan(planName);
  };

  const handleChangePlan = async () => {
    if (!confirmChangePlan) return;
    
    setActionError(null);
    setActionSuccess(null);
    
    const success = await changeSubscription(confirmChangePlan);
    
    if (success) {
      setActionSuccess(`Successfully changed to ${confirmChangePlan} plan`);
      setTimeout(() => setActionSuccess(null), 5000);
    } else {
      setActionError('Failed to change subscription plan');
    }
    
    // Reset confirmation state
    setConfirmChangePlan(null);
  };

  const handleCancelSubscription = async () => {
    setActionError(null);
    setActionSuccess(null);
    
    const success = await cancelSubscription();
    
    if (success) {
      setConfirmCancel(false);
      setActionSuccess('Your subscription has been canceled');
      setTimeout(() => setActionSuccess(null), 5000);
    } else {
      setActionError('Failed to cancel subscription');
    }
  };

  // Loading state
  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="text-center">
          <svg className="animate-spin h-10 w-10 text-accent mx-auto" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <p className="mt-2 text-text-primary">Loading subscription information...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto p-6 max-w-4xl bg-background">
      <header className="mb-8 text-center">
        <h1 className="text-3xl font-bold text-text-primary mb-2">
          Subscription Management
        </h1>
        <p className="text-text-secondary">
          Manage your {branding.app_name || "Mozaiks"} subscription
        </p>
      </header>
      
      {(error || actionError) && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-6" role="alert">
          <p className="font-bold">Error</p>
          <p>{error || actionError}</p>
        </div>
      )}
      
      {actionSuccess && (
        <div className="bg-green-100 border border-green-400 text-green-700 px-4 py-3 rounded mb-6" role="alert">
          <p>{actionSuccess}</p>
        </div>
      )}

      {/* Trial Banner - Updated to always show when status is trialing */}
      {userSubscription?.status === 'trialing' && (
        <div className="bg-blue-100 border border-blue-400 text-blue-800 px-6 py-4 rounded-lg mb-8 shadow-sm">
          <div className="flex items-start">
            <div className="mr-4 bg-blue-200 p-2 rounded-full">
              <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <div>
              <h3 className="font-bold text-lg mb-1">Trial Subscription</h3>
              <p className="mb-2">
                You're currently on a free trial of the <span className="font-medium">{currentPlan?.display_name}</span> plan. 
                {userSubscription?.trial_info?.days_remaining ? (
                  <span> Your trial will expire in <span className="font-bold">{userSubscription.trial_info.days_remaining} days</span>.</span>
                ) : (
                  <span> Your 14-day trial period is currently active.</span>
                )}
              </p>
              <p className="text-sm">
                To continue using premium features after your trial ends, please select a plan below.
              </p>
            </div>
          </div>
        </div>
      )}
      
      {/* Current Subscription */}
      <section className="mb-10 bg-primary p-6 rounded-lg shadow-md">
        <h2 className="text-xl font-semibold text-text-primary mb-4">Current Subscription</h2>
        
        <div className="bg-secondary p-4 rounded-md mb-4">
          <div className="flex flex-col md:flex-row justify-between">
            <div>
              <h3 className="font-bold text-lg text-text-primary">
                {currentPlan ? currentPlan.display_name : 'Free'}
                {userSubscription?.status === 'trialing' && (
                  <span className="ml-2 text-sm bg-blue-500 text-white px-2 py-0.5 rounded-full">
                    TRIAL
                  </span>
                )}
              </h3>
              
              {/* Only show status if enabled in settings */}
              {settings.show_subscription_status && (
                <p className="text-text-secondary">
                  Status: <span className="font-medium">{userSubscription?.status || 'Inactive'}</span>
                </p>
              )}
              
              {userSubscription?.status === 'trialing' && userSubscription?.trial_info ? (
                <p className="text-text-secondary">
                  Trial ends: <span className="font-medium">
                    {new Date(userSubscription.trial_info.end_date).toLocaleDateString()}
                  </span>
                </p>
              ) : userSubscription?.next_billing_date ? (
                <p className="text-text-secondary">
                  Next billing: <span className="font-medium">
                    {new Date(userSubscription.next_billing_date).toLocaleDateString()}
                  </span>
                </p>
              ) : null}
            </div>
            
            <div className="mt-4 md:mt-0">
              <p className="text-2xl font-bold text-accent">
                {currentPlan ? `$${currentPlan.price}` : '$0'} 
                <span className="text-sm text-text-secondary font-normal">
                  /{currentPlan?.billing_cycle || 'month'}
                </span>
              </p>
              {userSubscription?.status === 'trialing' && (
                <p className="text-xs text-right text-accent">Free during trial</p>
              )}
            </div>
          </div>
        </div>

        {/* Only show cancel button if enabled in settings and not in trial */}
        {settings.show_cancel_button && userSubscription?.status === 'active' && !confirmCancel && (
          <button
            onClick={() => setConfirmCancel(true)}
            className="text-red-600 hover:text-red-800 font-medium text-sm"
          >
            Cancel Subscription
          </button>
        )}
        
        {confirmCancel && (
          <div className="bg-red-50 border border-red-200 p-4 rounded mt-4">
            <p className="text-red-700 mb-3">Are you sure you want to cancel your subscription?</p>
            <div className="flex space-x-3">
              <button
                onClick={handleCancelSubscription}
                disabled={processingAction}
                className="bg-red-600 text-white px-4 py-2 rounded text-sm hover:bg-red-700"
              >
                {processingAction ? 'Processing...' : 'Yes, Cancel'}
              </button>
              <button
                onClick={() => setConfirmCancel(false)}
                className="bg-gray-200 text-gray-800 px-4 py-2 rounded text-sm hover:bg-gray-300"
              >
                No, Keep Subscription
              </button>
            </div>
          </div>
        )}
      </section>
      
      {/* Change Plan Confirmation Dialog */}
      {confirmChangePlan && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-lg p-6 max-w-md w-full">
            <h3 className="text-xl font-bold mb-4">Confirm Subscription Change</h3>
            <p className="mb-6">
              Are you sure you want to change your subscription to the <span className="font-medium">{plans.find(p => p.name === confirmChangePlan)?.display_name}</span> plan?
            </p>
            <div className="flex space-x-3 justify-end">
              <button
                onClick={() => setConfirmChangePlan(null)}
                className="px-4 py-2 text-gray-700 border border-gray-300 rounded hover:bg-gray-100"
              >
                Cancel
              </button>
              <button
                onClick={handleChangePlan}
                disabled={processingAction}
                className="px-4 py-2 bg-accent text-white rounded hover:bg-opacity-90"
              >
                {processingAction ? 'Processing...' : 'Confirm Change'}
              </button>
            </div>
          </div>
        </div>
      )}
      
      {/* Available Plans */}
      <section className="mb-10">
        <h2 className="text-xl font-semibold text-text-primary mb-6">Available Plans</h2>
        
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {plans.map((plan) => (
            <div 
              key={plan.name}
              className={`
                bg-primary rounded-lg shadow-md overflow-hidden border-2
                ${plan.name === userSubscription?.plan 
                  ? 'border-accent' 
                  : 'border-transparent hover:border-gray-300'}
              `}
            >
              <div className="p-6">
                <h3 className="text-xl font-bold text-text-primary mb-2">
                  {plan.display_name}
                </h3>
                <p className="text-2xl font-bold text-accent mb-4">
                  ${plan.price}
                  <span className="text-sm text-text-secondary font-normal">
                    /{plan.billing_cycle || 'month'}
                  </span>
                </p>
                
                <ul className="mb-6 text-text-secondary">
                  {plan.features && plan.features.map((feature, index) => (
                    <li key={index} className="flex items-start mb-2">
                      <svg className="h-5 w-5 text-accent mr-2 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                      </svg>
                      <span>{feature}</span>
                    </li>
                  ))}
                </ul>
                
                {plan.name === 'premium' && userSubscription?.status === 'trialing' ? (
                  <button
                    disabled={true}
                    className="w-full py-2 px-4 rounded text-center font-medium bg-blue-100 text-blue-700 border border-blue-300"
                  >
                    Current Trial Plan
                  </button>
                ) : (
                  <button
                    onClick={() => initiateChangePlan(plan.name)}
                    disabled={processingAction || (plan.name === userSubscription?.plan && userSubscription?.status !== 'trialing')}
                    className={`
                      w-full py-2 px-4 rounded text-center font-medium
                      ${plan.name === userSubscription?.plan && userSubscription?.status !== 'trialing'
                        ? 'bg-gray-200 text-gray-500 cursor-not-allowed'
                        : 'bg-accent text-white hover:opacity-90'}
                    `}
                  >
                    {processingAction 
                      ? 'Processing...' 
                      : plan.name === userSubscription?.plan 
                        ? (userSubscription?.status === 'trialing' ? 'Select After Trial' : 'Current Plan')
                        : 'Select Plan'}
                  </button>
                )}
                
                {userSubscription?.status === 'trialing' && plan.name === userSubscription?.plan && (
                  <p className="text-center text-xs mt-2 text-text-secondary">
                    Your trial will convert to this plan unless you choose another
                  </p>
                )}
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
};

export default SubscriptionPage;
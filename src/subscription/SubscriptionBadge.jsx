// src/subscription/SubscriptionBadge.jsx
import React from 'react';
import { useSubscription } from './SubscriptionContext';

const SubscriptionBadge = ({ className = '' }) => {
  const { userSubscription, getCurrentPlan } = useSubscription();
  
  const currentPlan = getCurrentPlan();
  
  if (!userSubscription) {
    return null;
  }
  
  let color = 'bg-gray-100 text-gray-800';
  
  if (userSubscription.status === 'active') {
    color = 'bg-green-100 text-green-800';
  } else if (userSubscription.status === 'trialing') {
    color = 'bg-blue-100 text-blue-800';
  } else if (userSubscription.status === 'past_due') {
    color = 'bg-yellow-100 text-yellow-800';
  }
  
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${color} ${className}`}>
      {currentPlan ? currentPlan.display_name : 'Free'} • {userSubscription.status}
    </span>
  );
};

export default SubscriptionBadge;
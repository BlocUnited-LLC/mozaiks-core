// src/subscription/PluginAccessIndicator.jsx
import React, { useState, useEffect } from 'react';
import { useSubscription } from './SubscriptionContext';

const PluginAccessIndicator = ({ pluginName }) => {
  const { hasPluginAccess } = useSubscription();
  const [hasAccess, setHasAccess] = useState(null);
  const [loading, setLoading] = useState(true);
  
  useEffect(() => {
    const checkAccess = async () => {
      const access = await hasPluginAccess(pluginName);
      setHasAccess(access);
      setLoading(false);
    };
    
    checkAccess();
  }, [pluginName, hasPluginAccess]);
  
  if (loading) {
    return <span className="inline-block w-4 h-4 rounded-full animate-pulse bg-gray-300"></span>;
  }
  
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
      hasAccess ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
    }`}>
      {hasAccess ? 'Access Granted' : 'Upgrade Required'}
    </span>
  );
};

export default PluginAccessIndicator;
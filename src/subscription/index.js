// src/subscription/index.js
import SubscriptionPage from './SubscriptionPage';
import SubscriptionBadge from './SubscriptionBadge';
import { SubscriptionProvider } from './SubscriptionContext';
import PlanFeatureList from './PlanFeatureList';
import PluginAccessIndicator from './PluginAccessIndicator';

// Export components for direct use
export { 
  SubscriptionPage, 
  SubscriptionBadge, 
  SubscriptionProvider,
  PlanFeatureList,
  PluginAccessIndicator
};

// Default export is the main page component
export default SubscriptionPage;
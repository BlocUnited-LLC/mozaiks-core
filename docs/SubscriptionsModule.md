# Subscriptions Module

## Overview
The Subscriptions module manages subscription tiers, trials, billing, and feature access gating throughout the Mozaiks platform. It integrates with payment services and provides necessary data for secure access control based on user subscription plans, ensuring that users can only access features appropriate for their subscription level.

## Core Responsibilities
- Subscription plan definition and management
- Trial period handling and expiration tracking
- Plugin and feature access control
- Subscription history tracking
- Payment processing and billing history (when MONETIZATION enabled)
- Frontend subscription UI and user plan selection
- Monetization detection and stub service when disabled

## Dependencies

### Internal Dependencies
- **Orchestration**: API route registration via `director.py`
- **Auth**: User registration integration for trial activation
- **Database**: MongoDB collections for subscription data
- **Event Bus**: Subscription event publishing
- **Plugins**: Access control for plugin execution

### External Dependencies
- **fastapi**: API route definition
- **pymongo**: Database interaction
- **react**: Frontend components for subscription management
- **dateutil**: Date calculation for billing cycles

## API Reference

### Backend Endpoints

#### `GET /api/subscription-plans`
Returns available subscription plans with settings.
- **Returns**: Complete subscription configuration

#### `GET /api/user-subscription`
Get current user subscription details.
- **Parameters**:
  - Requires authentication
- **Returns**: User's current subscription details

#### `POST /api/update-subscription`
Updates a user's subscription plan.
- **Parameters**:
  - `new_plan`: New plan name
  - Requires authentication
- **Returns**: Success message with new plan details

#### `POST /api/cancel-subscription`
Cancels a user's subscription.
- **Parameters**:
  - Requires authentication
- **Returns**: Success message

### Backend Methods

#### `subscription_manager.get_available_plans()`
Returns all available subscription plans.
- **Returns**: Array of plan objects

#### `subscription_manager.get_user_subscription(user_id)`
Retrieves the user's current subscription from MongoDB.
- **Parameters**:
  - `user_id` (str): User ID to get subscription for
- **Returns**: Subscription details object

#### `subscription_manager.change_user_subscription(user_id, new_plan)`
Updates the user's subscription plan in MongoDB.
- **Parameters**:
  - `user_id` (str): User ID to update
  - `new_plan` (str): New plan name
- **Returns**: Success message with new plan details

#### `subscription_manager.cancel_user_subscription(user_id)`
Cancels the user's subscription.
- **Parameters**:
  - `user_id` (str): User ID to update
- **Returns**: Success message

#### `subscription_manager.start_user_trial(user_id)`
Start a trial subscription for a new user.
- **Parameters**:
  - `user_id` (str): User ID to start trial for
- **Returns**: Trial details

#### `subscription_manager.check_trial_status(user_id)`
Check if a trial has expired and update status if needed.
- **Parameters**:
  - `user_id` (str): User ID to check
- **Returns**: Trial status object

#### `subscription_manager.is_plugin_accessible(user_id, plugin_name)`
Checks whether a user has access to a given plugin.
- **Parameters**:
  - `user_id` (str): User ID to check
  - `plugin_name` (str): Plugin name to check
- **Returns**: Boolean indicating access

#### `subscription_manager.log_billing_event(user_id, amount, event_type, status, metadata=None)`
Logs billing events such as payments, refunds, and invoices.
- **Parameters**:
  - `user_id` (str): User ID
  - `amount` (float): Transaction amount
  - `event_type` (str): Type of billing event (payment, refund, invoice)
  - `status` (str): Status of event (successful, failed, pending)
  - `metadata` (dict, optional): Additional metadata
- **Returns**: None

### Frontend Methods

#### `useSubscription()` Hook
Custom React hook to access subscription state and functions.
- **Returns**:
  - `plans` (array): Available subscription plans
  - `userSubscription` (object): User's current subscription
  - `settings` (object): Subscription settings
  - `isLoading` (boolean): Loading state
  - `error` (string): Error message
  - `processingAction` (boolean): Action in progress
  - `changeSubscription(newPlan)` (function): Change subscription
  - `cancelSubscription()` (function): Cancel subscription
  - `hasPluginAccess(pluginName)` (function): Check plugin access
  - `getCurrentPlan()` (function): Get current plan details

## Configuration

### Subscription Config
Located at `/backend/core/config/subscription_config.json`.

Example structure:
```json
{
  "settings": {
    "show_subscription_status": true,
    "show_cancel_button": true,
    "trial_period_days": 14,
    "trial_plan": "premium",
    "default_plan": "free"
  },
  "subscription_plans": [
    {
      "name": "free",
      "display_name": "Free",
      "price": 0,
      "billing_cycle": "monthly",
      "features": [
        "Basic access to the platform",
        "Limited number of plugins",
        "Up to 5 notes"
      ],
      "plugins_unlocked": []
    },
    {
      "name": "basic",
      "display_name": "Basic",
      "price": 9.99,
      "billing_cycle": "monthly",
      "features": [
        "Full access to the platform",
        "Access to basic plugins",
        "Unlimited notes",
        "Email support"
      ],
      "plugins_unlocked": [
        "notes_manager"
      ]
    },
    {
      "name": "premium",
      "display_name": "Premium",
      "price": 19.99,
      "billing_cycle": "monthly",
      "features": [
        "Everything in Basic",
        "Access to all plugins",
        "Priority support",
        "Advanced features"
      ],
      "plugins_unlocked": [
        "*"
      ]
    }
  ]
}
```

### Environment Variables
- `MONETIZATION`: Set to "1" to enable monetization, "0" to disable
- `SUBSCRIPTION_API_URL`: URL of the external subscription service (if any)

## Data Models

### Subscription Plan
```typescript
interface SubscriptionPlan {
  name: string;           // Plan identifier
  display_name: string;   // Human-readable name
  price: number;          // Price in currency units
  billing_cycle: string;  // Billing frequency (monthly, yearly)
  features: string[];     // List of features as strings
  plugins_unlocked: string[]; // List of plugin names or "*" for all
}
```

### User Subscription
```typescript
interface UserSubscription {
  user_id: string;        // User ID
  plan: string;           // Current plan name
  status: string;         // Status: active, inactive, trialing, past_due
  billing_cycle: string;  // Billing frequency
  next_billing_date: string; // ISO date string
  updated_at: string;     // ISO date string
  is_trial: boolean;      // Whether this is a trial
  trial_info?: {          // Only present for trials
    days_remaining: number;
    end_date: string;     // ISO date string
  }
}
```

### Billing History Entry
```typescript
interface BillingEvent {
  user_id: string;        // User ID
  amount: number;         // Amount in currency units
  event_type: string;     // payment, refund, invoice
  status: string;         // successful, failed, pending
  timestamp: string;      // ISO date string
  metadata: object;       // Additional information
}
```

## Integration Points

### Plugin Access Control
Plugin access is controlled via the subscription manager:

```python
# In director.py
if MONETIZATION and not await subscription_manager.is_plugin_accessible(user_id, plugin_name):
    raise HTTPException(status_code=403, detail="Access denied")
```

### Trial Activation on Registration
New users get a trial when they register:

```python
# In auth.py during registration
trial_info = await subscription_manager.start_user_trial(user_id)
```

### Frontend Component Integration
Display subscription-related UI components:

```jsx
import { useSubscription } from './subscription/SubscriptionContext';
import { SubscriptionBadge } from './subscription/SubscriptionBadge';
import { PlanFeatureList } from './subscription/PlanFeatureList';

const MyComponent = () => {
  const { userSubscription, getCurrentPlan } = useSubscription();
  const currentPlan = getCurrentPlan();
  
  return (
    <div>
      <h2>Your Subscription</h2>
      <SubscriptionBadge />
      {currentPlan && <PlanFeatureList plan={currentPlan} />}
    </div>
  );
};
```

## Events

### Events Published
- `subscription_updated`: When a subscription is updated
- `subscription_canceled`: When a subscription is canceled
- `trial_started`: When a trial is started
- `trial_ended`: When a trial ends

### Events Subscribed To
- None directly

## Monetization Modes

### Full Monetization (MONETIZATION=1)
When monetization is enabled:
- Real subscription plans are available
- Trials expire after the configured period
- Plugin access is restricted based on plan
- Billing events are logged

### Non-Monetized Mode (MONETIZATION=0)
When monetization is disabled:
- `SubscriptionStub` is used instead of the full manager
- All users get the "unlimited" plan automatically
- All plugins are accessible to all users
- No billing or payment processing occurs

## Trial Management

The module handles trial subscriptions with these steps:

1. **Trial Activation**:
   - New user registers
   - `start_user_trial` assigns premium trial
   - Trial expiration date is calculated

2. **Trial Status Checking**:
   - `check_trial_status` is called when retrieving subscription
   - If trial has expired, user is downgraded to free plan
   - Expired trial event is published to event bus

3. **Trial UI**:
   - Frontend displays trial status and days remaining
   - Warning appears as trial expiration approaches
   - Plan selection UI shows options for after trial

4. **Trial Conversion**:
   - User can convert trial to paid plan at any time
   - No service interruption during conversion
   - Trial history is preserved in subscription history

## Common Issues & Troubleshooting

### Trial Not Starting
- Check that `trial_period_days` is properly set in config
- Verify `trial_plan` is a valid plan name
- Look for errors in user registration process
- Check that `start_user_trial` is being called

### Plugin Access Issues
- Verify plugin is listed in appropriate plan's `plugins_unlocked`
- Check that special wildcard `"*"` works correctly
- Ensure subscription status is active or trialing
- Verify `is_plugin_accessible` is being called correctly

### Subscription Changes Not Taking Effect
- Check for cached navigation or plugin access data
- Verify events are being published correctly
- Look for database write errors
- Check that frontend is refreshing on plan changes

### Billing Date Calculation Issues
- Verify `calculate_next_billing_date` logic
- Check timezone handling for billing dates
- Ensure date format consistency

## Subscription Management UI

The module provides several React components for subscription management:

### `SubscriptionPage`
Main page for viewing and managing subscriptions.

### `SubscriptionBadge`
Displays current subscription status in a badge format.

### `PlanFeatureList`
Displays features included in a subscription plan.

### `PluginAccessIndicator`
Shows whether a user has access to a specific plugin.

## Related Files
- `/backend/core/subscription_manager.py`
- `/backend/core/subscription_stub.py`
- `/backend/core/config/subscription_config.json`
- `/backend/core/director.py` (subscription endpoints)
- `/src/subscription/SubscriptionContext.jsx`
- `/src/subscription/SubscriptionPage.jsx`
- `/src/subscription/SubscriptionBadge.jsx`
- `/src/subscription/PlanFeatureList.jsx`
- `/src/subscription/PluginAccessIndicator.jsx`
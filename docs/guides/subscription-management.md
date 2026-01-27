# Subscription Management

Control which plugins users can access based on their plan.

---

## 1. Define Your Plans

Create `subscription_config.json`:

```json
{
  "subscription_plans": [
    {
      "name": "free",
      "plugins_unlocked": ["basic_chat"]
    },
    {
      "name": "pro",
      "plugins_unlocked": ["*"]
    }
  ]
}
```

- `"*"` = all plugins
- List specific plugins to restrict access

---

## 2. Set Environment Variables

```bash
export MOZAIKS_CONFIGS_PATH="/path/to/config"
export INTERNAL_API_KEY="your-secret-key"
```

---

## 3. Update User Plans

When someone pays (or you want to upgrade them manually):

```bash
curl -X POST http://localhost:8000/api/internal/subscription/sync \
  -H "X-Internal-API-Key: $INTERNAL_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"userId": "user_123", "plan": "pro", "status": "active"}'
```

That's it. User now has access to all plugins.

---

## Connecting Stripe (or any payment provider)

Your payment provider sends a webhook → you call the sync endpoint.

```
Stripe → Your webhook handler → POST /api/internal/subscription/sync → Done
```

See `examples/billing/stripe_webhook.py` for a complete example.

---

## Reference

### Sync Endpoint

```
POST /api/internal/subscription/sync
Header: X-Internal-API-Key: {your-key}

{
  "userId": "user_123",
  "plan": "pro",
  "status": "active"
}
```

Status options: `active`, `trialing`, `inactive`, `canceled`

### Config File

```json
{
  "subscription_plans": [
    {
      "name": "free",
      "plugins_unlocked": ["basic_chat"]
    },
    {
      "name": "pro", 
      "plugins_unlocked": ["*"]
    }
  ],
  "settings": {
    "trial_plan": "pro",
    "trial_period_days": 14
  }
}
```

# Billing Integration Examples

This folder contains ready-to-use examples for connecting your payment provider to mozaiks.

## Quick Start

### Stripe Integration

1. Copy the example files:
   ```bash
   cp stripe_webhook.py /path/to/your/project/
   cp requirements.txt /path/to/your/project/
   ```

2. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```

3. Set environment variables:
   ```bash
   export STRIPE_SECRET_KEY="sk_..."
   export STRIPE_WEBHOOK_SECRET="whsec_..."
   export MOZAIKS_API_URL="http://localhost:8000"
   export INTERNAL_API_KEY="your-internal-api-key"  # Same key used by mozaiks
   ```

4. Run the webhook handler:
   ```bash
   python stripe_webhook.py
   # Or for production:
   gunicorn stripe_webhook:app -b 0.0.0.0:4242
   ```

5. Configure Stripe to send webhooks to:
   ```
   https://your-domain.com/webhooks/stripe
   ```

### Mapping Price IDs to Plans

Edit `stripe_webhook.py` to map your Stripe price IDs to mozaiks plan IDs:

```python
PRICE_TO_PLAN = {
    "price_your_free_price_id": "free",
    "price_your_starter_price_id": "starter",
    "price_your_pro_price_id": "pro",
}
```

### User ID Tracking

When creating a Stripe Checkout Session, include the user ID in metadata:

```python
session = stripe.checkout.Session.create(
    customer=stripe_customer_id,
    line_items=[{"price": "price_...", "quantity": 1}],
    mode="subscription",
    subscription_data={
        "metadata": {
            "user_id": "your-app-user-id",  # Required!
        }
    },
    success_url="https://your-app.com/success",
    cancel_url="https://your-app.com/cancel",
)
```

## Other Payment Providers

The sync endpoint is provider-agnostic. You can integrate any payment system by calling:

```bash
POST /api/internal/subscription/sync
X-Internal-API-Key: <your-internal-key>
Content-Type: application/json

{
    "userId": "user_123",
    "plan": "pro",
    "status": "active"
}
```

### Example: Generic Webhook Template

```python
# generic_webhook.py
from flask import Flask, request, jsonify
import requests
import os

app = Flask(__name__)
INTERNAL_KEY = os.environ.get("INTERNAL_API_KEY")

@app.route("/webhooks/payment", methods=["POST"])
def payment_webhook():
    # Parse your payment provider's webhook format
    data = request.json
    
    # Extract user_id, plan, and status
    user_id = data["customer"]["metadata"]["user_id"]
    plan = map_product_to_plan(data["product_id"])
    status = "active" if data["status"] == "paid" else "canceled"
    
    # Sync to mozaiks
    requests.post(
        "http://localhost:8000/api/internal/subscription/sync",
        headers={"X-Internal-API-Key": INTERNAL_KEY},
        json={"userId": user_id, "plan": plan, "status": status}
    )
    
    return jsonify({"ok": True})
```

## Docker Deployment

```dockerfile
FROM python:3.11-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY stripe_webhook.py .

CMD ["gunicorn", "stripe_webhook:app", "-b", "0.0.0.0:4242"]
```

Build and run:
```bash
docker build -t mozaiks-stripe-webhook .
docker run -p 4242:4242 \
  -e STRIPE_SECRET_KEY="sk_..." \
  -e STRIPE_WEBHOOK_SECRET="whsec_..." \
  -e MOZAIKS_API_URL="http://mozaiks:8000" \
  -e INTERNAL_API_KEY="your-internal-key" \
  mozaiks-stripe-webhook
```

## Testing

Use Stripe CLI to forward webhooks locally:

```bash
# Install Stripe CLI
brew install stripe/stripe-cli/stripe

# Login
stripe login

# Forward webhooks to your local handler
stripe listen --forward-to localhost:4242/webhooks/stripe

# In another terminal, trigger a test event
stripe trigger customer.subscription.created
```

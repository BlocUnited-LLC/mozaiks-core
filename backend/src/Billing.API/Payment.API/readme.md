# Payments Flow Documentation

All payments — whether for subscriptions, investments, or app purchases — are processed through a centralized backend controller.

Every frontend action initiates a payment by sending:

- `transaction_type`: A string indicating the type of payment
- `metadata`: Context-specific data for routing and processing the payment

---

## ✅ 1. Mozaiks Platform Subscription

| Attribute             | Description                                   |
| --------------------- | --------------------------------------------- |
| **transaction\_type** | `platform_subscription`                       |
| **Payer**             | Mozaiks user                                  |
| **Receiver**          | Mozaiks                                       |
| **Purpose**           | Access to Mozaiks tools and platform features |
| **Mozaiks Fee**       | N/A (Mozaiks receives 100%)                   |
| **Provider Flow**     | Standard subscription (non-connected)         |

### 🔗 Relevant API Flow

```
POST /api/Payment/create-payment-intent
```

Payload includes:

```json
{
  "amount": 5000,
  "currency": "usd",
  "userId": "user_001",
  "transaction_type": "platform_subscription",
  "metadata": {
    "planType": "pro"
  }
}
```

---

## ✅ 2. Investor Funds an App Creator

| Attribute             | Description                                |
| --------------------- | ------------------------------------------ |
| **transaction\_type** | `investor_funding`                         |
| **Payer**             | Investor                                   |
| **Receivers**         | App Creator (96.5%) + Mozaiks (3.5%)       |
| **Purpose**           | Capital support for app development        |
| **Mozaiks Fee**       | 3.5%                                       |
| **Provider Flow**     | One-time connected payment with split |

### 🔗 Relevant API Flow

```
POST /api/Payment/create-payment-intent
```

Payload:

```json
{
  "amount": 100000,
  "currency": "usd",
  "userId": "investor_001",
  "transaction_type": "investor_funding",
  "metadata": {
    "destinationAccountId": "acct_abc123",
    "appId": "app_456"
  }
}
```

---

## ✅ 3. End-user Purchases or Subscribes to a Private App

| Attribute             | Description                                     |
| --------------------- | ----------------------------------------------- |
| **transaction\_type** | `private_app_purchase`                          |
| **Payer**             | End-user                                        |
| **Receivers**         | App Creator (96.5%) + Mozaiks (3.5%)            |
| **Purpose**           | Access to private app features or subscriptions |
| **Mozaiks Fee**       | 3.5%                                            |
| **Provider Flow**     | Connected split                                 |

### 🔗 Relevant API Flow

```
POST /api/Payment/create-payment-intent
```

Payload:

```json
{
  "amount": 2000,
  "currency": "usd",
  "userId": "user_123",
  "transaction_type": "private_app_purchase",
  "metadata": {
    "appId": "app_private_789",
    "destinationAccountId": "acct_def456"
  }
}
```

---

## ✅ 4. End-user Purchases or Subscribes to a Public App

| Attribute             | Description                                      |
| --------------------- | ------------------------------------------------ |
| **transaction\_type** | `public_app_purchase`                            |
| **Payer**             | End-user                                         |
| **Receivers**         | App Creator + Investors (96.5%) + Mozaiks (3.5%) |
| **Purpose**           | Access to public app features and services       |
| **Mozaiks Fee**       | 3.5%                                             |
| **Provider Flow**     | Connected split (multiple destinations)          |

### 🔗 Relevant API Flow

```
POST /api/Payment/create-payment-intent
```

Payload:

```json
{
  "amount": 3000,
  "currency": "usd",
  "userId": "user_456",
  "transaction_type": "public_app_purchase",
  "metadata": {
    "appId": "app_public_999",
    "creatorAccountId": "acct_xyz123",
    "investorSplitInfo": [
      { "investorId": "inv_01", "percentage": 10 },
      { "investorId": "inv_02", "percentage": 20 }
    ]
  }
}
```

---

## 🧩 Additional Payment API Endpoints

| Endpoint                                        | Method | Description                                                             |
| ----------------------------------------------- | ------ | ----------------------------------------------------------------------- |
| `/api/Payment/create-payment-intent`            | `POST` | Creates a payment intent with routing logic based on `transaction_type` |
| `/api/Payment/payment-confirmed/{clientSecret}` | `POST` | Confirms a completed payment via the payment provider                   |
| `/api/Payment/payment-status/{paymentIntentId}` | `GET`  | Retrieves current status of a payment                                   |
| `/api/Payment/{paymentIntentId}/refund`         | `POST` | Processes a refund                                                      |
| `/api/Transaction/fail`                         | `POST` | Handles failed transactions                                             |

---

## 🔐 Authentication

All endpoints require bearer token authentication:

```http
Authorization: Bearer {access_token}
```


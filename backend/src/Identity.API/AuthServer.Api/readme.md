# 📘 Moz Server API Documentation (v1)

This documentation provides a detailed overview of the Moz Server's modular API endpoints structured under various domains like `Auth`, `Apps`, `Invite`, `Permissions`, `Roles`, `User`, `Wallet`, and `SubscriptionPlan`. This is powered by an OpenAPI 3.0.4 spec.

---

## 🔐 Auth Module (Deprecated)

End-user authentication is handled by Microsoft Entra External ID (CIAM). This service no longer exposes sign-in/sign-up endpoints or mints user JWTs.

---

## 🧩 Apps Module

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/apps` | GET | Lists apps for the current user (owned + member). |
| `/api/apps/public` | GET | Retrieves all public apps. |
| `/api/apps/{appId}` | GET | Gets app details by ID. |
| `/api/apps` | POST | Creates a new app (`multipart/form-data`). |
| `/api/apps/{appId}` | PATCH | Updates app config and/or visibility. |
| `/api/apps/{appId}/feature-flags` | PATCH | Toggles a feature flag (SuperAdmin). |

---

## 📩 Invite Module

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/Invite/SendInvite` | POST | Sends an invite to join an app. |
| `/api/Invite/GetInviteById/{id}` | GET | Fetches an invite by ID. |
| `/api/Invite/GetSentInvites/{userId}` | GET | Gets invites sent by user. |
| `/api/Invite/GetReceivedInvites/{userId}` | GET | Gets invites received by user. |
| `/api/Invite/UpdateInvite/{id}/{status}` | PUT | Updates invite status (accepted/rejected). |

---

## 🔐 Permissions Module

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/Permissions/CreatePermission` | POST | Creates a new permission entry. |
| `/api/Permissions/GetPermissionsByAppId/{appId}` | GET | Permissions scoped to an app. |
| `/api/Permissions/GetAllPermissions` | GET | Lists all permissions globally. |
| `/api/Permissions/GetPermissions/{id}` | GET | Fetches a permission by ID. |
| `/api/Permissions/GetPermissionsByRoleId/{roleId}` | GET | Gets permissions associated with a role. |
| `/api/Permissions/UpdatePermission/{id}` | PUT | Updates permission entry. |
| `/api/Permissions/DeletePermission/{id}` | DELETE | Deletes permission. |

---

## 🧑‍🤝‍🧑 Roles Module

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/Roles/CreateRole` | POST | Creates a new role. |
| `/api/Roles/GetRoleById/{id}` | GET | Gets role by ID. |
| `/api/Roles/GetRoleByAppId/{appId}` | GET | Gets roles for a specific app. |
| `/api/Roles/UpdateRole/{id}` | PUT | Updates an existing role. |
| `/api/Roles/DeleteRole/{id}` | DELETE | Deletes a role. |
| `/api/Roles/HasPermission/{userId}/{permission}/{appId}` | GET | Checks if user has permission in an app. |
| `/api/Roles/AddPermissions/{roleName}/{newPermissions}` | POST | Adds permissions to a role. |

---

## 💳 Subscription Plan Module

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/SubscriptionPlan/add` | POST | Adds a new subscription plan. |
| `/api/SubscriptionPlan/update/{id}` | PUT | Updates subscription plan by ID. |
| `/api/SubscriptionPlan/category/{category}` | GET | Lists plans by category. |
| `/api/SubscriptionPlan/all` | GET | Lists all subscription plans. |
| `/api/SubscriptionPlan/{id}` | GET | Gets subscription plan by ID. |
| `/api/SubscriptionPlan/user-subscription` | POST | Creates a user subscription. |
| `/api/SubscriptionPlan/assign` | POST | Assigns a plan to a user. |
| `/api/SubscriptionPlan/remove/{userId}` | DELETE | Removes a user's subscription. |

---

## 👤 User Module

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/User/GetUserById/{id}` | GET | Fetches a user by ID. |
| `/api/User/GetUserByEmail/{email}` | GET | Fetches user using email. |
| `/api/User/GetAllUsersAdmin` | GET | Admin access to all users. |
| `/api/User/UpdateUser/{id}` | PUT | Updates user information. |
| `/api/User/RevokeUser/{id}` | PATCH | Revokes user access. |

---

## 🧑 Me Module

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/me/dashboard` | GET | Returns apps + investor positions + investments for the current user. |

---

## 💼 Wallet Module

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/Wallet/create/{userId}/{appId}` | POST | Creates or fetches the user wallet for an app. |
| `/api/Wallet/{walletId}/balance` | GET | Gets wallet balance. |
| `/api/Wallet/{walletId}/transactions` | GET | Gets wallet transaction history. |
| `/api/Wallet/{walletId}/refund` | POST | Creates a refund entry in the wallet ledger. |

## 🔐 Security
All routes are secured with Bearer Token Authentication (OIDC access tokens):

```http
Authorization: Bearer {access_token}
```

---

Let me know if you want this exported to PDF, HTML, or GitHub Pages.

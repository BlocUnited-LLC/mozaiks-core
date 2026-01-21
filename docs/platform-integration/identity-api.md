# 🔐 Identity API Specification

> For **mozaiks-platform** connectors to call MozaiksCore's Identity.API.

---

## 📍 Base URL

```
Production: https://core.mozaiks.com
Development: http://localhost:8080
```

---

## 🔑 Authentication

All requests require a valid JWT token or internal API key:

```http
Authorization: Bearer {user_jwt}
# OR
X-Mozaiks-App-Key: {app_api_key}
```

---

## 📡 Endpoints

### Get Current User

```http
GET /api/auth/me
Authorization: Bearer {token}
```

**Response:**
```json
{
    "user_id": "abc123",
    "username": "john_doe",
    "email": "john@example.com",
    "roles": ["user", "admin"],
    "profile": {
        "display_name": "John Doe",
        "avatar_url": "https://...",
        "bio": "..."
    },
    "subscription": {
        "tier": "premium",
        "expires_at": "2026-01-01T00:00:00Z"
    },
    "created_at": "2025-01-01T00:00:00Z"
}
```

---

### Get User by ID

```http
GET /api/users/{user_id}
X-Mozaiks-App-Key: {app_key}
```

**Response:**
```json
{
    "user_id": "abc123",
    "username": "john_doe",
    "email": "john@example.com",
    "roles": ["user"],
    "created_at": "2025-01-01T00:00:00Z",
    "last_login": "2025-10-15T08:00:00Z"
}
```

---

### List Users (Admin)

```http
GET /api/admin/users
X-Mozaiks-App-Key: {app_key}
```

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `skip` | int | Pagination offset |
| `limit` | int | Max results (default: 50) |
| `role` | string | Filter by role |
| `search` | string | Search by username/email |

**Response:**
```json
{
    "users": [
        {
            "user_id": "abc123",
            "username": "john_doe",
            "email": "john@example.com",
            "roles": ["user"],
            "created_at": "2025-01-01T00:00:00Z"
        }
    ],
    "total": 150,
    "skip": 0,
    "limit": 50
}
```

---

### Get User Roles

```http
GET /api/users/{user_id}/roles
X-Mozaiks-App-Key: {app_key}
```

**Response:**
```json
{
    "user_id": "abc123",
    "roles": ["user", "admin"],
    "permissions": [
        "read:users",
        "write:users",
        "manage:plugins"
    ]
}
```

---

### Update User Roles

```http
PUT /api/users/{user_id}/roles
X-Mozaiks-App-Key: {app_key}
Content-Type: application/json

{
    "roles": ["user", "admin", "moderator"]
}
```

**Response:**
```json
{
    "success": true,
    "user_id": "abc123",
    "roles": ["user", "admin", "moderator"]
}
```

---

### Check Permission

```http
GET /api/users/{user_id}/permissions/{permission}
X-Mozaiks-App-Key: {app_key}
```

**Response:**
```json
{
    "user_id": "abc123",
    "permission": "manage:plugins",
    "allowed": true
}
```

---

### Batch Check Permissions

```http
POST /api/users/{user_id}/permissions/check
X-Mozaiks-App-Key: {app_key}
Content-Type: application/json

{
    "permissions": ["read:users", "write:users", "delete:users"]
}
```

**Response:**
```json
{
    "user_id": "abc123",
    "results": {
        "read:users": true,
        "write:users": true,
        "delete:users": false
    }
}
```

---

## 🐍 Python Connector

```python
# runtime/connectors/identity.py
from __future__ import annotations
from dataclasses import dataclass
from typing import Protocol, Any
from .base import PlatformHttpClient


@dataclass
class User:
    user_id: str
    username: str
    email: str
    roles: list[str]
    profile: dict[str, Any] | None = None
    subscription: dict[str, Any] | None = None
    created_at: str | None = None
    last_login: str | None = None


@dataclass
class UserRoles:
    user_id: str
    roles: list[str]
    permissions: list[str]


class IdentityConnector(Protocol):
    """Identity API connector interface."""
    
    async def get_current_user(
        self, *, user_jwt: str
    ) -> User: ...
    
    async def get_user(
        self, user_id: str, *, correlation_id: str = ""
    ) -> User: ...
    
    async def list_users(
        self,
        *,
        skip: int = 0,
        limit: int = 50,
        role: str | None = None,
        search: str | None = None,
        correlation_id: str = ""
    ) -> tuple[list[User], int]: ...
    
    async def get_user_roles(
        self, user_id: str, *, correlation_id: str = ""
    ) -> UserRoles: ...
    
    async def update_user_roles(
        self, user_id: str, roles: list[str], *, correlation_id: str = ""
    ) -> bool: ...
    
    async def check_permission(
        self, user_id: str, permission: str, *, correlation_id: str = ""
    ) -> bool: ...


class ManagedIdentityConnector:
    """Calls MozaiksCore Identity.API."""
    
    def __init__(self, http: PlatformHttpClient):
        self._http = http
    
    async def get_current_user(self, *, user_jwt: str) -> User:
        response = await self._http.get(
            "/api/auth/me",
            user_jwt=user_jwt
        )
        return User(**response)
    
    async def get_user(
        self, user_id: str, *, correlation_id: str = ""
    ) -> User:
        response = await self._http.get(
            f"/api/users/{user_id}",
            correlation_id=correlation_id
        )
        return User(**response)
    
    async def list_users(
        self,
        *,
        skip: int = 0,
        limit: int = 50,
        role: str | None = None,
        search: str | None = None,
        correlation_id: str = ""
    ) -> tuple[list[User], int]:
        params = {"skip": skip, "limit": limit}
        if role:
            params["role"] = role
        if search:
            params["search"] = search
        
        response = await self._http.get(
            "/api/admin/users",
            params=params,
            correlation_id=correlation_id
        )
        users = [User(**u) for u in response["users"]]
        return users, response["total"]
    
    async def get_user_roles(
        self, user_id: str, *, correlation_id: str = ""
    ) -> UserRoles:
        response = await self._http.get(
            f"/api/users/{user_id}/roles",
            correlation_id=correlation_id
        )
        return UserRoles(**response)
    
    async def update_user_roles(
        self, user_id: str, roles: list[str], *, correlation_id: str = ""
    ) -> bool:
        response = await self._http.put(
            f"/api/users/{user_id}/roles",
            json_body={"roles": roles},
            correlation_id=correlation_id
        )
        return response.get("success", False)
    
    async def check_permission(
        self, user_id: str, permission: str, *, correlation_id: str = ""
    ) -> bool:
        response = await self._http.get(
            f"/api/users/{user_id}/permissions/{permission}",
            correlation_id=correlation_id
        )
        return response.get("allowed", False)


class MockIdentityConnector:
    """Mock connector for self-hosted mode."""
    
    async def get_current_user(self, *, user_jwt: str) -> User:
        return User(
            user_id="mock_user",
            username="mock_user",
            email="mock@example.com",
            roles=["user", "admin"]
        )
    
    async def get_user(
        self, user_id: str, *, correlation_id: str = ""
    ) -> User:
        return User(
            user_id=user_id,
            username=f"user_{user_id}",
            email=f"{user_id}@example.com",
            roles=["user"]
        )
    
    async def list_users(
        self, *, skip: int = 0, limit: int = 50, **kwargs
    ) -> tuple[list[User], int]:
        return [], 0
    
    async def get_user_roles(
        self, user_id: str, *, correlation_id: str = ""
    ) -> UserRoles:
        return UserRoles(
            user_id=user_id,
            roles=["user"],
            permissions=["read:self"]
        )
    
    async def update_user_roles(
        self, user_id: str, roles: list[str], *, correlation_id: str = ""
    ) -> bool:
        return True
    
    async def check_permission(
        self, user_id: str, permission: str, *, correlation_id: str = ""
    ) -> bool:
        return True  # Allow all in mock mode
```

---

## 🔗 Related

- 📖 [Platform Integration Overview](./overview.md)
- 📖 [Billing API Spec](./billing-api.md)
- 🔐 [Core Authentication](../core/authentication.md)

# API Reference

MozaiksCore exposes the following endpoints.

## Authentication

### `POST /auth/login`

Authenticate a user and receive a JWT token.

**Request:**
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "user": {
    "id": "user_123",
    "email": "user@example.com"
  }
}
```

## Users

### `GET /users/me`

Get the current authenticated user.

**Headers:**
```
Authorization: Bearer <token>
```

**Response:**
```json
{
  "id": "user_123",
  "email": "user@example.com",
  "created_at": "2026-01-01T00:00:00Z"
}
```

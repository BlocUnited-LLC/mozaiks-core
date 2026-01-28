# backend/core/routes/admin_users.py
"""
Admin surface for user management.
Routes under /__mozaiks/admin/users guarded by X-Mozaiks-App-Admin-Key.
"""

import logging
import math
import re
from datetime import datetime, timezone
from typing import Optional, List, Literal

from bson import ObjectId
from fastapi import APIRouter, Depends, Header, HTTPException, Query, status
from pydantic import BaseModel, Field

from mozaiks_infra.config.database import users_collection
from mozaiks_infra.config.settings import settings

logger = logging.getLogger("mozaiks_core.admin_users")

router = APIRouter(prefix="/__mozaiks/admin/users", tags=["admin-users"])


# ---------------------------------------------------------------------------
# Auth: X-Mozaiks-App-Admin-Key header guard
# ---------------------------------------------------------------------------

def _get_admin_key() -> str | None:
    """Get the expected admin key from settings/env."""
    # Check for dedicated admin key first, fall back to internal API key
    import os
    admin_key = os.getenv("MOZAIKS_APP_ADMIN_KEY") or settings.internal_api_key
    return admin_key


def _safe_compare(a: str, b: str) -> bool:
    """Constant-time comparison to prevent timing attacks."""
    import hmac
    return hmac.compare_digest(a.encode(), b.encode())


async def require_admin_key(
    x_mozaiks_app_admin_key: Optional[str] = Header(None, alias="X-Mozaiks-App-Admin-Key")
) -> bool:
    """Dependency that validates the admin key header."""
    expected_key = _get_admin_key()
    
    if not expected_key:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Admin API not configured (no MOZAIKS_APP_ADMIN_KEY or INTERNAL_API_KEY set)"
        )
    
    if not x_mozaiks_app_admin_key:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing X-Mozaiks-App-Admin-Key header"
        )
    
    if not _safe_compare(expected_key, x_mozaiks_app_admin_key):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Invalid admin key"
        )
    
    return True


# ---------------------------------------------------------------------------
# DTOs / Models
# ---------------------------------------------------------------------------

class UserItem(BaseModel):
    """Stable user record shape for admin responses."""
    id: str
    username: str
    email: Optional[str] = None
    disabled: bool = False
    createdAt: Optional[str] = None
    lastLoginAt: Optional[str] = None
    
    class Config:
        extra = "ignore"


class UserListResponse(BaseModel):
    """Paginated list response."""
    items: List[UserItem]
    page: int
    limit: int
    total: int
    pages: int


class ActionRequest(BaseModel):
    """Request body for user actions."""
    action: Literal["suspendUser", "unsuspendUser", "resetPassword"]
    targetIds: List[str] = Field(..., min_length=1, max_length=100)
    params: Optional[dict] = None


class ActionResponse(BaseModel):
    """Response for action execution."""
    success: bool
    affected: int
    message: str
    errors: Optional[List[dict]] = None


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _serialize_user(user: dict) -> UserItem:
    """Convert MongoDB user document to stable UserItem shape."""
    # Handle datetime fields - convert to ISO string
    created_at = user.get("created_at") or user.get("createdAt")
    if isinstance(created_at, datetime):
        created_at = created_at.isoformat()
    
    last_login = user.get("last_login") or user.get("lastLoginAt") or user.get("last_login_at")
    if isinstance(last_login, datetime):
        last_login = last_login.isoformat()
    
    return UserItem(
        id=str(user["_id"]),
        username=user.get("username", ""),
        email=user.get("email"),
        disabled=bool(user.get("disabled", False)),
        createdAt=created_at,
        lastLoginAt=last_login
    )


def _validate_object_id(id_str: str) -> ObjectId:
    """Validate and convert string to ObjectId."""
    try:
        return ObjectId(id_str)
    except Exception:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Invalid user ID format: {id_str}"
        )


# ---------------------------------------------------------------------------
# Routes
# ---------------------------------------------------------------------------

@router.get("", response_model=UserListResponse)
async def list_users(
    _: bool = Depends(require_admin_key),
    page: int = Query(1, ge=1, description="Page number (1-indexed)"),
    limit: int = Query(20, ge=1, le=100, description="Items per page"),
    q: Optional[str] = Query(None, description="Search query (username or email)"),
    disabled: Optional[str] = Query(None, description="Filter by disabled status (true/false)")
):
    """
    List users with pagination, search, and filtering.
    
    Query params:
    - page: Page number (1-indexed)
    - limit: Items per page (max 100)
    - q: Search query (searches username and email)
    - disabled: Filter by disabled status ("true" or "false")
    """
    # Build query
    query = {}
    
    # Search filter
    if q and q.strip():
        search_regex = {"$regex": re.escape(q.strip()), "$options": "i"}
        query["$or"] = [
            {"username": search_regex},
            {"email": search_regex}
        ]
    
    # Disabled filter
    if disabled is not None and disabled.lower() in ("true", "false"):
        query["disabled"] = disabled.lower() == "true"
    
    # Get total count
    total = await users_collection.count_documents(query)
    
    # Calculate pagination
    skip = (page - 1) * limit
    pages = max(1, math.ceil(total / limit))
    
    # Fetch users
    cursor = users_collection.find(query).sort("created_at", -1).skip(skip).limit(limit)
    users = await cursor.to_list(length=limit)
    
    # Serialize
    items = [_serialize_user(user) for user in users]
    
    return UserListResponse(
        items=items,
        page=page,
        limit=limit,
        total=total,
        pages=pages
    )


@router.get("/{user_id}", response_model=UserItem)
async def get_user(
    user_id: str,
    _: bool = Depends(require_admin_key)
):
    """
    Get a single user by ID.
    """
    oid = _validate_object_id(user_id)
    
    user = await users_collection.find_one({"_id": oid})
    
    if not user:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="User not found"
        )
    
    return _serialize_user(user)


@router.post("/action", response_model=ActionResponse)
async def execute_action(
    request: ActionRequest,
    _: bool = Depends(require_admin_key)
):
    """
    Execute an action on one or more users.
    
    Supported actions:
    - suspendUser: Set disabled=true
    - unsuspendUser: Set disabled=false
    - resetPassword: Reset user's password (requires params.newPassword)
    """
    action = request.action
    target_ids = request.targetIds
    params = request.params or {}
    
    # Validate all IDs upfront
    oids = []
    for tid in target_ids:
        try:
            oids.append(ObjectId(tid))
        except Exception:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Invalid user ID format: {tid}"
            )
    
    affected = 0
    errors = []
    
    if action == "suspendUser":
        result = await users_collection.update_many(
            {"_id": {"$in": oids}},
            {
                "$set": {
                    "disabled": True,
                    "updated_at": datetime.now(tz=timezone.utc).replace(tzinfo=None).isoformat()
                }
            }
        )
        affected = result.modified_count
        message = f"Suspended {affected} user(s)"
        
    elif action == "unsuspendUser":
        result = await users_collection.update_many(
            {"_id": {"$in": oids}},
            {
                "$set": {
                    "disabled": False,
                    "updated_at": datetime.now(tz=timezone.utc).replace(tzinfo=None).isoformat()
                }
            }
        )
        affected = result.modified_count
        message = f"Unsuspended {affected} user(s)"
        
    elif action == "resetPassword":
        new_password = params.get("newPassword")
        
        if not new_password:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="resetPassword action requires params.newPassword"
            )
        
        if len(new_password) < 8:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Password must be at least 8 characters"
            )
        
        # Import password hashing from auth module
        from passlib.context import CryptContext
        pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")
        hashed_password = pwd_context.hash(new_password)
        
        result = await users_collection.update_many(
            {"_id": {"$in": oids}},
            {
                "$set": {
                    "hashed_password": hashed_password,
                    "updated_at": datetime.now(tz=timezone.utc).replace(tzinfo=None).isoformat()
                }
            }
        )
        affected = result.modified_count
        message = f"Reset password for {affected} user(s)"
        
    else:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Unknown action: {action}"
        )
    
    logger.info(f"Admin action '{action}' executed on {affected} users")
    
    return ActionResponse(
        success=True,
        affected=affected,
        message=message,
        errors=errors if errors else None
    )


@router.get("/schema", response_model=dict)
async def get_schema(
    _: bool = Depends(require_admin_key)
):
    """
    Get the schema for the users admin module.
    Used by schema-driven admin UIs to render the interface.
    """
    return {
        "module": "users",
        "displayName": "Users",
        "description": "Manage application users",
        "listColumns": [
            {"field": "username", "label": "Username", "sortable": True},
            {"field": "email", "label": "Email", "sortable": True},
            {"field": "createdAt", "label": "Created", "sortable": True, "type": "datetime"},
            {"field": "lastLoginAt", "label": "Last Login", "sortable": True, "type": "datetime"},
            {"field": "disabled", "label": "Status", "type": "boolean", "trueLabel": "Suspended", "falseLabel": "Active"}
        ],
        "actions": [
            {
                "id": "suspendUser",
                "label": "Suspend User",
                "icon": "ban",
                "confirmMessage": "Are you sure you want to suspend this user?",
                "appliesWhen": {"field": "disabled", "equals": False},
                "bulk": True
            },
            {
                "id": "unsuspendUser",
                "label": "Unsuspend User",
                "icon": "check",
                "confirmMessage": "Are you sure you want to unsuspend this user?",
                "appliesWhen": {"field": "disabled", "equals": True},
                "bulk": True
            },
            {
                "id": "resetPassword",
                "label": "Reset Password",
                "icon": "key",
                "requiresInput": {
                    "fields": [
                        {"name": "newPassword", "type": "password", "label": "New Password", "required": True, "minLength": 8}
                    ]
                },
                "bulk": False
            }
        ],
        "filters": [
            {
                "field": "disabled",
                "label": "Status",
                "type": "select",
                "options": [
                    {"value": "", "label": "All"},
                    {"value": "false", "label": "Active"},
                    {"value": "true", "label": "Suspended"}
                ]
            }
        ],
        "searchable": True,
        "searchPlaceholder": "Search by username or email..."
    }

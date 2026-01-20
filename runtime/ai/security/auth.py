# backend/security/auth.py

import logging
import os
from datetime import datetime, timedelta
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, status

from core.config.database import users_collection
from core.subscription_manager import subscription_manager
from jose import jwt
from passlib.context import CryptContext
from pydantic import BaseModel

from security.constants import ACCESS_TOKEN_EXPIRE_MINUTES, ALGORITHM, SECRET_KEY
# Import get_current_user from authentication.py
from .authentication import get_current_user

router = APIRouter()
logger = logging.getLogger("mozaiks_core.auth")


def _auth_mode() -> str:
    raw = (os.getenv("MOZAIKS_AUTH_MODE") or "external").strip().lower()
    if raw in {"external", "platform", "local"}:
        return raw
    if raw == "oidc":
        return "external"
    if raw == "jwt":
        return "local"
    return "external"


async def get_user(username: str):
    """Get user from MongoDB database"""
    return await users_collection.find_one({"username": username})


def _create_access_token(data: dict, expires_delta: Optional[timedelta] = None) -> str:
    payload = data.copy()
    expire = datetime.utcnow() + (expires_delta or timedelta(minutes=ACCESS_TOKEN_EXPIRE_MINUTES))
    payload.update({"exp": expire})
    algorithm = (os.getenv("JWT_ALGORITHM") or ALGORITHM).strip() or ALGORITHM
    return jwt.encode(payload, SECRET_KEY, algorithm=algorithm)


def _create_app_token(current_user: dict) -> tuple[str, int]:
    """
    Mint a short-lived, app-scoped token after validating an external/platform token.

    This is optional and only used when MOZAIKS_TOKEN_EXCHANGE=true (or when clients opt in).
    """
    try:
        expires_minutes = int((os.getenv("MOZAIKS_APP_TOKEN_EXPIRE_MINUTES") or "").strip() or "15")
    except Exception:
        expires_minutes = 15

    roles_claim = (
        (os.getenv("MOZAIKS_ROLES_CLAIM") or os.getenv("MOZAIKS_PLATFORM_ROLES_CLAIM") or "roles").strip() or "roles"
    )
    superadmin_claim = (os.getenv("MOZAIKS_SUPERADMIN_CLAIM") or "").strip()

    payload: dict = {
        "sub": current_user.get("username"),
        "user_id": current_user.get("user_id"),
        "mozaiks_token_use": "app",
    }

    roles = current_user.get("roles") or []
    if roles:
        payload[roles_claim] = roles

    if superadmin_claim:
        payload[superadmin_claim] = bool(current_user.get("is_superadmin"))

    app_id = (os.getenv("MOZAIKS_APP_ID") or "").strip()
    if app_id:
        payload["aud"] = app_id

    token = _create_access_token(payload, expires_delta=timedelta(minutes=expires_minutes))
    return token, expires_minutes * 60


if _auth_mode() == "local":
    from fastapi.security import OAuth2PasswordRequestForm

    pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")

    class UserRegister(BaseModel):
        username: str
        password: str
        email: str
        full_name: str

    def _verify_password(plain_password: str, hashed_password: str) -> bool:
        return pwd_context.verify(plain_password, hashed_password)

    async def _authenticate_user(username: str, password: str):
        user = await get_user(username)
        if not user:
            return None
        if not _verify_password(password, user.get("hashed_password", "")):
            return None
        return user

    @router.post("/token")
    async def login_for_access_token(form_data: OAuth2PasswordRequestForm = Depends()):
        user = await _authenticate_user(form_data.username, form_data.password)
        if not user:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Incorrect username or password",
                headers={"WWW-Authenticate": "Bearer"},
            )

        access_token_expires = timedelta(minutes=ACCESS_TOKEN_EXPIRE_MINUTES)
        access_token = _create_access_token(
            data={"sub": user["username"], "user_id": str(user["_id"])},
            expires_delta=access_token_expires,
        )

        now = datetime.utcnow().isoformat()
        try:
            await users_collection.update_one({"_id": user["_id"]}, {"$set": {"last_login": now, "last_active": now}})
        except Exception:
            pass

        return {"access_token": access_token, "token_type": "bearer", "user_id": str(user["_id"]), "username": user["username"]}

    @router.post("/register")
    async def register_user(user_data: UserRegister):
        existing_user = await users_collection.find_one({"username": user_data.username})
        if existing_user:
            raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Username already registered")

        existing_email = await users_collection.find_one({"email": user_data.email})
        if existing_email:
            raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Email already registered")

        hashed_password = pwd_context.hash(user_data.password)
        now = datetime.utcnow().isoformat()
        new_user = {
            "username": user_data.username,
            "full_name": user_data.full_name,
            "email": user_data.email,
            "hashed_password": hashed_password,
            "disabled": False,
            "created_at": now,
            "updated_at": now,
            "last_login": now,
            "last_active": now,
        }

        result = await users_collection.insert_one(new_user)
        user_id = str(result.inserted_id)

        try:
            await subscription_manager.start_user_trial(user_id)
        except Exception:
            pass

        access_token_expires = timedelta(minutes=ACCESS_TOKEN_EXPIRE_MINUTES)
        access_token = _create_access_token(
            data={"sub": user_data.username, "user_id": user_id},
            expires_delta=access_token_expires,
        )

        return {"access_token": access_token, "token_type": "bearer", "user_id": user_id, "username": user_data.username}


@router.get("/me")
async def get_current_user_info(current_user: dict = Depends(get_current_user)):
    user_data = await get_user(current_user["username"])
    if not user_data:
        raise HTTPException(status_code=404, detail="User not found")
    
    # Return user info without sensitive data
    return {
        "username": user_data["username"],
        "email": user_data["email"],
        "full_name": user_data["full_name"],
        "user_id": str(user_data["_id"])
    }

@router.get("/validate-token")
async def validate_token(current_user: dict = Depends(get_current_user)):
    """Endpoint to validate if a token is still valid"""
    return {
        "valid": True,
        "user_id": current_user["user_id"],
        "username": current_user["username"]
    }


@router.post("/token-exchange")
async def token_exchange(current_user: dict = Depends(get_current_user)):
    """
    Exchange an external/platform JWT for a short-lived app-scoped token.

    - Clients call this after OIDC login if MOZAIKS_TOKEN_EXCHANGE=true.
    - When token exchange is enabled, app API calls should use the returned token.
    """
    if _auth_mode() == "local":
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Token exchange is not available in local mode")

    access_token, expires_in = _create_app_token(current_user)
    return {
        "access_token": access_token,
        "token_type": "bearer",
        "expires_in": expires_in,
        "user_id": current_user["user_id"],
        "username": current_user["username"],
    }

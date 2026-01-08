# backend/core/routes/events.py
import logging
from datetime import datetime
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, Field

from security.authentication import get_current_active_user
from core.analytics import raw_events

logger = logging.getLogger("mozaiks_core.routes.events")
router = APIRouter()


class _BaseEvent(BaseModel):
    appId: Optional[str] = None
    timestamp: Optional[datetime] = None


class UserSignedUpIn(_BaseEvent):
    userId: str = Field(min_length=1)


class UserActiveIn(_BaseEvent):
    userId: str = Field(min_length=1)


@router.post("/user/signed-up")
async def user_signed_up(payload: UserSignedUpIn, current_user: dict = Depends(get_current_active_user)):
    if payload.userId != current_user.get("user_id"):
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Invalid userId")
    try:
        await raw_events.append_user_signed_up(
            user_id=payload.userId,
            app_id=payload.appId,
            timestamp=payload.timestamp,
        )
        return {"ok": True}
    except Exception as e:
        logger.error(f"Failed to append UserSignedUp: {e}")
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail="Failed to write event")


@router.post("/user/active")
async def user_active(payload: UserActiveIn, current_user: dict = Depends(get_current_active_user)):
    if payload.userId != current_user.get("user_id"):
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Invalid userId")
    try:
        await raw_events.append_user_active(
            user_id=payload.userId,
            app_id=payload.appId,
            timestamp=payload.timestamp,
        )
        return {"ok": True}
    except Exception as e:
        logger.error(f"Failed to append UserActive: {e}")
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail="Failed to write event")

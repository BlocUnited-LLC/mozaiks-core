# backend/app/connectors/mock.py
from __future__ import annotations

import json
import logging
import uuid

from mozaiks_app.connectors.base import (
    CancelRequest,
    CheckoutRequest,
    CheckoutResponse,
    OkResponse,
    PaymentConnector,
    PaymentStatus,
    SubscriptionScope,
)

logger = logging.getLogger("mozaiks_sdk.connectors.mock")


def _log_json(level: int, payload: dict) -> None:
    logger.log(level, json.dumps(payload, separators=(",", ":"), default=str))


class MockPaymentConnector(PaymentConnector):
    async def checkout(
        self,
        *,
        payload: CheckoutRequest,
        correlation_id: str,
        user_jwt: str | None = None,
    ) -> CheckoutResponse:
        session_id = str(uuid.uuid4())
        _log_json(
            logging.INFO,
            {
                "event": "payment.checkout",
                "mode": "self_hosted",
                "correlationId": correlation_id,
                "scope": payload.scope,
                "appId": payload.appId,
                "sessionId": session_id,
            },
        )
        return CheckoutResponse(checkoutUrl="self_hosted_mode", sessionId=session_id)

    async def subscription_status(
        self,
        *,
        scope: SubscriptionScope,
        appId: str | None,
        correlation_id: str,
        user_jwt: str | None = None,
    ) -> PaymentStatus:
        _log_json(
            logging.INFO,
            {
                "event": "payment.subscription_status",
                "mode": "self_hosted",
                "correlationId": correlation_id,
                "scope": scope,
                "appId": appId,
            },
        )
        return PaymentStatus(
            scope=scope,
            appId=appId,
            active=True,
            status="active",
            plan="self_hosted",
        )

    async def cancel(
        self,
        *,
        payload: CancelRequest,
        correlation_id: str,
        user_jwt: str | None = None,
    ) -> OkResponse:
        _log_json(
            logging.INFO,
            {
                "event": "payment.cancel",
                "mode": "self_hosted",
                "correlationId": correlation_id,
                "scope": payload.scope,
                "appId": payload.appId,
            },
        )
        return OkResponse(ok=True)

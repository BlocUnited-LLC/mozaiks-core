# backend/security/constants.py

import os
import secrets
import logging
from dotenv import load_dotenv

load_dotenv()  # Ensure environment variables are loaded

logger = logging.getLogger("mozaiks_core.security")

# Use the environment variable; generate ephemeral secret for dev if not set
_jwt_secret = os.getenv("JWT_SECRET") or os.getenv("JWT_SECRET_KEY") or ""
if not _jwt_secret:
    # Development/test-friendly fallback: ephemeral secret (NOT for production).
    _jwt_secret = secrets.token_urlsafe(48)
    logger.warning("JWT_SECRET not set; generated an ephemeral dev secret (set JWT_SECRET for stable tokens)")

SECRET_KEY = _jwt_secret
ALGORITHM = "HS256"
ACCESS_TOKEN_EXPIRE_MINUTES = 60 * 24  # 1 day

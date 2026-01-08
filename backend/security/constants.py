# backend/security/constants.py

import os
from dotenv import load_dotenv

load_dotenv()  # Ensure environment variables are loaded

# Use the environment variable with a fallback for development
SECRET_KEY = os.getenv("JWT_SECRET", "supersecretkey")
ALGORITHM = "HS256"
ACCESS_TOKEN_EXPIRE_MINUTES = 60 * 24  # 1 day

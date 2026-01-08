# backend/core/state_manager.py
import threading
import logging
import time

logger = logging.getLogger("mozaiks_core.state_manager")

class StateManager:
    def __init__(self):
        self.state = {}
        self._lock = threading.Lock()

    def set(self, key, value, expire_in=None):
        """
        Stores a key-value pair in memory.
        Optionally, set an expiration time in seconds.
        """
        with self._lock:
            self.state[key] = {"value": value, "expires_at": time.time() + expire_in if expire_in else None}
            logger.info(f"🔹 State updated: '{key}' set to '{value}' (Expires in: {expire_in} sec)")

    def get(self, key):
        """
        Retrieves a value from memory.
        If the value has expired, it is deleted and None is returned.
        """
        with self._lock:
            entry = self.state.get(key)
            if not entry:
                return None

            # If expired, remove from state and return None
            if entry["expires_at"] and time.time() > entry["expires_at"]:
                del self.state[key]
                logger.info(f"⚠️ Expired state key removed: '{key}'")
                return None

            return entry["value"]

    def delete(self, key):
        """
        Removes a key-value pair from the state.
        """
        with self._lock:
            if key in self.state:
                del self.state[key]
                logger.info(f"🗑️ State key removed: '{key}'")

    def clear(self):
        """
        Clears all stored state data.
        """
        with self._lock:
            self.state.clear()
            logger.info("🧹 All state cleared.")

state_manager = StateManager()
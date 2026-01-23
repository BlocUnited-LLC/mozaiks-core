"""
Plugin SDK - Simple database access for plugins.

Usage in plugins:
    from sdk import get_collection
    
    async def execute(data: dict) -> dict:
        user_id = data["user_id"]
        
        coll = get_collection("my_items")
        items = await coll.find({"user_id": user_id}).to_list(100)
        
        await coll.insert_one({"user_id": user_id, "title": "My Item"})
"""

import os
from typing import Optional

from bson import ObjectId
from motor.motor_asyncio import AsyncIOMotorClient, AsyncIOMotorCollection, AsyncIOMotorDatabase

# Module-level client and database (lazy initialized)
_client: Optional[AsyncIOMotorClient] = None
_db: Optional[AsyncIOMotorDatabase] = None


def _get_client() -> AsyncIOMotorClient:
    """Get or create MongoDB client."""
    global _client
    if _client is None:
        uri = os.getenv("MONGODB_URI", "mongodb://mongodb:27017")
        _client = AsyncIOMotorClient(uri)
    return _client


def _get_db() -> AsyncIOMotorDatabase:
    """Get the plugins database."""
    global _db
    if _db is None:
        db_name = os.getenv("PLUGINS_DB_NAME", "MozaiksPlugins")
        _db = _get_client()[db_name]
    return _db


def get_collection(name: str) -> AsyncIOMotorCollection:
    """
    Get a MongoDB collection.
    
    Args:
        name: Collection name
        
    Returns:
        AsyncIOMotorCollection for direct MongoDB operations
        
    Example:
        async def execute(data: dict) -> dict:
            tasks = get_collection("tasks")
            items = await tasks.find({"user_id": data["user_id"]}).to_list(100)
    """
    return _get_db()[name]


def get_db() -> AsyncIOMotorDatabase:
    """
    Get the raw database instance.
    
    Useful for advanced operations like transactions or aggregations.
    """
    return _get_db()


# Re-export ObjectId for convenience
__all__ = [
    "get_collection",
    "get_db",
    "ObjectId",
]
]

"""
Plugin SDK - Provides multi-tenant safe database access for plugins.

Usage in plugins:
    from sdk import get_collection
    
    async def execute(data: dict) -> dict:
        user_id = data["user_id"]
        app_id = data["app_id"]
        
        # Scoped collection (auto-filters by app_id from context)
        coll = get_collection("my_items", data)
        items = await coll.find({"user_id": user_id}).to_list(100)
        
        # Insert (app_id automatically added)
        await coll.insert_one({"user_id": user_id, "title": "My Item"})
"""

import os
from typing import Any, Dict, List, Optional

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


class ScopedCollection:
    """
    A collection wrapper that auto-scopes queries by app_id.
    
    This ensures multi-tenant isolation without plugins needing to remember
    to add app_id to every query.
    """
    
    def __init__(self, collection: AsyncIOMotorCollection, app_id: str):
        self._collection = collection
        self._app_id = app_id
    
    def _scope_filter(self, filter_dict: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Add app_id to any filter."""
        base = {"app_id": self._app_id}
        if filter_dict:
            base.update(filter_dict)
        return base
    
    def _scope_document(self, document: Dict[str, Any]) -> Dict[str, Any]:
        """Add app_id to a document being inserted."""
        document["app_id"] = self._app_id
        return document
    
    async def find(self, filter_dict: Optional[Dict[str, Any]] = None, **kwargs):
        """
        Find documents scoped to this app.
        
        Returns a cursor - use .to_list(length) to get results.
        
        Example:
            items = await coll.find({"status": "active"}).to_list(100)
        """
        return self._collection.find(self._scope_filter(filter_dict), **kwargs)
    
    async def find_one(self, filter_dict: Optional[Dict[str, Any]] = None, **kwargs) -> Optional[Dict[str, Any]]:
        """Find one document scoped to this app."""
        return await self._collection.find_one(self._scope_filter(filter_dict), **kwargs)
    
    async def insert_one(self, document: Dict[str, Any], **kwargs):
        """Insert document with app_id automatically added."""
        return await self._collection.insert_one(self._scope_document(document.copy()), **kwargs)
    
    async def insert_many(self, documents: List[Dict[str, Any]], **kwargs):
        """Insert documents with app_id automatically added."""
        scoped_docs = [self._scope_document(doc.copy()) for doc in documents]
        return await self._collection.insert_many(scoped_docs, **kwargs)
    
    async def update_one(self, filter_dict: Dict[str, Any], update: Dict[str, Any], **kwargs):
        """Update one document scoped to this app."""
        return await self._collection.update_one(
            self._scope_filter(filter_dict), update, **kwargs
        )
    
    async def update_many(self, filter_dict: Dict[str, Any], update: Dict[str, Any], **kwargs):
        """Update many documents scoped to this app."""
        return await self._collection.update_many(
            self._scope_filter(filter_dict), update, **kwargs
        )
    
    async def delete_one(self, filter_dict: Dict[str, Any], **kwargs):
        """Delete one document scoped to this app."""
        return await self._collection.delete_one(self._scope_filter(filter_dict), **kwargs)
    
    async def delete_many(self, filter_dict: Dict[str, Any], **kwargs):
        """Delete many documents scoped to this app."""
        return await self._collection.delete_many(self._scope_filter(filter_dict), **kwargs)
    
    async def count_documents(self, filter_dict: Optional[Dict[str, Any]] = None, **kwargs) -> int:
        """Count documents scoped to this app."""
        return await self._collection.count_documents(self._scope_filter(filter_dict), **kwargs)


def get_collection(name: str, context: Dict[str, Any]) -> ScopedCollection:
    """
    Get a collection that's automatically scoped to the current app.
    
    Args:
        name: Collection name
        context: The data dict passed to execute() containing app_id
        
    Returns:
        ScopedCollection that auto-filters by app_id
        
    Example:
        async def execute(data: dict) -> dict:
            tasks = get_collection("tasks", data)
            items = await (await tasks.find({"user_id": data["user_id"]})).to_list(100)
    """
    app_id = context.get("app_id")
    if not app_id:
        raise ValueError("app_id not found in context - this should never happen")
    
    collection = _get_db()[name]
    return ScopedCollection(collection, app_id)


def get_raw_collection(name: str) -> AsyncIOMotorCollection:
    """
    Get a raw collection without app_id scoping.
    
    WARNING: Only use this for cross-app admin operations.
    Most plugins should use get_collection() instead.
    """
    return _get_db()[name]


def get_db() -> AsyncIOMotorDatabase:
    """
    Get the raw database instance.
    
    WARNING: This bypasses app_id scoping. Use get_collection() for normal plugin operations.
    """
    return _get_db()


# Re-export ObjectId for convenience
__all__ = [
    "get_collection",
    "get_raw_collection", 
    "get_db",
    "ScopedCollection",
    "ObjectId",
]

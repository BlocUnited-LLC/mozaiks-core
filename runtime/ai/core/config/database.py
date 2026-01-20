# backend/core/config/database.py
import os
import logging
import asyncio
from datetime import datetime
from motor.motor_asyncio import AsyncIOMotorClient
from dotenv import load_dotenv
import functools
import time

# Load environment variables from .env
load_dotenv()

# Configure logger
logger = logging.getLogger("mozaiks_core.database")
logging.basicConfig(level=logging.INFO)

# Constants for connection management
MAX_POOL_SIZE = 100  # Maximum number of connections in the pool
MIN_POOL_SIZE = 10   # Minimum number of connections to maintain in the pool
MAX_IDLE_TIME_MS = 60000  # Maximum time a connection can remain idle before being closed
CONNECTION_TIMEOUT_MS = 5000  # Connection timeout in milliseconds
SERVER_SELECTION_TIMEOUT_MS = 5000  # Server selection timeout in milliseconds
HEARTBEAT_FREQUENCY_MS = 10000  # How often to check the connection is alive

# Load MongoDB URI from the environment (with a fallback)
MONGO_URI = os.getenv("DATABASE_URI", "mongodb://localhost:27017/mozaiks")

# Initialize the Motor async client with optimized connection settings
mongo_client = AsyncIOMotorClient(
    MONGO_URI,
    maxPoolSize=MAX_POOL_SIZE,
    minPoolSize=MIN_POOL_SIZE,
    maxIdleTimeMS=MAX_IDLE_TIME_MS,
    connectTimeoutMS=CONNECTION_TIMEOUT_MS,
    serverSelectionTimeoutMS=SERVER_SELECTION_TIMEOUT_MS,
    heartbeatFrequencyMS=HEARTBEAT_FREQUENCY_MS,
    retryWrites=True,  # Automatically retry certain write operations
    w="majority"  # Write concern - wait for acknowledgment from majority of instances
)

# For enterprise data, always use the MozaiksDB database.
db_enterprise = mongo_client["MozaiksDB"]
enterprises_collection = db_enterprise["Enterprises"]

# For all other collections, choose the database based on ENV
if os.getenv("ENV") == "production":
    db = mongo_client["client"]
else:
    db = mongo_client["MozaiksCore"]

# Track connection status
_is_connected = False
_last_connection_check = 0
_connection_check_interval = 60  # Check connection every 60 seconds at most

# Verify the MongoDB connection asynchronously
async def verify_connection(force=False):
    """
    Verify connection to MongoDB. Will cache connection status for 60 seconds 
    unless force=True is specified.
    """
    global _is_connected, _last_connection_check
    
    # Use cached connection status if available and not forced to recheck
    current_time = time.time()
    if not force and _is_connected and (current_time - _last_connection_check) < _connection_check_interval:
        return True
    
    try:
        await mongo_client.server_info()  # Verify connection
        logger.info("✅ Successfully connected to MongoDB")
        _is_connected = True
        _last_connection_check = current_time
        return True
    except Exception as e:
        _is_connected = False
        logger.error(f"❌ MongoDB Connection Error: {e}")
        raise e

# Retry decorator for database operations
def with_retry(max_retries=3, delay=1):
    """
    Decorator to retry database operations with exponential backoff.
    
    Args:
        max_retries (int): Maximum number of retry attempts
        delay (int): Initial delay between retries in seconds, doubles each retry
    """
    def decorator(func):
        @functools.wraps(func)
        async def wrapper(*args, **kwargs):
            retries = 0
            current_delay = delay
            
            while True:
                try:
                    return await func(*args, **kwargs)
                except Exception as e:
                    retries += 1
                    if retries > max_retries:
                        logger.error(f"Maximum retries ({max_retries}) exceeded for {func.__name__}: {e}")
                        raise
                    
                    logger.warning(f"Retry {retries}/{max_retries} for {func.__name__} after error: {e}")
                    await asyncio.sleep(current_delay)
                    current_delay *= 2  # Exponential backoff
                    
                    # Verify connection before retrying
                    await verify_connection(force=True)
        
        return wrapper
    return decorator

# If db is valid, initialize other collections
if db is not None:
    users_collection = db["users"]
    subscriptions_collection = db["subscriptions"]
    subscription_history_collection = db["subscription_history"]
    billing_history_collection = db["billing_history"]
    
    # Add the settings collection
    settings_collection = db["settings"]
    
    # Initialize indexes for settings collection
    @with_retry(max_retries=5, delay=2)
    async def create_settings_indexes():
        try:
            # Create compound index on user_id + plugin_name for faster lookups
            await settings_collection.create_index([("user_id", 1), ("plugin_name", 1)])
            logger.info("✅ Created index on user_id and plugin_name in settings collection")
            
            # Add TTL index for temporary settings if needed
            # await settings_collection.create_index([("expires_at", 1)], expireAfterSeconds=0)
            # logger.info("✅ Created TTL index on expires_at in settings collection")
        except Exception as e:
            logger.error(f"❌ Error creating settings indexes: {e}")
            raise
else:
    users_collection = None
    subscriptions_collection = None
    subscription_history_collection = None
    billing_history_collection = None
    settings_collection = None

# Async functions to initialize enterprise data
@with_retry(max_retries=5, delay=1)
async def create_enterprise_index():
    """
    Create a NON-UNIQUE index on 'AdminId' so we can query by it quickly,
    but allow duplicates/nulls if needed.
    """
    try:
        # If you had a unique index before, you can drop it:
        # await enterprises_collection.drop_index("AdminId_1")

        await enterprises_collection.create_index("AdminId")  # non-unique
        logger.info("✅ Created non-unique index on 'AdminId' in Enterprises collection")
    except Exception as e:
        logger.error(f"❌ Error creating index on 'AdminId': {e}")
        raise

@with_retry(max_retries=5, delay=1)
async def ensure_enterprise_exists():
    """
    Check if a document with AdminId from the .env file exists in the collection.
    If not, create it. Otherwise, log that it exists.
    """
    AdminId = os.getenv("AdminId")
    if not AdminId:
        logger.error("❌ AdminId not set in .env file.")
    else:
        logger.info(f"Using AdminId: {AdminId}")
        enterprise = await enterprises_collection.find_one({"AdminId": AdminId})
        if not enterprise:
            new_enterprise = {
                "AdminId": AdminId,
                "name": os.getenv("EnterpriseName", "Default Enterprise"),
                "created_at": datetime.utcnow().isoformat()
            }
            await enterprises_collection.insert_one(new_enterprise)
            logger.info(f"✅ Created enterprise with id {AdminId}")
        else:
            logger.info(f"Enterprise with id {AdminId} already exists.")

# Add initialization function for all indexes
async def initialize_database():
    """Initialize all database collections and indexes"""
    await verify_connection()
    await create_enterprise_index()
    await ensure_enterprise_exists()
    
    # Only create settings indexes if settings_collection is available
    if settings_collection is not None:
        await create_settings_indexes()

# Cache for frequently accessed database lookups
class DBCache:
    def __init__(self, max_size=1000, ttl=300):
        self.cache = {}
        self.max_size = max_size
        self.ttl = ttl  # Time to live in seconds
    
    def get(self, key):
        if key in self.cache:
            value, timestamp = self.cache[key]
            if time.time() - timestamp <= self.ttl:
                return value
            else:
                # Expired, remove from cache
                del self.cache[key]
        return None
    
    def set(self, key, value):
        # Enforce cache size limit - remove oldest items if needed
        if len(self.cache) >= self.max_size:
            # Sort by timestamp and remove oldest
            oldest_keys = sorted(self.cache.keys(), key=lambda k: self.cache[k][1])[:len(self.cache) // 10]
            for old_key in oldest_keys:
                del self.cache[old_key]
        
        self.cache[key] = (value, time.time())
    
    def invalidate(self, key):
        if key in self.cache:
            del self.cache[key]
    
    def clear(self):
        self.cache.clear()

# Create global cache instance
db_cache = DBCache()

# Helper function to create a document ID key for caching
def make_cache_key(collection_name, document_id):
    return f"{collection_name}:{document_id}"

# Helper to get document with caching
async def get_cached_document(collection, query, cache_key=None):
    """
    Get a document with caching support
    """
    if cache_key:
        cached = db_cache.get(cache_key)
        if cached:
            return cached
    
    document = await collection.find_one(query)
    
    if cache_key and document:
        db_cache.set(cache_key, document)
    
    return document

# Helper to update a document and invalidate cache
async def update_and_invalidate(collection, query, update, cache_key=None):
    """
    Update a document and invalidate cache if needed
    """
    result = await collection.update_one(query, update)
    
    if cache_key:
        db_cache.invalidate(cache_key)
    
    return result
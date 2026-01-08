# Orchestration Module

## Overview
The Orchestration module serves as the central hub of the Mozaiks platform. It handles startup, configuration loading, and integration of core services—ensuring smooth communication between components (database, plugins, subscription logic, etc.). This module establishes the foundation upon which all other modules operate.

## Core Responsibilities
- Configuration loading and environment setup
- Database connection management with robust retry logic
- Core services initialization and integration
- API routing and middleware management
- WebSocket integration
- Event bus and state management
- Caching and performance optimization
- Error handling and logging
- Subscription service detection and initialization
- Plugin discovery and registration

## Dependencies

### Internal Dependencies
- **Plugins**: Loads and manages plugins via `plugin_manager`
- **Auth**: Integrates authentication routes via `auth_router`
- **Subscriptions**: Initializes either `subscription_manager` or `subscription_stub`
- **Notifications**: Integrates notification routes
- **Settings**: Provides access to settings configuration
- **WebSocket**: Establishes WebSocket connections

### External Dependencies
- **fastapi**: Primary web framework
- **uvicorn**: ASGI server
- **motor**: Async MongoDB driver
- **dotenv**: Environment variable management
- **httpx**: HTTP client for external API calls
- **logging**: Structured logging
- **asyncio**: Asynchronous programming

## API Reference

### Core API Endpoints

#### `GET /api/app-config`
Returns application configuration including monetization status.
- **Returns**: Object with monetization status, app name, version and environment

#### `GET /api/navigation`
Returns navigation items based on user permissions and installed plugins.
- **Parameters**:
  - Requires authentication
- **Returns**: Navigation object with menu items

#### `GET /api/theme-config`
Returns theme configuration.
- **Returns**: Theme configuration object

#### `GET /api/settings-config`
Returns settings configuration with visibility based on subscription.
- **Parameters**:
  - Requires authentication
- **Returns**: Settings configuration object

#### `GET /api/user-profile`
Returns user profile information.
- **Parameters**:
  - Requires authentication
- **Returns**: User profile object

#### `POST /api/update-profile`
Updates user profile information.
- **Parameters**:
  - Requires authentication
  - Profile data in request body
- **Returns**: Success message

#### `GET /api/available-plugins`
Returns list of plugins available to the user.
- **Parameters**:
  - Requires authentication
- **Returns**: Array of plugin objects

#### `GET /api/check-plugin-access/{plugin_name}`
Checks if user has access to a specific plugin.
- **Parameters**:
  - `plugin_name`: Name of plugin to check
  - Requires authentication
- **Returns**: Access status

#### `POST /api/execute/{plugin_name}`
Executes a plugin with provided data.
- **Parameters**:
  - `plugin_name`: Name of plugin to execute
  - Request body: Data for plugin execution
  - Requires authentication
- **Returns**: Plugin execution result

### Helper Functions

#### `load_config(filename)`
Loads a JSON configuration file with caching.
- **Parameters**:
  - `filename` (str): Configuration file to load
- **Returns**: Configuration object
- **Raises**: HTTPException on failure

#### `ensure_plugins_up_to_date()`
Ensures plugins are refreshed periodically.
- **Returns**: None

#### `create_default_admin()`
Creates a default admin user if none exists.
- **Returns**: None

## Configuration

### Environment Variables
Located in `.env` file or environment:

```
DATABASE_URI=mongodb+srv://username:password@server/database
SUBSCRIPTION_API_URL=https://your-subscription-service.com
JWT_SECRET=your_secret_key
ENV=development|production
PORT=8000
AdminId=admin_user_id
EnterpriseName=Your_Company_Name
RUNTIME_BASE_URL=http://localhost:8090
MOZAIKS_RUNTIME_UI_BASE_URL=http://localhost:8090
MOZAIKS_CHATUI_URL_TEMPLATE={runtime_ui_base_url}/chat?app_id={app_id}&chat_id={chat_id}&token={token}
MONETIZATION=0|1
HOSTING_SERVICE=0|1
EMAIL_SERVICE_URL=http://email-service-url
EMAIL_SERVICE_API_KEY=your_email_api_key
```

### Configuration Files
- `/backend/core/config/navigation_config.json`: Navigation structure
- `/backend/core/config/theme_config.json`: Theme settings
- `/backend/core/config/subscription_config.json`: Subscription plans
- `/backend/core/config/settings_config.json`: Settings structure
- `/backend/core/config/plugin_registry.json`: Plugin registry

## Data Models

### State Management
```python
class StateManager:
    state = {}  # In-memory state store
    
    def set(key, value, expire_in=None)
    def get(key)
    def delete(key)
    def clear()
```

### Database Cache
```python
class DBCache:
    cache = {}
    max_size = 1000
    ttl = 300  # seconds
    
    def get(key)
    def set(key, value)
    def invalidate(key)
    def clear()
```

## Integration Points

### Event-Driven Architecture
The `event_bus` allows components to communicate asynchronously:

```python
# Publishing events
event_bus.publish("event_name", {"key": "value"})

# Subscribing to events
@on_event("event_name")
def handle_event(data):
    # Process event data
    pass
```

### Plugin Registration
Plugins are registered at startup and periodically refreshed:

```python
# In main.py
@app.on_event("startup")
async def startup_event():
    global plugin_manager
    plugin_manager = await plugin_manager.init_async()
    await register_websockets(app)
```

### Middleware Integration
Custom middleware for request logging, error handling, and more:

```python
@app.middleware("http")
async def log_requests(request: Request, call_next):
    # Middleware implementation
    pass
```

## Events

### Events Published
- `settings_updated`: When settings are updated
- `plugin_executed`: When a plugin is executed
- `plugin_execution_error`: When a plugin execution fails
- `api_error`: When an API error occurs
- `subscription_updated`: When a subscription is updated
- `subscription_canceled`: When a subscription is canceled
- `theme_changed`: When a theme is changed
- `notification_preferences_updated`: When notification preferences are updated
- `profile_updated`: When a profile is updated

### Events Subscribed To
The director itself subscribes to various events through the state manager and event bus.

## Startup Sequence

1. **Load environment variables**
2. **Initialize FastAPI app**
3. **Set up CORS middleware**
4. **Include API routers**:
   - Auth router
   - Notifications router
   - Custom routers
5. **Initialize subscription manager**:
   - Use real manager if MONETIZATION=1
   - Use stub if MONETIZATION=0
6. **Database initialization**:
   - Verify connection
   - Create indexes
   - Ensure enterprise exists
7. **Load plugins**:
   - Scan plugin directory
   - Update registry
   - Load enabled plugins
8. **Register WebSocket routes**:
   - Chat WebSocket
   - Notifications WebSocket
   - Plugin WebSockets

## Common Issues & Troubleshooting

### Database Connection Problems
- Check DATABASE_URI is correct
- Verify network connectivity
- Check database user permissions
- Inspect connection settings in database.py

### Plugin Loading Issues
- Check plugin directory structure
- Verify plugin_registry.json is valid
- Look for import errors in logs
- Check plugin dependencies

### Configuration Loading Errors
- Verify JSON files are valid
- Check file permissions
- Ensure environment variables are set

### Performance Problems
- Check caching settings
- Analyze slow requests in logs
- Verify database indexes are created
- Check connection pooling settings

## Related Files
- `/backend/main.py`: Main entry point
- `/backend/core/director.py`: Core orchestration logic
- `/backend/core/event_bus.py`: Event handling system
- `/backend/core/state_manager.py`: In-memory state and caching
- `/backend/core/config/database.py`: Database connection management
- `/backend/core/config/*.json`: Configuration files

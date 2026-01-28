# Plugins.API

Core service for managing the Mozaiks plugin ecosystem.

## Responsibilities

- **Plugin Catalog**: Browse and search available plugins
- **Plugin Manifests**: Serve runtime configuration for the frontend shell
- **Installation Management**: Install, uninstall, enable/disable plugins per app

## API Endpoints

### Catalog (Public)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/plugins/catalog` | Search plugins |
| GET | `/api/plugins/catalog/{id}` | Get plugin details |
| GET | `/api/plugins/catalog/by-name/{name}` | Get plugin by name |
| GET | `/api/plugins/catalog/categories` | List categories |

### Installations (Authenticated)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/plugins/apps/{appId}` | Get installed plugins |
| POST | `/api/plugins/apps/{appId}/install` | Install a plugin |
| DELETE | `/api/plugins/apps/{appId}/{pluginName}` | Uninstall plugin |
| PATCH | `/api/plugins/apps/{appId}/{pluginName}/toggle` | Enable/disable |
| PATCH | `/api/plugins/apps/{appId}/{pluginName}/settings` | Update settings |

### Manifests (Runtime)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/plugins/manifests/{appId}` | Get combined runtime manifest |

## Data Models

### Plugin

```json
{
  "name": "moz.app.blog",
  "displayName": "Blog",
  "description": "Add a blog to your app",
  "version": "1.0.0",
  "category": "Content",
  "manifest": {
    "frontendEntry": "@mozaiks/plugin-blog",
    "backendEntry": "plugins.blog.logic",
    "routes": [...],
    "navigation": [...],
    "widgets": [...]
  }
}
```

### PluginInstallation

```json
{
  "appId": "app_123",
  "pluginName": "moz.app.blog",
  "installedVersion": "1.0.0",
  "isEnabled": true,
  "settings": { ... }
}
```

## Running Locally

```bash
cd backend/src/Plugins.API
dotnet run
```

Swagger UI: http://localhost:5000/swagger

#!/usr/bin/env python
"""
Project Initialization CLI

Creates a new mozaiks project with proper structure.

Usage:
    python -m cli.main init <name>
    python -m cli.main init <name> --template minimal
    python -m cli.main init <name> --no-git

Contract Version: 1.0
"""

import os
import re
import subprocess
from pathlib import Path
from datetime import datetime


def to_snake_case(name: str) -> str:
    """Convert name to snake_case."""
    s1 = re.sub('(.)([A-Z][a-z]+)', r'\\1_\\2', name)
    s2 = re.sub('([a-z0-9])([A-Z])', r'\\1_\\2', s1)
    s3 = s2.replace('-', '_').replace(' ', '_')
    return s3.lower()


def to_title_case(name: str) -> str:
    """Convert to Title Case for display."""
    return ' '.join(word.capitalize() for word in name.replace('-', '_').split('_'))


def init_project(
    name: str,
    template: str = "minimal",
    no_git: bool = False,
    output_dir: str = None,
) -> int:
    """
    Initialize a new mozaiks project.

    Args:
        name: Project name
        template: Template to use (minimal)
        no_git: Skip git initialization
        output_dir: Output directory (default: current directory)

    Returns:
        0 on success, 1 on failure
    """
    project_name = to_snake_case(name)
    display_name = to_title_case(name)

    print(f"\n🚀 Initializing mozaiks project: {project_name}\n")

    # Determine project directory
    if output_dir:
        base_dir = Path(output_dir)
    else:
        base_dir = Path.cwd()

    project_dir = base_dir / project_name

    # Check if directory exists
    if project_dir.exists():
        print(f"❌ Directory '{project_dir}' already exists")
        return 1

    # Create project structure
    print(f"   [1/7] Creating project directory: {project_dir}")
    project_dir.mkdir(parents=True, exist_ok=True)

    # Create mozaiks.toml
    print(f"   [2/7] Creating mozaiks.toml")
    toml_content = generate_mozaiks_toml(project_name, display_name)
    (project_dir / "mozaiks.toml").write_text(toml_content)

    # Create docker-compose.yml
    print(f"   [3/7] Creating docker-compose.yml")
    docker_content = generate_docker_compose(project_name)
    (project_dir / "docker-compose.yml").write_text(docker_content)

    # Create .env.example
    print(f"   [4/7] Creating .env.example")
    env_content = generate_env_example(project_name)
    (project_dir / ".env.example").write_text(env_content)

    # Create .gitignore
    print(f"   [5/7] Creating .gitignore")
    gitignore_content = generate_gitignore()
    (project_dir / ".gitignore").write_text(gitignore_content)

    # Create runtime directories
    print(f"   [6/7] Creating runtime directories")
    runtime_ai = project_dir / "runtime" / "ai"
    runtime_ai.mkdir(parents=True, exist_ok=True)

    # Create subdirectories with .gitkeep
    (runtime_ai / "workflows").mkdir(exist_ok=True)
    (runtime_ai / "workflows" / ".gitkeep").write_text("")

    plugins_dir = project_dir / "runtime" / "ai" / "plugins"
    plugins_dir.mkdir(parents=True, exist_ok=True)
    (plugins_dir / ".gitkeep").write_text("")

    # Create README.md
    print(f"   [7/7] Creating README.md")
    readme_content = generate_readme(project_name, display_name)
    (project_dir / "README.md").write_text(readme_content)

    # Initialize git repository
    if not no_git:
        print(f"\n   Initializing git repository...")
        try:
            subprocess.run(
                ["git", "init"],
                cwd=project_dir,
                capture_output=True,
                check=True
            )
            print(f"   ✅ Git repository initialized")
        except subprocess.CalledProcessError:
            print(f"   ⚠️  Failed to initialize git (git may not be installed)")
        except FileNotFoundError:
            print(f"   ⚠️  Git not found, skipping repository initialization")

    # Success message
    print(f"\n{'=' * 55}")
    print(f"✨ Project '{project_name}' created!")
    print(f"{'=' * 55}")
    print(f"\nLocation: {project_dir}")
    print(f"\nProject structure:")
    print(f"  {project_name}/")
    print(f"  ├── mozaiks.toml          # Project configuration")
    print(f"  ├── docker-compose.yml    # Local MongoDB")
    print(f"  ├── .env.example          # Environment template")
    print(f"  ├── .gitignore")
    print(f"  ├── README.md")
    print(f"  └── runtime/")
    print(f"      ├── ai/workflows/     # Your AI workflows")
    print(f"      └── ai/plugins/       # Your backend plugins")

    print(f"\nNext steps:")
    print(f"  1. cd {project_name}")
    print(f"  2. cp .env.example .env")
    print(f"  3. docker-compose up -d")

    print("\nResources:")
    print("  - docs/guides/cli.md")
    print("  - docs/guides/creating-plugins.md")
    print("  - docs/guides/creating-workflows.md")

    return 0


def generate_mozaiks_toml(project_name: str, display_name: str) -> str:
    """Generate mozaiks.toml project configuration."""
    return f'''# Mozaiks Project Configuration
# ==============================
# See: docs/ai-runtime/configuration-reference.md (in mozaiks-core)

[project]
name = "{project_name}"
version = "0.1.0"
description = "{display_name} - Built with Mozaiks"

[runtime]
# Runtime configuration
port = 8080
host = "0.0.0.0"

# Authentication mode:
# - local: Built-in JWT auth (default for self-hosted)
# - external: External OIDC provider
# - platform: Mozaiks Platform managed
auth_mode = "local"

# Logging level
log_level = "INFO"

[database]
# MongoDB connection
# Override with DATABASE_URI environment variable
uri = "mongodb://localhost:27017/{project_name}"

[plugins]
# Plugin configuration
# enabled = ["*"]     # Enable all plugins (default)
# enabled = ["todo", "notes"]  # Enable specific plugins
# disabled = ["deprecated_plugin"]  # Disable specific plugins

[workflows]
# Workflow configuration
# enabled = ["*"]     # Enable all workflows (default)
# default_model = "gpt-4o-mini"  # Default model for workflows

[security]
# Security settings
# cors_origins = ["http://localhost:3000"]
# rate_limit = 100  # Requests per minute

[features]
# Feature flags (optional)
# entitlements = true  # Enable entitlement system
# analytics = false    # Disable analytics
'''


def generate_docker_compose(project_name: str) -> str:
    """Generate docker-compose.yml for local services."""
    return f'''# Docker Compose for {project_name}
# ================================
# Start services: docker-compose up -d
# Stop services: docker-compose down

version: "3.8"

services:
  # MongoDB database
  mongodb:
    image: mongo:7
    container_name: {project_name}_mongodb
    restart: unless-stopped
    ports:
      - "27017:27017"
    volumes:
      - mongodb_data:/data/db
    environment:
      # For development only - set proper credentials for production
      MONGO_INITDB_DATABASE: {project_name}
    healthcheck:
      test: echo 'db.runCommand("ping").ok' | mongosh localhost:27017/{project_name} --quiet
      interval: 10s
      timeout: 5s
      retries: 5

  # Optional: Mongo Express for database management
  # mongo-express:
  #   image: mongo-express
  #   container_name: {project_name}_mongo_express
  #   restart: unless-stopped
  #   ports:
  #     - "8081:8081"
  #   environment:
  #     ME_CONFIG_MONGODB_URL: mongodb://mongodb:27017/
  #   depends_on:
  #     - mongodb

volumes:
  mongodb_data:
    name: {project_name}_mongodb_data
'''


def generate_env_example(project_name: str) -> str:
    """Generate .env.example template."""
    return f'''# {project_name} Environment Configuration
# =========================================
# Copy this file to .env and update values
# NEVER commit .env to version control!

# =============================================================================
# REQUIRED
# =============================================================================

# JWT Secret for authentication (generate a secure random string)
# Generate with: python -c "import secrets; print(secrets.token_urlsafe(32))"
JWT_SECRET=your-jwt-secret-here-change-me

# =============================================================================
# DATABASE
# =============================================================================

# MongoDB connection string
DATABASE_URI=mongodb://localhost:27017/{project_name}

# =============================================================================
# AI / LLM (Optional - required for workflows)
# =============================================================================

# OpenAI API Key (for GPT models)
# OPENAI_API_KEY=sk-...

# Anthropic API Key (for Claude models)
# ANTHROPIC_API_KEY=sk-ant-...

# Azure OpenAI (alternative to OpenAI)
# AZURE_OPENAI_API_KEY=
# AZURE_OPENAI_ENDPOINT=
# AZURE_OPENAI_DEPLOYMENT_NAME=

# =============================================================================
# RUNTIME
# =============================================================================

# Server port
PORT=8080

# Environment (development, staging, production)
ENVIRONMENT=development

# Log level (DEBUG, INFO, WARNING, ERROR)
LOG_LEVEL=INFO

# =============================================================================
# SECURITY (Optional)
# =============================================================================

# CORS origins (comma-separated)
# CORS_ORIGINS=http://localhost:3000,http://localhost:5173

# =============================================================================
# FEATURES (Optional)
# =============================================================================

# Enable monetization/entitlements (true/false)
# MONETIZATION=false

# Enable analytics tracking
# ANALYTICS=false
'''


def generate_gitignore() -> str:
    """Generate .gitignore for mozaiks projects."""
    return '''# Mozaiks Project .gitignore
# ==========================

# Environment files
.env
.env.local
.env.*.local

# Python
__pycache__/
*.py[cod]
*$py.class
*.so
.Python
build/
develop-eggs/
dist/
downloads/
eggs/
.eggs/
lib/
lib64/
parts/
sdist/
var/
wheels/
*.egg-info/
.installed.cfg
*.egg
venv/
.venv/
ENV/

# Node.js (if using frontend)
node_modules/
npm-debug.log*
yarn-debug.log*
yarn-error.log*

# IDE
.idea/
.vscode/
*.swp
*.swo
*~

# OS
.DS_Store
Thumbs.db
ehthumbs.db

# Logs
logs/
*.log

# Database
*.db
*.sqlite

# Testing
.coverage
htmlcov/
.pytest_cache/
.tox/

# Build outputs
dist/
build/
*.egg-info/

# Docker
docker-compose.override.yml

# Secrets (never commit!)
*.pem
*.key
credentials.json
secrets.json
'''


def generate_readme(project_name: str, display_name: str) -> str:
    """Generate README.md for the project."""
    return f'''# {display_name}

Built with MozaiksCore - the open-source AI workflow runtime.

## Quick Start

### Prerequisites

- Python 3.10+
- Docker (for MongoDB)

### Setup

1. **Setup environment**

   ```bash
   cd {project_name}
   cp .env.example .env
   # Edit .env with your configuration
   ```

2. **Start MongoDB**

   ```bash
   docker-compose up -d
   ```

3. **Install dependencies**

   ```bash
   cd runtime/ai
   pip install -r requirements.txt
   ```

4. **Run the runtime**

   ```bash
   python main.py
   ```

## Creating Plugins & Workflows

```bash
cd runtime/ai
python -m cli.main new plugin my_plugin
python -m cli.main new workflow my_workflow
python -m cli.main doctor
```

## Documentation

- mozaiks-core docs (see the mozaiks-core repository)

## License

MIT
'''

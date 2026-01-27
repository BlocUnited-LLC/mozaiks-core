# Self-Hosting Guide

This guide explains how to run MozaiksCore as a standalone, self-hosted service **without** mozaiks-platform.

## Quick Start

```bash
# Clone the repo
git clone https://github.com/your-org/mozaiks-core.git
cd mozaiks-core/runtime/ai

# Create virtual environment
python -m venv .venv
source .venv/bin/activate  # or .venv\Scripts\activate on Windows

# Install dependencies
pip install -r requirements.txt

# Copy example configs
cp .env.example .env
cp entitlements.yaml.example entitlements.yaml

# Run
uvicorn core.director:app --host 0.0.0.0 --port 8000
```

## No Platform Required

MozaiksCore is designed to work 100% standalone. You do NOT need:
- mozaiks-platform account
- Stripe integration
- Any external billing service

By default, everything is **unlimited**:
- ✅ Unlimited tokens
- ✅ All features enabled
- ✅ No rate limits

## Configuration

### Option 1: Defaults (Everything Unlimited)

Just run Core. No config needed. All features enabled, no limits.

### Option 2: Local Entitlements File

Create `entitlements.yaml` to define your own limits:

```yaml
# entitlements.yaml
defaults:
  tier: "self-hosted"
  
  token_budget:
    limit: 1000000      # 1M tokens per month
    period: "monthly"
    enforcement: "soft"  # warn but don't block
  
  features:
    workflow_execution: true
    multi_agent: true
    code_execution: true
    function_calling: true
    vision: true
  
  rate_limits:
    requests_per_minute: 60
    concurrent_workflows: 5

# Per-app overrides (optional)
apps:
  production-app:
    token_budget:
      limit: 5000000    # Higher limit for production
    rate_limits:
      concurrent_workflows: 20
```

### Option 3: Environment Variables

For simple deployments:

```bash
# .env
MOZAIKS_DEFAULT_TIER=self-hosted
MOZAIKS_DEFAULT_TOKEN_LIMIT=1000000
MOZAIKS_DEFAULT_ENFORCEMENT=soft
```

## Directory Structure

```
runtime/ai/
├── .env                    # Environment config
├── entitlements.yaml       # Entitlement config (optional)
├── core/
│   ├── billing/
│   │   ├── entitlements.py # Local entitlement management
│   │   └── ...
│   └── ...
└── ...
```

## Using Entitlements in Your Code

```python
from core.billing import get_entitlements

# Get entitlements for an app
ent = get_entitlements("my-app-id")

# Check features
if ent.has_feature("code_execution"):
    # Allow code execution
    pass

# Check token budget
allowed, msg = ent.check_token_budget(tokens_needed=500)
if not allowed:
    raise QuotaExceeded(msg)

# Record usage
ent.consume_tokens(500)
```

## Migration to Platform (Optional)

If you later want to use mozaiks-platform for:
- Managed billing (Stripe integration)
- Usage analytics dashboard
- Multi-tenant management

Simply add these environment variables:

```bash
# .env
MOZAIKS_PLATFORM_URL=https://api.mozaiks.ai
MOZAIKS_PLATFORM_API_KEY=sk_c2p_live_your_key_here
```

Core will then:
- Accept entitlement syncs from Platform
- Report usage events to Platform

Your local `entitlements.yaml` becomes a fallback if Platform is unreachable.

## Docker Deployment

```dockerfile
FROM python:3.11-slim

WORKDIR /app
COPY runtime/ai /app

RUN pip install -r requirements.txt

# Optional: Add your entitlements config
COPY entitlements.yaml /app/entitlements.yaml

EXPOSE 8000
CMD ["uvicorn", "core.director:app", "--host", "0.0.0.0", "--port", "8000"]
```

```yaml
# docker-compose.yml
version: '3.8'
services:
  mozaiks-core:
    build: .
    ports:
      - "8000:8000"
    volumes:
      - ./entitlements.yaml:/app/entitlements.yaml
    environment:
      - OPENAI_API_KEY=${OPENAI_API_KEY}
```

## FAQ

### Do I need Stripe for self-hosting?
No. Stripe integration is only in mozaiks-platform. Core has no payment code.

### Can I use Core commercially?
Yes. Core is open source. See LICENSE for details.

### What's the 3% platform fee?
Only applies if you use mozaiks-platform hosting. Self-hosting = no fee.

### Can I migrate from self-hosted to Platform later?
Yes. Just add the Platform API key. Your apps keep working.

### How do I get support?
- GitHub Issues: [link]
- Community Discord: [link]
- Commercial support: contact@mozaiks.ai

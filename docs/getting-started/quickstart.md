# 🚀 Quickstart Guide

> Get MozaiksCore running in under 5 minutes.

---

## 📋 Prerequisites

| Requirement | Version |
|-------------|---------|
| Python | 3.11+ |
| Node.js | 18+ |
| MongoDB | 6.0+ (or Atlas) |

---

## ⚡ Quick Setup

### 1️⃣ Clone & Install

```bash
# Clone the repo
git clone https://github.com/your-org/mozaiks-core.git
cd mozaiks-core

# Backend setup
cd runtime/ai
python -m venv venv
venv\Scripts\activate  # Windows
# source venv/bin/activate  # macOS/Linux
pip install -r requirements.txt

# Frontend setup
cd ../packages/shell
npm install
```

### 2️⃣ Configure Environment

Create `runtime/ai/.env`:

```env
# Database
MONGODB_URI=mongodb://localhost:27017/mozaikscore
# OR for Atlas:
# MONGODB_URI=mongodb+srv://user:pass@cluster.mongodb.net/mozaikscore

# Auth
JWT_SECRET=your-super-secret-jwt-key-change-in-production
AUTH_ENABLED=true

# Mode
ENV=development
MOZAIKS_MANAGED=false
```

### 3️⃣ Start Services

**Terminal 1 - Backend:**
```bash
cd runtime/ai
venv\Scripts\activate
uvicorn main:app --reload --port 8000
```

**Terminal 2 - Frontend:**
```bash
cd runtime/packages/shell
npm start
```

### 4️⃣ Access App

Open: **http://localhost:3000**

---

## 🔐 First Login

### Development Mode (No Keycloak)

With `AUTH_ENABLED=false`, you'll get mock authentication:

```javascript
// Auto-login as test user
{
  user_id: "dev_user_001",
  username: "developer",
  roles: ["user", "admin"]
}
```

### Production Mode (Keycloak)

1. Set up Keycloak realm
2. Configure in `.env`:
```env
AUTH_ENABLED=true
MOZAIKS_OIDC_AUTHORITY=https://your-keycloak.com/realms/mozaiks
AUTH_AUDIENCE=api://mozaiks-auth
```

---

## 📁 Project Structure

```
mozaiks-core/
├── runtime/
│   ├── ai/                # AI Runtime (FastAPI)
│   │   ├── main.py        # Entry point
│   │   ├── core/          # Core systems
│   │   ├── plugins/       # Backend plugins
│   │   └── workflows/     # AI workflows
│   ├── plugin-host/       # Plugin execution host (optional)
│   └── packages/
│       └── shell/         # React frontend
│           └── src/
│               ├── core/      # Core components
│               ├── plugins/   # Frontend plugins (create as needed)
│               └── chat/      # ChatUI
└── docs/                  # This documentation
```

---

## ✅ Verify Installation

### Check Backend

```bash
curl http://localhost:8000/health
```

Expected:
```json
{"status": "healthy", "version": "1.0.0"}
```

### Check Frontend

Navigate to http://localhost:5173 and verify:
- [ ] Login page loads
- [ ] Navigation sidebar renders
- [ ] No console errors

---

## 🧰 Optional CLI Helpers

Mozaiks-core includes a small CLI for scaffolding and diagnostics.

```bash
cd runtime/ai
python -m cli.main doctor
python -m cli.main db --check-db
python -m cli.main new plugin todo
```

📚 Full CLI guide: [CLI Guide](../guides/cli.md)

---

## 🎯 Next Steps

| Goal | Guide |
|------|-------|
| Create your first plugin | [Creating Plugins](../guides/creating-plugins.md) |
| Add AI capabilities | [Creating Workflows](../guides/creating-workflows.md) |
| Deploy to production | [Deployment Guide](../guides/deployment.md) |
| Troubleshoot issues | [Troubleshooting](../guides/troubleshooting.md) |
| Use the CLI | [CLI Guide](../guides/cli.md) |

---

## 🔗 Related

- 🏗️ [Core Architecture](../core/architecture.md)
- 🔌 [Plugin System](../core/plugins.md)
- 🤖 [AI Runtime](../ai-runtime/architecture.md)

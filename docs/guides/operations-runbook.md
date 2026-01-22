# Production Operations Runbook

## 🚀 Deployment

### Initial Setup

1. **Configure DNS** (one-time):
   ```bash
   # Add CNAME record:
   docs.mozaiks.ai → blocunited-llc.github.io
   ```

2. **Enable GitHub Pages**:
   - Go to repo Settings → Pages
   - Source: GitHub Actions
   - Custom domain: `docs.mozaiks.ai`

3. **Add Repository Secrets** (Settings → Secrets → Actions):
   ```
   OPENAI_API_KEY=sk-...
   MONGODB_URI=mongodb+srv://...
   JWT_SECRET=<32+ char random string>
   ```

### Deploy New Release

```bash
# Tag release
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0

# Docker images auto-build and push to ghcr.io
# Verify at: https://github.com/orgs/BlocUnited-LLC/packages
```

### Rollback

```bash
# Find previous version
docker pull ghcr.io/blocunited-llc/mozaiks-core/backend:v0.9.0

# Deploy previous tag
kubectl set image deployment/backend backend=ghcr.io/blocunited-llc/mozaiks-core/backend:v0.9.0
```

---

## 📊 Monitoring

### Health Checks

```bash
# Backend health
curl https://api.mozaiks.ai/health

# Shell (frontend)
curl https://app.mozaiks.ai/

# Expected: 200 OK
```

### Metrics Endpoints

```bash
# Performance metrics
curl https://api.mozaiks.ai/metrics/perf/aggregate

# Per-chat metrics
curl https://api.mozaiks.ai/metrics/perf/chats
```

### Logs

```bash
# View backend logs (if using Docker)
docker logs -f mozaiks-backend --tail 100

# View nginx logs (shell)
docker logs -f mozaiks-shell --tail 50

# Filter for errors
docker logs mozaiks-backend 2>&1 | grep ERROR
```

---

## 🗄️ Database Operations

### Backup MongoDB

```bash
# Full backup
mongodump --uri="$MONGODB_URI" --out=/backups/$(date +%Y%m%d)

# Specific database
mongodump --uri="$MONGODB_URI" --db=MozaiksCore --out=/backups/core-$(date +%Y%m%d)
```

### Restore from Backup

```bash
# Restore full backup
mongorestore --uri="$MONGODB_URI" /backups/20260121/

# Restore specific collection
mongorestore --uri="$MONGODB_URI" --db=MozaiksCore --collection=users /backups/core-20260121/MozaiksCore/users.bson
```

### Database Migrations

```bash
# Run migration scripts (if using Alembic or similar)
cd runtime/ai
python -m alembic upgrade head

# Manual schema update (Python script)
python scripts/migrate_db.py --from=v1.0 --to=v1.1
```

---

## 🔥 Incident Response

### Backend Down

1. Check health endpoint:
   ```bash
   curl https://api.mozaiks.ai/health
   ```

2. View logs for errors:
   ```bash
   docker logs mozaiks-backend --tail 200 | grep -i error
   ```

3. Restart service:
   ```bash
   docker restart mozaiks-backend
   # or
   kubectl rollout restart deployment/backend
   ```

4. If DNS issue, check:
   ```bash
   nslookup api.mozaiks.ai
   ```

### Database Connection Issues

1. Test connection:
   ```bash
   mongosh "$MONGODB_URI" --eval "db.runCommand({ ping: 1 })"
   ```

2. Check IP whitelist (MongoDB Atlas):
   - Go to Network Access
   - Ensure server IP is whitelisted

3. Rotate credentials if compromised:
   ```bash
   # Update GitHub secret
   gh secret set MONGODB_URI --body "mongodb+srv://..."
   
   # Redeploy
   kubectl rollout restart deployment/backend
   ```

### High Memory/CPU

1. Check resource usage:
   ```bash
   docker stats mozaiks-backend
   ```

2. Scale horizontally:
   ```bash
   kubectl scale deployment/backend --replicas=3
   ```

3. Investigate memory leaks:
   ```bash
   # Profile Python memory
   py-spy top --pid $(pgrep -f "python.*main.py")
   ```

---

## 🔐 Security

### Rotate Secrets

```bash
# Generate new JWT secret
openssl rand -base64 32

# Update in GitHub secrets
gh secret set JWT_SECRET --body "<new-secret>"

# Redeploy
git push origin main
```

### Check for Vulnerabilities

```bash
# Python dependencies
cd runtime/ai
pip-audit

# npm dependencies
cd runtime/packages/shell
npm audit

# Docker image scan
docker scan ghcr.io/blocunited-llc/mozaiks-core/backend:latest
```

### Enable Secret Scanning

Go to repo Settings → Security → enable:
- [x] Secret scanning
- [x] Push protection

---

## 📈 Performance Tuning

### Database Indexes

```javascript
// Create indexes for common queries
db.users.createIndex({ "email": 1 }, { unique: true });
db.chat_sessions.createIndex({ "user_id": 1, "created_at": -1 });
db.notifications.createIndex({ "user_id": 1, "read": 1, "created_at": -1 });
```

### Redis Caching (Optional)

```bash
# Add Redis for session caching
docker run -d --name redis -p 6379:6379 redis:alpine

# Update backend env
REDIS_URL=redis://localhost:6379
```

### CDN for Static Assets

Configure CloudFlare or AWS CloudFront:
- Origin: `app.mozaiks.ai`
- Cache: static assets (`.js`, `.css`, `.svg`)
- TTL: 1 year for versioned assets

---

## 📞 Escalation

| Issue | Contact | SLA |
|-------|---------|-----|
| P0 - Complete outage | On-call engineer | 15 min |
| P1 - Critical bug | Dev team lead | 1 hour |
| P2 - Performance degradation | DevOps | 4 hours |
| P3 - Minor bug | Weekly triage | 1 week |

### On-Call Rotation

```bash
# Check current on-call
curl https://api.pagerduty.com/oncalls

# Page on-call
curl -X POST https://api.pagerduty.com/incidents \
  -H "Authorization: Token token=<token>" \
  -d '{"incident":{"type":"incident","title":"Backend down"}}'
```

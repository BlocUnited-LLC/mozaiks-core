# 🚢 Deployment Guide

> Deploy MozaiksCore to production environments.

---

## 🎯 Deployment Options

| Option | Best For |
|--------|----------|
| **Docker Compose** | Single server, small teams |
| **Kubernetes** | Scale, high availability |
| **Platform Hosting** | Managed infrastructure |

---

## 🐳 Docker Compose

### docker-compose.yml

```yaml
version: '3.8'

services:
  # MongoDB
  mongodb:
    image: mongo:6.0
    restart: unless-stopped
    volumes:
      - mongo_data:/data/db
    environment:
      MONGO_INITDB_ROOT_USERNAME: admin
      MONGO_INITDB_ROOT_PASSWORD: ${MONGO_PASSWORD}
    networks:
      - mozaiks

  # Backend API
  backend:
    build:
      context: ./runtime/backend
      dockerfile: Dockerfile
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - MONGODB_URI=mongodb://admin:${MONGO_PASSWORD}@mongodb:27017/mozaikscore?authSource=admin
      - JWT_SECRET=${JWT_SECRET}
      - ENV=production
      - AUTH_ENABLED=true
      - MOZAIKS_OIDC_AUTHORITY=${OIDC_AUTHORITY}
    depends_on:
      - mongodb
    networks:
      - mozaiks

  # Frontend
  frontend:
    build:
      context: .
      dockerfile: Dockerfile.frontend
    restart: unless-stopped
    ports:
      - "3000:80"
    environment:
      - VITE_API_URL=http://backend:8080
    depends_on:
      - backend
    networks:
      - mozaiks

  # Nginx Reverse Proxy
  nginx:
    image: nginx:alpine
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./certs:/etc/nginx/certs:ro
    depends_on:
      - frontend
      - backend
    networks:
      - mozaiks

volumes:
  mongo_data:

networks:
  mozaiks:
    driver: bridge
```

### Backend Dockerfile

```dockerfile
# runtime/backend/Dockerfile
FROM python:3.11-slim

WORKDIR /app

# Install dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy source
COPY . .

# Run
EXPOSE 8080
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8080"]
```

### Frontend Dockerfile

```dockerfile
# Dockerfile.frontend
FROM node:18-alpine AS builder

WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=builder /app/dist /usr/share/nginx/html
COPY nginx-frontend.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

### Deploy

```bash
# Create .env file
cat > .env << EOF
MONGO_PASSWORD=your_secure_password
JWT_SECRET=your_jwt_secret_min_32_chars
OIDC_AUTHORITY=https://your-keycloak.com/realms/mozaiks
EOF

# Start services
docker-compose up -d

# Check logs
docker-compose logs -f backend
```

---

## ☸️ Kubernetes

### Namespace & ConfigMap

```yaml
# k8s/namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: mozaiks
---
# k8s/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: mozaiks-config
  namespace: mozaiks
data:
  ENV: "production"
  AUTH_ENABLED: "true"
```

### Secrets

```yaml
# k8s/secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: mozaiks-secrets
  namespace: mozaiks
type: Opaque
stringData:
  MONGODB_URI: "mongodb+srv://user:pass@cluster.mongodb.net/mozaikscore"
  JWT_SECRET: "your-jwt-secret-here"
```

### Backend Deployment

```yaml
# k8s/backend-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: mozaiks-backend
  namespace: mozaiks
spec:
  replicas: 3
  selector:
    matchLabels:
      app: mozaiks-backend
  template:
    metadata:
      labels:
        app: mozaiks-backend
    spec:
      containers:
        - name: backend
          image: your-registry/mozaiks-backend:latest
          ports:
            - containerPort: 8080
          envFrom:
            - configMapRef:
                name: mozaiks-config
            - secretRef:
                name: mozaiks-secrets
          resources:
            requests:
              memory: "256Mi"
              cpu: "250m"
            limits:
              memory: "512Mi"
              cpu: "500m"
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 30
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: mozaiks-backend
  namespace: mozaiks
spec:
  selector:
    app: mozaiks-backend
  ports:
    - port: 80
      targetPort: 8080
```

### Ingress

```yaml
# k8s/ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: mozaiks-ingress
  namespace: mozaiks
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
    - hosts:
        - app.yourdomain.com
      secretName: mozaiks-tls
  rules:
    - host: app.yourdomain.com
      http:
        paths:
          - path: /api
            pathType: Prefix
            backend:
              service:
                name: mozaiks-backend
                port:
                  number: 80
          - path: /
            pathType: Prefix
            backend:
              service:
                name: mozaiks-frontend
                port:
                  number: 80
```

### Deploy to Kubernetes

```bash
# Apply all manifests
kubectl apply -f k8s/

# Check status
kubectl get pods -n mozaiks
kubectl logs -f deployment/mozaiks-backend -n mozaiks
```

---

## 🔐 Security Checklist

### Environment Variables

```env
# ✅ REQUIRED for production
JWT_SECRET=<min-32-character-random-string>
MONGODB_URI=<connection-string-with-auth>
AUTH_ENABLED=true

# ✅ RECOMMENDED
CORS_ORIGINS=https://yourdomain.com
RATE_LIMIT_REQUESTS=100
RATE_LIMIT_WINDOW=60
```

### Security Headers

Configure in your reverse proxy:

```nginx
# nginx.conf
add_header X-Frame-Options "SAMEORIGIN" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-XSS-Protection "1; mode=block" always;
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
add_header Content-Security-Policy "default-src 'self'" always;
```

### Database Security

```bash
# MongoDB Atlas - enable:
# ✅ IP Allowlist
# ✅ Database users with minimal permissions
# ✅ Encryption at rest
# ✅ Network peering (for K8s)
```

---

## 📊 Monitoring

### Health Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Basic health check |
| `GET /health/ready` | Readiness (DB connected) |
| `GET /health/live` | Liveness (app running) |

### Logging

```python
# Structured logging format
{
    "timestamp": "2025-01-01T12:00:00Z",
    "level": "INFO",
    "service": "mozaiks-backend",
    "correlation_id": "abc-123",
    "message": "Request processed",
    "duration_ms": 45
}
```

### Metrics (Optional)

Add Prometheus metrics:

```python
# main.py
from prometheus_fastapi_instrumentator import Instrumentator

Instrumentator().instrument(app).expose(app)
```

---

## 🔄 CI/CD Pipeline

### GitHub Actions

```yaml
# .github/workflows/deploy.yml
name: Deploy

on:
  push:
    branches: [main]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Build Backend
        run: |
          docker build -t ${{ secrets.REGISTRY }}/mozaiks-backend:${{ github.sha }} \
            -f runtime/backend/Dockerfile runtime/backend
      
      - name: Build Frontend
        run: |
          docker build -t ${{ secrets.REGISTRY }}/mozaiks-frontend:${{ github.sha }} \
            -f Dockerfile.frontend .
      
      - name: Push Images
        run: |
          docker push ${{ secrets.REGISTRY }}/mozaiks-backend:${{ github.sha }}
          docker push ${{ secrets.REGISTRY }}/mozaiks-frontend:${{ github.sha }}
      
      - name: Deploy to Kubernetes
        run: |
          kubectl set image deployment/mozaiks-backend \
            backend=${{ secrets.REGISTRY }}/mozaiks-backend:${{ github.sha }} \
            -n mozaiks
```

---

## ✅ Pre-deployment Checklist

- [ ] Environment variables configured
- [ ] MongoDB connection tested
- [ ] JWT secret is secure (32+ chars)
- [ ] Auth provider configured
- [ ] CORS origins restricted
- [ ] TLS/SSL enabled
- [ ] Health endpoints working
- [ ] Logging configured
- [ ] Backups scheduled
- [ ] Monitoring alerts set

---

## 🔗 Related

- 🚀 [Quickstart](./quickstart.md)
- 🔐 [Authentication](../core/authentication.md)
- 🗄️ [Database](../core/database.md)

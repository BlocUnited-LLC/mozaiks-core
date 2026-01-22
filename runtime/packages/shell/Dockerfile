# syntax=docker/dockerfile:1
# MozaiksCore Shell - Production Dockerfile
# ==========================================
# Build: docker build -t mozaiks/shell:latest .
# Run:   docker run -p 5173:80 mozaiks/shell:latest

# ------------------------------------------------------------------------------
# Stage 1: Builder
# ------------------------------------------------------------------------------
FROM node:20-alpine AS builder

WORKDIR /build

# Copy package files
COPY package*.json ./
RUN npm ci

# Copy source and build
COPY . .
RUN npm run build

# ------------------------------------------------------------------------------
# Stage 2: Runtime (nginx)
# ------------------------------------------------------------------------------
FROM nginx:alpine AS runtime

# Copy built assets
COPY --from=builder /build/dist /usr/share/nginx/html

# Copy nginx config if exists
COPY nginx.conf /etc/nginx/conf.d/default.conf 2>/dev/null || true

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]

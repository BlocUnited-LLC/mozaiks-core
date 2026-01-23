# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

Use this section to tell people how to report a vulnerability.

Tell them where to go, how often you check, and what to expect.

## Security Architecture Overview

Mozaiks Core is designed with a defense-in-depth approach, integrating security at multiple layers:

### 1. Authentication & Identity
*   **Identity Provider (IdP)**: The system is designed to work with OIDC-compliant providers (e.g., Keycloak, Azure AD).
*   **Identity API**: Located in `backend/src/Identity.API`, this service manages user identities, token issuance, and validation.
*   **JWT Implementation**: 
    *   Strict Bearer token validation using `Microsoft.AspNetCore.Authentication.JwtBearer`.
    *   Tokens are signed and validated against a centralized authority (`Jwt__Issuer`).
    *   Audience validation ensures tokens are used only for intended services (`Jwt__Audience`).
*   **AI Runtime Auth**: The Python-based AI Runtime (`runtime/ai`) implements parallel JWT validation logic (`runtime/ai/core/runtime/auth/jwt_validator.py`) to ensure consistency across the hybrid .NET/Python architecture.

### 2. Authorization & Access Control
*   **Role-Based Access Control (RBAC)**: User roles and permissions are encoded in the JWT and enforced at the API gateway and service level.
*   **User Context**: A dedicated `UserContextAccessor` in the .NET backend ensures that user identity is propagated securely through the request pipeline.
*   **API Keys**: 
    *   Service-to-service and external developer access is managed via the `Mozaiks.ApiKeys` building block.
    *   API keys use secure hashing (`ApiKeyHashing.cs`) and are never stored in plain text.

### 3. Data Protection
*   **Secret Management**: 
    *   Production environments use **Azure Key Vault** for high-security secret storage.
    *   Development environments use secure environment variable injection.
*   **Data Encryption**: 
    *   ASP.NET Data Protection keys are persisted securely (`DataProtection:KeysPath`), allowing for encryption of cookies and anti-forgery tokens.
*   **Auditing**: 
    *   Comprehensive administrative auditing via `Mozaiks.Auditing`. 
    *   Sensitive actions are logged to MongoDB with detailed metadata (`AdminAuditActionFilter`), providing a tamper-evident trail for compliance.

### 4. Application Security
*   **Secure Headers**: APIs are configured to forward headers securely for reverse proxy scenarios.
*   **Input Validation**: Strict input validation is applied at the API surface to prevent injection attacks.
*   **Container Security**: 
    *   Services run in isolated Docker containers (`docker-compose.yml`).
    *   Network segmentation ensures that backend services (like MongoDB) are not directly exposed to the public internet, but are only accessible via the API layer.

### 5. AI Safety & Sandboxing
*   **Sandboxed Execution**: AI plugins and dynamic code execution are designed to run in isolated environments (e.g., `workflows/PluginGenerator/tools/sandbox_api`) to prevent unauthorized system access.
*   **Traffic Monitoring**: The AI Runtime includes middleware to monitor and log usage (`usage_ingest.py`), helping detect and mitigate abuse patterns.

## Infrastructure Security

*   **Network Isolation**: Docker Compose manages a private internal network for inter-service communication. Only the Frontend and API Gateway ports are exposed to the host.
*   **Dependency Management**: Dependencies are pinned in `requirements.txt` and `package.json` to ensure reproducible and secure builds.

For further questions or to report a security potential issue, please contact the security team.

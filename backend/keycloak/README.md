# Keycloak Theme Assets for MozaiksCore

> ⚠️ **SCOPE LIMITATION**: This Keycloak configuration is for **MozaiksCore app-user authentication ONLY**.
> It is NOT used for platform user authentication.
>
> **See:** [PLATFORM_AUTH.md](../docs/source_of_truth/PLATFORM_AUTH.md) for the authoritative authentication architecture.

---

## What This Is

This folder contains Keycloak theme assets that can be deployed to MozaiksCore runtime instances for **app-user authentication**.

When a founder builds an app with MozaiksCore:
- Their app users (end-users of the founder's product) can authenticate via Keycloak
- The Keycloak login UI is themed with Mozaiks branding
- This is a **white-label** option for founders who want managed auth

---

## What This Is NOT

This is NOT:
- ❌ Platform authentication for Mozaiks users
- ❌ The IdP for this control plane
- ❌ A dependency of any service in this repository
- ❌ Used for founder, investor, or admin authentication

**Platform users authenticate via Azure AD External Identities (CIAM), NOT Keycloak.**

---

## Architecture Role

```
┌─────────────────────────────────────────────────────────────────┐
│                    MOZAIKS CONTROL PLANE                         │
│   (This repository - BlocUnitedMicroservice)                     │
│                                                                  │
│   Platform User Auth: Azure AD External Identities (CIAM)        │
│   - Founders, Investors, Admins                                  │
│   - Issuer: https://mozaiks.ciamlogin.com/...                    │
│   - Audience: api://mozaiks-auth                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ Provisions & manages
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    MOZAIKSCORE RUNTIME                           │
│   (Customer's deployed app - identified by appId)                │
│                                                                  │
│   App User Auth: Keycloak (OPTIONAL)                             │
│   - Themed with assets from this folder                          │
│   - Each MozaiksCore instance can have its own Keycloak realm    │
│   - Or founder can bring their own IdP                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Deployment

When a MozaiksCore instance is provisioned with managed auth:

1. Provisioning.Agent deploys a Keycloak container
2. Theme assets from this folder are copied to `/opt/keycloak/themes/mozaiks`
3. A realm is created for the specific appId
4. Connection details are stored and returned to the app

---

## Files

| File/Folder | Purpose |
|-------------|---------|
| `Dockerfile` | Builds Keycloak image with Mozaiks theme |
| `theme/mozaiks/` | Theme assets (CSS, templates, images) |
| `theme-schema/` | Schema definitions |
| `login-*.html` | Reference/test files |

---

## IMPORTANT: Code Review Guidelines

When reviewing PRs that touch this folder:

1. **REJECT** any changes that integrate Keycloak into platform auth flows
2. **REJECT** any service code that imports or depends on Keycloak for platform users
3. **ACCEPT** theme customization for MozaiksCore app-user experience
4. **ACCEPT** Keycloak admin API usage for MozaiksCore realm management

---

## Related Documentation

- [CONTROL_PLANE_AUTHORITY.md](../docs/source_of_truth/CONTROL_PLANE_AUTHORITY.md) - Defines platform auth boundaries
- [00_SYSTEM_OVERVIEW.md](../docs/source_of_truth/00_SYSTEM_OVERVIEW.md) - System architecture

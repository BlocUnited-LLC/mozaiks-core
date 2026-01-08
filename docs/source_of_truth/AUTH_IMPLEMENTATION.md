# auth.md — Mozaiks Authentication & Authorization (Fact-Based)

This document captures **only what has been explicitly observed, configured, or reported in this thread**.  
Where something is not confirmed from the thread, it is listed under **“Items to Confirm”** (no assumptions).

---

## 1) High-Level Architecture (What Mozaiks Does)

### 1.1 Core principle
- The Mozaiks stack is configured to use an external Identity Provider (IdP) for sign-in (CIAM / OIDC).
- Mozaiks services **validate tokens** issued by the IdP; Mozaiks does **not** mint user JWTs in production code (per verification reports provided in the conversation).

### 1.2 Token usage
- Frontend obtains tokens (via MSAL).
- Backend validates incoming Bearer tokens against an authority + audience + required scope.
- User identity in backend is derived from the `sub` claim (per verification report content provided).

---

## 2) Tenant & Identity Provider (Confirmed)

### 2.1 CIAM tenant
- **CIAM Tenant ID**: `9d0073d5-42e8-46f0-a325-5b4be7b1a38d`
- CIAM login domain observed in working discovery JSON:
  - Base: `https://mozaiks.ciamlogin.com/`
  - Issuer in discovery JSON:
    - `https://9d0073d5-42e8-46f0-a325-5b4be7b1a38d.ciamlogin.com/9d0073d5-42e8-46f0-a325-5b4be7b1a38d/v2.0`

### 2.2 OpenID configuration behavior that was observed
Two different URL patterns were tested:

**A) “B2C policy-style path” (did not work)**
- Attempted:
  - `https://mozaiks.ciamlogin.com/B2C_1_signupsignin/v2.0/.well-known/openid-configuration`
  - Result observed: `AADSTS90002 invalid tenant` for tenant segment `b2c_1_signupsignin` (error returned in screenshot).
- Attempted:
  - `https://mozaiks.ciamlogin.com/mozaiks.onmicrosoft.com/B2C_1_signupsignin/v2.0/.well-known/openid-configuration`
  - Result observed: HTTP 404

**B) “Tenant-guid based path” (worked)**
- Working discovery JSON was shown in the thread for a URL of the form:
  - `https://mozaiks.ciamlogin.com/<TENANT_ID>/v2.0/.well-known/openid-configuration`
- The JSON included (confirmed values):
  - `authorization_endpoint`:
    - `https://mozaiks.ciamlogin.com/9d0073d5-42e8-46f0-a325-5b4be7b1a38d/oauth2/v2.0/authorize`
  - `token_endpoint`:
    - `https://mozaiks.ciamlogin.com/9d0073d5-42e8-46f0-a325-5b4be7b1a38d/oauth2/v2.0/token`
  - `jwks_uri`:
    - `https://mozaiks.ciamlogin.com/9d0073d5-42e8-46f0-a325-5b4be7b1a38d/discovery/v2.0/keys`
  - `end_session_endpoint`:
    - `https://mozaiks.ciamlogin.com/9d0073d5-42e8-46f0-a325-5b4be7b1a38d/oauth2/v2.0/logout`
  - `userinfo_endpoint`:
    - `https://graph.microsoft.com/oidc/userinfo`

---

## 3) App Registrations in the Tenant (Confirmed from screenshot list + CLI output)

From the Azure Portal “App registrations” list screenshot, the following applications exist:

1) **Mozaiks**  
- Application (client) ID: `3f59079c-9eaa-45b9-a67c-ea9eb729ff1b`

2) **Mozaiks Mobile**  
- Application (client) ID: `81ae4247-2079-4c03-830f-abd3e99f9b0a`

3) **MozaiksAuth API**  
- Application (client) ID: `2b729cd7-3004-4fb6-8e86-ce1b92589f0d`

(There is also the default `b2c-extensions-app` shown in the list, which Azure creates for identity storage scenarios; it appears in the portal list as standard infrastructure.)

### 3.1 “MozaiksAuth API” OAuth scope (Confirmed)
From `az ad app show` output in the thread:

- API OAuth2 permission scope:
  - `value`: `access_as_user`
  - `id`: `2e866f6a-70bb-4073-bdb3-9ea1ee5a80aa`
  - `isEnabled`: `true`
  - Admin consent display:
    - `adminConsentDisplayName`: `Access Mozaiks platform APIs`
    - `adminConsentDescription`: `Allows you to access Mozaiks platform APIs`

The UI screenshot of the scope creation showed the identifier URI pattern:
- `api://mozaiks-auth/access_as_user` (this was explicitly confirmed by you with: “Yes → api://mozaiks-auth/access_as_user”)

### 3.2 Consent & admin-consent-required (Confirmed)
In the permission blade screenshot:
- The scope shows **Admin consent required: No**.

This means:
- A user can consent to this delegated scope themselves *without requiring an admin to consent first* (assuming tenant consent policies allow user consent).

> Note: “Admin consent required: No” is **not inherently good or bad**; it is a product decision. It reduces friction for end-users, but increases the importance of using accurate consent descriptions and controlling who can create accounts, if needed.

---

## 4) Web Frontend (MOZ-UI) Auth Logic (Facts from the provided verification report)

### 4.1 Library usage (confirmed by report text)
- Uses MSAL libraries:
  - `@azure/msal-browser`
  - `@azure/msal-react`

### 4.2 Configuration method (confirmed by report text)
- Auth configuration is environment-driven and read from env vars.

The report listed:
- `REACT_APP_AUTH_PROVIDER=ciam`
- `REACT_APP_AUTH_AUTHORITY` includes `https://mozaiks.ciamlogin.com/`
- `REACT_APP_AUTH_SCOPES` present
- `REACT_APP_AUTH_CLIENT_ID` previously had a placeholder in `.env.development.local` (report called this out as blocking earlier tests)

### 4.3 Token attachment to API calls (confirmed by report text)
- Axios factory obtains an access token and sets:
  - `Authorization: Bearer <token>`

### 4.4 Auth flow (confirmed by report text)
- Redirect-based sign-in using MSAL redirect flow.
- A `PrivateRoute` exists that checks authentication state and gates routes.

---

## 5) Backend (.NET services) Auth Logic (Facts from the provided verification report)

### 5.1 Single entry point pattern
The report stated that all backend services use a single helper:
- `builder.AddMozaiksAuth(...)`

Services listed in the report as using this:
- users.api
- payment.api
- app.api
- governance.api
- communicationservice.api
- authserver.api
- admin.api
- validationengine.api
- hosting.api
- insights.api
- notification.api

### 5.2 Environment-driven backend configuration
The report included a sample `.env`:

- `AUTH_PROVIDER=ciam`
- `AUTH_AUTHORITY=https://mozaiks.ciamlogin.com/`
- `AUTH_TENANT_ID=9d0073d5-42e8-46f0-a325-5b4be7b1a38d`
- `AUTH_AUDIENCE=api://mozaiks-auth`
- `AUTH_REQUIRED_SCOPE=access_as_user`
- `AUTH_USER_ID_CLAIM=sub`

### 5.3 User identity claim
The report stated:
- The backend uses `sub` as the configurable user ID claim (`AUTH_USER_ID_CLAIM=sub`).
- No production dependency on `oid` was found per report.

### 5.4 “No JWT minting” in production
The report stated:
- No `GenerateJwtToken` usage in production (only test file reference).
- No password authentication code in production.
- No `/Auth/SignIn` endpoint.
- Any old symmetric key JWT code exists only in archived/retired code.

---

## 6) Users App Shell (Open Source) Auth Modes (Facts from the provided verification report)

### 6.1 Supported auth modes
The report states the shell supports:
- `platform` (Mozaiks-hosted CIAM)
- `external` (self-hosted “bring your own IdP”)
- `local` (local/password mode; optional and isolated)

Default mode (per report):
- `external` unless `MOZAIKS_AUTH_MODE` is set differently.

### 6.2 External mode behavior
The report states:
- Validates JWTs using JWKS fetched and cached.
- No password fields in external user provisioning.
- Auto-provisions a user record on first valid token.

### 6.3 Local mode behavior
The report states:
- `/token` and `/register` routes exist only when `MOZAIKS_AUTH_MODE=local`
- Password UI appears only in `local` mode; external/platform uses redirect login.

---

## 7) Mobile App Registration (What was configured / attempted in CLI)

### 7.1 Azure CLI login state in Mozaiks tenant (confirmed)
- You logged into tenant-level account because the tenant has **no subscription** associated (as shown by the CLI output):
  - `az login --tenant 9d0073d5-42e8-46f0-a325-5b4be7b1a38d --allow-no-subscriptions`

### 7.2 Creating a “Mozaiks Mobile” app (confirmed)
- Created:
  - `Mozaiks Mobile` appId: `81ae4247-2079-4c03-830f-abd3e99f9b0a`

### 7.3 Setting `publicClient` redirect URIs via `az ad app update` (confirmed failure)
- Command attempted:
  - `az ad app update --set publicClient.redirectUris="[]"`
- CLI error:
  - `Couldn't find 'publicClient' ...`

### 7.4 Using Microsoft Graph PATCH (`az rest`) to set `publicClient` (confirmed)
- First Graph PATCH attempt used the `appId` in the `/applications/{id}` path, resulting in:
  - `Request_ResourceNotFound`
- You then correctly retrieved the **object id**:
  - object id: `176d4d5b-92ce-4f48-9a97-d1ee1ff567f9`
- PATCH to:
  - `https://graph.microsoft.com/v1.0/applications/176d4d5b-92ce-4f48-9a97-d1ee1ff567f9`
- Completed without error output (success implied by the absence of an error message in the transcript).

### 7.5 Adding API permission for Mobile to call MozaiksAuth API (confirmed)
You added the permission using the **scope GUID**:

- MozaiksAuth API appId: `2b729cd7-3004-4fb6-8e86-ce1b92589f0d`
- Scope id: `2e866f6a-70bb-4073-bdb3-9ea1ee5a80aa`

Command (confirmed):
- `az ad app permission add --id 81ae4247-2079-4c03-830f-abd3e99f9b0a --api 2b729cd7-3004-4fb6-8e86-ce1b92589f0d --api-permissions 2e866f6a-70bb-4073-bdb3-9ea1ee5a80aa=Scope`

Then:
- `az ad app permission list --id 81ae4247-2079-4c03-830f-abd3e99f9b0a` showed the scope id present.

> Note: The CLI output indicated an additional step “permission grant” may be required for the change to be effective.

---

## 8) Consent Screens Observed (Facts from screenshots)

You observed consent prompts showing:

- **MozaiksAuth** requesting:
  - “Access Mozaiks platform APIs (MozaiksAuth API)”
  - “View your basic profile”
  - “Maintain access to data you have given it access to”

Then a second step:
- **MozaiksAuth API** requesting:
  - “View users’ basic profile”
  - “Maintain access to data you have given it access to”

This indicates:
- The sign-in flow is requesting delegated permissions that include:
  - Your custom scope (`access_as_user`)
  - Microsoft Graph basic profile scope(s) (e.g., User.Read / profile/openid-related permissions)

---

## 9) Branding & Publisher Domain (Facts + current state)

### 9.1 Branding screen state (confirmed from screenshot)
You are in:
- **App registrations → Mozaiks → Branding & properties**
Fields shown populated:
- Home page URL: `https://www.mozaiks.ai/`
- Terms of service URL: `https://www.mozaiks.ai/terms`
- Privacy statement URL: `https://www.mozaiks.ai/privacy`
Logo: only a logo upload is visible on that page.

### 9.2 Publisher domain warning shown (confirmed)
The portal shows:
- Publisher domain currently `mozaiks.onmicrosoft.com`
- Warning indicates you must use a **custom domain** (DNS-verified) to proceed with publisher verification.

---

## 10) Items to Confirm (Explicitly Unknown / Not Proven in this thread)

These are not safe to assert without additional evidence:

1) **Exact user flows / policies configuration**
- We saw references to `B2C_1_signupsignin` in attempted URLs and UI, but we do not have confirmed details of the configured user flows, their names, or their assignments beyond what was attempted.

2) **Exact redirect URIs required for each client**
- For the SPA screen you showed these URIs present (observed):
  - `http://localhost:3000`
  - `http://localhost:3000/auth/callback`
  - `https://app.mozaiks.ai`
  - `https://app.mozaiks.ai/auth/callback`
- We do not have a complete inventory for every app registration (Mozaiks vs Mozaiks Mobile vs MozaiksAuth API).

3) **Mobile redirect URI scheme**
- For native apps, the redirect URI typically uses a custom scheme (e.g., `msal<clientId>://auth` or app-specific). This was not shown/confirmed.

4) **Whether user provisioning / linking logic exists in .NET backend**
- The “auto-provision” claim was explicitly confirmed for the Users App Shell per report, but not confirmed for the .NET backend.

5) **Exact production authority used by each repo**
- We have env examples, but we do not have the checked-in final `.env` files, nor the production secrets/config.

---

## 11) Operational Checklist (Based on Confirmed Config)

### 11.1 CIAM endpoints to rely on (confirmed pattern)
- Authority base: `https://mozaiks.ciamlogin.com/`
- Tenant-specific endpoints are of the form:
  - `https://mozaiks.ciamlogin.com/<TENANT_ID>/oauth2/v2.0/authorize`
  - `https://mozaiks.ciamlogin.com/<TENANT_ID>/oauth2/v2.0/token`
  - `https://mozaiks.ciamlogin.com/<TENANT_ID>/discovery/v2.0/keys`

### 11.2 API audience & scope (confirmed)
- API audience: `api://mozaiks-auth`
- Required scope: `access_as_user`
- Scope GUID: `2e866f6a-70bb-4073-bdb3-9ea1ee5a80aa`

### 11.3 App IDs (confirmed)
- Tenant ID: `9d0073d5-42e8-46f0-a325-5b4be7b1a38d`
- Mozaiks (client): `3f59079c-9eaa-45b9-a67c-ea9eb729ff1b`
- Mozaiks Mobile (client): `81ae4247-2079-4c03-830f-abd3e99f9b0a`
- MozaiksAuth API (resource): `2b729cd7-3004-4fb6-8e86-ce1b92589f0d`

---

## 12) Repository Mapping (What is known from your description)

You stated you have:
1) **Frontend repo** (Mozaiks UI)
2) **Backend repo** (.NET services)
3) **Mozaiks Shell** (open source self-hosting)

This document does not assert exact file paths beyond what your provided verification text referenced; it records the behavioral assertions from those reports as stated.

---

## Appendix A — CLI/Graph Notes Observed

### A.1 AppId vs ObjectId (confirmed lesson from the transcript)
- Microsoft Graph `/applications/{id}` expects the **object id**, not the **appId (client id)**.
- You resolved this correctly by querying:
  - `az ad app list --filter "appId eq '<APPID>'" --query "[0].id"`

### A.2 Permission assignment uses GUIDs (confirmed)
- When adding permissions via CLI, the `--api-permissions` value must be the **scope GUID**, not the human string `access_as_user`.
- You ultimately used:
  - `2e866f6a-70bb-4073-bdb3-9ea1ee5a80aa=Scope`

---

## Appendix B — Current UX Observations (What you saw)
- The sign-in / sign-up pages shown are the standard CIAM hosted UI (email-first screens and consent prompts).
- You observed that “Login” and “Sign up” both lead into the same email-first flow, with the UI offering “No account? Create one” as a link.

(No additional UI customization behavior is asserted here; only what you observed.)

---

End of document.

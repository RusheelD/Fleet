# Fleet — Authentication & Environment Setup Guide

This guide covers:

1. [Microsoft Entra ID App Registration](#1-microsoft-entra-id-app-registration)
2. [Adding Google as an Identity Provider](#2-adding-google-as-an-identity-provider)
3. [Adding GitHub as an Identity Provider](#3-adding-github-as-an-identity-provider)
4. [Backend Configuration](#4-backend-configuration)
5. [Frontend Configuration](#5-frontend-configuration)
6. [Website (Marketing Site) Configuration](#6-website-marketing-site-configuration)
7. [Environment & Deployment Setup](#7-environment--deployment-setup)
8. [DNS Configuration](#8-dns-configuration)

---

## 1. Microsoft Entra ID App Registration

Before configuring social providers, you need two Entra ID app registrations: one for the **SPA (frontend)** and one for the **API (backend)**.

### 1a. API App Registration (Fleet.Server)

1. Go to [Azure Portal → Entra ID → App registrations → New registration](https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/CreateApplicationBlade)
2. **Name:** `Fleet API`
3. **Supported account types:** "Accounts in this organizational directory only" (single tenant) — or "Accounts in any organizational directory and personal Microsoft accounts" if you want broad access
4. Click **Register**
5. Note the **Application (client) ID** and **Directory (tenant) ID**

#### Expose an API

1. Go to **Expose an API** in the left nav
2. Click **Set** next to "Application ID URI" — accept the default `api://{clientId}` or set a custom one
3. Click **Add a scope**:
   - **Scope name:** `access_as_user`
   - **Who can consent:** Admins and users
   - **Admin consent display name:** `Access Fleet API as a user`
   - **Admin consent description:** `Allows the Fleet SPA to call the Fleet API on behalf of the signed-in user`
   - **State:** Enabled
4. The full scope URI will be: `api://{apiClientId}/access_as_user`

### 1b. SPA App Registration (Frontend)

1. Go to **App registrations → New registration**
2. **Name:** `Fleet SPA`
3. **Supported account types:** Same as the API registration
4. **Redirect URI:** Select **Single-page application (SPA)** and add all redirect URIs:
   - `http://localhost:5173` (local dev via Vite)
   - `https://app-dev.fleet-ai.dev` (development)
   - `https://app-staging.fleet-ai.dev` (staging)
   - `https://app.fleet-ai.dev` (production)
5. Click **Register**
6. Note the **Application (client) ID**

#### API Permissions

1. Go to **API permissions → Add a permission → My APIs**
2. Select **Fleet API**
3. Check `access_as_user` → **Add permissions**
4. Click **Grant admin consent for {tenant}** (if applicable)

---

## 2. Adding Google as an Identity Provider

This uses **Entra External ID** (formerly Azure AD B2B) to allow users to sign in with their Google accounts.

### Step 1: Create a Google OAuth 2.0 Application

1. Go to [Google Cloud Console → Credentials](https://console.cloud.google.com/apis/credentials)
2. Create a new project (or select an existing one)
3. Click **Create Credentials → OAuth client ID**
4. If prompted, configure the **OAuth consent screen** first:
   - **App name:** `Fleet`
   - **User support email:** your email
   - **Authorized domains:** `fleet-ai.dev`
   - **Developer contact:** your email
   - Click **Save and Continue** through the Scopes and Test Users steps
5. Back on Credentials, click **Create Credentials → OAuth client ID**:
   - **Application type:** Web application
   - **Name:** `Fleet Entra ID`
   - **Authorized redirect URIs:** Add `https://login.microsoftonline.com/te/{tenantId}/oauth2/authresp`
     - Replace `{tenantId}` with your Entra ID directory (tenant) ID
   - Click **Create**
6. Note the **Client ID** and **Client Secret**

### Step 2: Configure Google in Entra ID

1. Go to [Azure Portal → Entra ID → External Identities → All identity providers](https://portal.azure.com/#view/Microsoft_AAD_IAM/CompanyRelationshipsMenuBlade/~/IdentityProviders)
2. Click **+ Google**
3. Enter:
   - **Client ID:** The Google OAuth client ID from Step 1
   - **Client secret:** The Google OAuth client secret from Step 1
4. Click **Save**

### Step 3: Configure Cross-tenant Access (if needed)

If your tenant is set to single-tenant, you may need to allow Google identities:

1. Go to **Entra ID → External Identities → Cross-tenant access settings**
2. Under **Default settings** or **Organizational settings**, ensure inbound access allows Google identity provider

> **Note:** The `domainHint: 'google.com'` parameter in MSAL tells Entra ID to skip the home realm discovery and route the user directly to Google sign-in. This is already configured in `frontend/src/auth/msalConfig.ts`.

---

## 3. Adding GitHub as an Identity Provider

GitHub requires using **Entra External ID with custom OpenID Connect** (since GitHub isn't a built-in Entra identity provider like Google or Facebook).

### Option A: Using Entra External ID for Customers (Recommended)

If you are using **Entra External ID** (a workforce + customer-facing tenant, also known as a CIAM tenant):

1. Go to [Azure Portal → Entra ID → External Identities → All identity providers](https://portal.azure.com/#view/Microsoft_AAD_IAM/CompanyRelationshipsMenuBlade/~/IdentityProviders)
2. Click **+ New OpenID Connect provider**
3. Configure:
   - **Name:** `GitHub`
   - **Metadata URL:** `https://token.actions.githubusercontent.com/.well-known/openid-configuration`
   - **Client ID:** Your GitHub OAuth App client ID (see below)
   - **Client secret:** Your GitHub OAuth App client secret

### Option B: Using a GitHub OAuth App with Custom OIDC

For standard workforce tenants, you need to set up a GitHub OAuth App:

#### Step 1: Create a GitHub OAuth App

1. Go to [GitHub → Settings → Developer settings → OAuth Apps → New OAuth App](https://github.com/settings/applications/new)
2. Fill in:
   - **Application name:** `Fleet`
   - **Homepage URL:** `https://fleet-ai.dev`
   - **Authorization callback URL:** `https://login.microsoftonline.com/te/{tenantId}/oauth2/authresp`
     - Replace `{tenantId}` with your Entra ID tenant ID
3. Click **Register application**
4. Note the **Client ID**
5. Click **Generate a new client secret** — note the **Client Secret**

#### Step 2: Configure as OpenID Connect Provider in Entra ID

1. Go to **Entra ID → External Identities → All identity providers**
2. Click **+ Custom OpenID Connect** (or **+ New OpenID Connect provider**)
3. Enter:
   - **Name:** `GitHub`
   - **Issuer URL / Metadata URL:** You'll need a proxy or federation service since GitHub OAuth doesn't natively support OIDC for Entra
   - **Client ID:** From the GitHub OAuth App
   - **Client secret:** From the GitHub OAuth App

### Option C: Using Azure AD B2C (Alternative)

If you need full social identity support with both Google and GitHub, **Azure AD B2C** provides built-in support:

1. Create an Azure AD B2C tenant
2. Add **Google** and **GitHub** as identity providers (both are built-in)
3. Create a **User Flow** for sign-up/sign-in
4. Update MSAL to use the B2C authority instead of standard Entra ID
5. The `domainHint` parameter works with B2C identity providers as well

> **Recommended approach:** If your primary goal is to support Google + GitHub alongside Microsoft accounts, **Azure AD B2C** or **Entra External ID for customers** gives the smoothest experience. Standard workforce Entra ID supports Google natively but requires custom OIDC configuration for GitHub.
<!--> <!-->
> **Note:** The `domainHint: 'github.com'` parameter in MSAL is pre-configured in `frontend/src/auth/msalConfig.ts` and will route users to the GitHub provider once it's configured.

---

## 4. Backend Configuration

### appsettings.json

The base `appsettings.json` contains Entra ID and CORS configuration. Environment-specific files override these values.

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_API_CLIENT_ID",
    "Audience": "api://YOUR_API_CLIENT_ID"
  },
  "AllowedOrigins": [
    "http://localhost:5173",
    "http://localhost:5174"
  ]
}
```

### Environment-Specific Overrides

| File | Purpose | AllowedOrigins |
| ------ | --------- | ---------------- |
| `appsettings.json` | Base / local dev | `localhost:5173`, `localhost:5174` |
| `appsettings.Development.json` | Local dev overrides | Inherits from base |
| `appsettings.Staging.json` | Staging deployment | `app-staging.fleet-ai.dev`, `fleet-ai.dev` |
| `appsettings.Production.json` | Production deployment | `app.fleet-ai.dev`, `fleet-ai.dev` |

Set `ASPNETCORE_ENVIRONMENT` to `Development`, `Staging`, or `Production` on each deployment target.

### CORS

CORS is configured in `Program.cs` to read from `AllowedOrigins`:

```csharp
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

---

## 5. Frontend Configuration

The frontend uses `.env` files (loaded by Vite) for Entra ID and environment config.

### Environment Files

| File | Used When | `VITE_ENVIRONMENT` |
| ------ | ----------- | ------------------- |
| `.env.development` | `vite dev` or `vite build --mode development` | `development` |
| `.env.staging` | `vite build --mode staging` | `staging` |
| `.env.production` | `vite build` or `vite build --mode production` | `production` |

### Required Variables

```dotenv
# Application (client) ID of the Fleet SPA app registration
VITE_ENTRA_CLIENT_ID=your-spa-client-id

# Authority - https://{tenant}.ciamlogin.com/{tenantId}
VITE_ENTRA_AUTHORITY=https://your-tenant.ciamlogin.com/your-tenant-id

# API scope - api://{apiClientId}/access_as_user
VITE_ENTRA_API_SCOPE=api://your-api-client-id/access_as_user

# Optional explicit redirect URI (must exactly match Entra app registration)
VITE_ENTRA_REDIRECT_URI=https://fleet-ai-dev.azurewebsites.net/

# Environment label
VITE_ENVIRONMENT=development

# Marketing website URL (for "back to site" links)
VITE_WEBSITE_URL=https://fleet-ai.dev
```

### Building for Each Environment

```bash
# Development
npx vite build --mode development

# Staging
npx vite build --mode staging

# Production (default)
npx vite build
```

### Entra ID SPA Redirect URIs

Make sure **all** app origins are registered as SPA redirect URIs in the **Fleet SPA** app registration:

| Environment | Redirect URI |
| ------------- | ------------- |
| Local dev | `http://localhost:5173` |
| Development | `https://app-dev.fleet-ai.dev` |
| Staging | `https://app-staging.fleet-ai.dev` |
| Production | `https://app.fleet-ai.dev` |

---

## 6. Website (Marketing Site) Configuration

The marketing website at `fleet-ai.dev` uses its own `.env` files.

### Required Variables (Website)

```dotenv
# URL of the Fleet app (Login/Sign up buttons link here)
VITE_APP_URL=https://app.fleet-ai.dev

# Environment label
VITE_ENVIRONMENT=production
```

### Environment Files (Website)

| File | `VITE_APP_URL` |
| ------ | ---------------- |
| `.env.development` | `https://app-dev.fleet-ai.dev` |
| `.env.staging` | `https://app-staging.fleet-ai.dev` |
| `.env.production` | `https://app.fleet-ai.dev` |

---

## 7. Environment & Deployment Setup

### Architecture Overview

```text
fleet-ai.dev              → Marketing website (website/ project)
app-dev.fleet-ai.dev      → Fleet app — development release
app-staging.fleet-ai.dev  → Fleet app — staging release
app.fleet-ai.dev          → Fleet app — production release
api-dev.fleet-ai.dev      → Fleet API — development
api-staging.fleet-ai.dev  → Fleet API — staging
api.fleet-ai.dev          → Fleet API — production
```

### Deployment Steps (per environment)

#### Backend (Fleet.Server)

1. Set `ASPNETCORE_ENVIRONMENT` to `Development`, `Staging`, or `Production`
2. Fill in `AzureAd` settings in the appropriate `appsettings.{Environment}.json`
3. Set `AllowedOrigins` to match the frontend + website URLs for that environment
4. Ensure the PostgreSQL connection string is configured (via Aspire or `ConnectionStrings:fleetdb`)
5. Ensure Redis is available (via Aspire or `ConnectionStrings:cache`)

```bash
# Example: publish for production
dotnet publish Fleet.Server -c Release -o ./publish
```

#### Frontend

1. Create the `.env.{mode}` file with correct Entra ID values and environment name
2. Build with the appropriate mode:

   ```bash
   cd frontend
   npm ci
   npx vite build --mode staging   # or production, development
   ```

3. Deploy the `dist/` output to a static file host (Azure Static Web Apps, CDN, etc.) or let Aspire serve it from `wwwroot`

#### Website

1. Create the `.env.{mode}` file with the correct `VITE_APP_URL`
2. Build:

   ```bash
   cd website
   npm ci
   npx vite build --mode staging   # or production, development
   ```

3. Deploy the `dist/` output to a static file host serving `fleet-ai.dev`

### Environment Variables Summary

| Variable | Where | Dev | Staging | Production |
| ---------- | ------- | ----- | --------- | ------------ |
| `ASPNETCORE_ENVIRONMENT` | Server | `Development` | `Staging` | `Production` |
| `VITE_ENTRA_CLIENT_ID` | Frontend | SPA client ID | SPA client ID | SPA client ID |
| `VITE_ENTRA_AUTHORITY` | Frontend | `https://{tenant}.ciamlogin.com/{tid}` | Same | Same |
| `VITE_ENTRA_API_SCOPE` | Frontend | `api://{id}/access_as_user` | Same | Same |
| `VITE_ENTRA_REDIRECT_URI` | Frontend | `http://localhost:5250/` | `https://fleet-ai-dev.azurewebsites.net/` | `https://fleet-ai-dev.azurewebsites.net/` |
| `VITE_ENVIRONMENT` | Frontend + Website | `development` | `staging` | `production` |
| `VITE_WEBSITE_URL` | Frontend | `https://fleet-ai.dev` | `https://fleet-ai.dev` | `https://fleet-ai.dev` |
| `VITE_APP_URL` | Website | `https://app-dev.fleet-ai.dev` | `https://app-staging.fleet-ai.dev` | `https://app.fleet-ai.dev` |

---

## 8. DNS Configuration

Set up the following DNS records for `fleet-ai.dev`:

| Record Type | Host | Value | Purpose |
| --- | --- | --- | --- |
| A / CNAME | `@` (root) | Your static host IP / CDN | Marketing website |
| A / CNAME | `app` | Your app hosting IP / CDN | Production app |
| A / CNAME | `app-staging` | Your staging host IP / CDN | Staging app |
| A / CNAME | `app-dev` | Your dev host IP / CDN | Development app |
| A / CNAME | `api` | Your API server IP / CDN | Production API |
| A / CNAME | `api-staging` | Your staging API IP / CDN | Staging API |
| A / CNAME | `api-dev` | Your dev API IP / CDN | Development API |

All subdomains should have **TLS certificates** configured (use a wildcard cert `*.fleet-ai.dev` or individual certs via Let's Encrypt / Azure-managed certs).

---

## Quick Start Checklist

- [ ] Create **Fleet API** app registration in Entra ID (Section 1a)
- [ ] Create **Fleet SPA** app registration with SPA redirect URIs (Section 1b)
- [ ] Configure Google as an identity provider in Entra ID (Section 2)
- [ ] Configure GitHub as an identity provider (Section 3 — pick Option A, B, or C)
- [ ] Fill in `AzureAd` values in `Fleet.Server/appsettings.json`
- [ ] Create `frontend/.env` with Entra ID values (copy from `.env.example`)
- [ ] Verify local dev works: `dotnet run --project Fleet.AppHost`
- [ ] Set up DNS records for `fleet-ai.dev` and subdomains
- [ ] Deploy backend, frontend, and website per environment (Section 7)
- [ ] Add all environment redirect URIs to the SPA app registration


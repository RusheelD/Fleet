# Fleet — Authentication & Security

## Identity Provider

**Microsoft Entra ID (Azure AD B2C)** via **MSAL** (Microsoft Authentication Library).

- Fleet delegates all authentication to Azure AD B2C as an external OIDC provider
- Azure AD B2C handles user registration, login, password reset, and token issuance
- The Fleet backend validates OIDC tokens — it does not manage passwords or sessions directly

### Supported Sign-In Methods (via Azure AD B2C Identity Providers)

- GitHub
- Google
- Microsoft (personal + work accounts)

### Token Flow

1. User clicks "Sign In" → redirected to Azure AD B2C login page
2. User authenticates with their chosen provider (GitHub, Google, Microsoft)
3. Azure AD B2C issues an **ID token** (identity) and **access token** (API authorization)
4. Frontend stores tokens via MSAL.js and sends the access token as a `Bearer` header on API calls
5. Fleet.Server validates the token using OIDC middleware (`AddAuthentication().AddJwtBearer()` or `AddMicrosoftIdentityWebApi()`)

### GitHub Token for Repo Access

- The Azure AD B2C sign-in gives Fleet the user's identity, but **not** a GitHub API token with repo permissions
- A separate **GitHub OAuth flow** is needed to obtain a GitHub access token with `repo` scope
- This GitHub token is stored securely in the database (encrypted) and used by agents to read/write repos
- The GitHub linking step happens during project creation (or earlier in settings)

### Backend Configuration

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAdB2C"));

// appsettings.json
{
  "AzureAdB2C": {
    "Instance": "https://<tenant>.b2clogin.com",
    "Domain": "<tenant>.onmicrosoft.com",
    "ClientId": "<app-client-id>",
    "SignUpSignInPolicyId": "B2C_1_signup_signin"
  }
}
```

### Frontend Configuration

- Use `@azure/msal-react` + `@azure/msal-browser` for the React SPA
- `MsalProvider` wraps the app (alongside `FluentProvider`)
- `useMsal()` hook for login/logout, `acquireTokenSilent()` for API calls
- Token is attached to all `/api/*` requests via an Axios/fetch interceptor

## Security Considerations

| Area | Approach |
| --- | --- |
| **API authentication** | All `/api/*` endpoints require a valid Bearer token (except health checks) |
| **GitHub tokens at rest** | Encrypted in the database; never returned to the frontend |
| **CORS** | Restrict to the Fleet frontend origin (Vite dev server in dev, production domain in prod) |
| **Rate limiting** | Apply rate limiting on API endpoints (ASP.NET rate-limiting middleware) |
| **Input validation** | `[ApiController]` provides automatic model validation; additional validation in service layer |
| **Secrets management** | Azure Key Vault for production secrets; .NET user secrets for local dev |

## Payments & Billing

### Payment Processor

**Stripe** as the primary payment processor, with support for:

- **Stripe Checkout / Billing** — subscription management, recurring payments
- **Google Pay** — via Stripe's payment method integration (Stripe supports Google Pay natively)
- **Apple Pay** — via Stripe's payment method integration (Stripe supports Apple Pay natively)

> Stripe handles Google Pay and Apple Pay as payment methods within the same integration — no separate payment processor needed.

### Credit Tracking

- Subscription tier and credit balance stored in the `Subscription` entity
- Agent execution deducts credits based on compute time / LLM token usage
- When credits are exhausted, agents are gracefully stopped
- Billing webhook from Stripe resets credits on renewal

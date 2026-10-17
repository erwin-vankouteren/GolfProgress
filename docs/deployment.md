# Deploying the API

What a deployed environment must provide, and how. For the database accounts and
migrations, see [`db/README.md`](../db/README.md).

## Secrets: supplied by the environment, never by the repository

`appsettings.json` ships `Jwt:Key` and `ConnectionStrings:DefaultConnection` empty on
purpose: secrets never live in the repository. Locally they come from user-secrets, but
**user-secrets are only loaded in the `Development` environment**, so every deployed
environment must supply them itself.

| Setting | Environment variable | Requirement |
|---|---|---|
| `Jwt:Key` | `Jwt__Key` | At least 32 bytes (UTF-8); used to sign access tokens with HMAC-SHA256 |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | The application's database connection; see `db/README.md` |

In environment variables, `__` (two underscores) stands for the `:` in a setting name.

### Startup fails fast if either is missing

This is intended. Startup checks both and stops with a message naming the setting and
where to supply it, rather than starting and failing on the first request:

- `ConnectionStrings:DefaultConnection is not configured. Supply it via user-secrets, the ConnectionStrings__DefaultConnection environment variable, or Key Vault.`
- `Jwt:Key must be at least 32 bytes for HMAC-SHA256. Supply it via user-secrets, the Jwt__Key environment variable, or Key Vault — never appsettings.json.`

If a deployment won't start, check the application log for one of these first.

### Generating a JWT key

Use a cryptographic random generator, never a hand-typed value. Any of these gives a
64-character key, comfortably over the minimum:

```powershell
# PowerShell (Windows PowerShell 5.1 and PowerShell 7)
$bytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

```bash
openssl rand -base64 48
```

Use a different key per environment, so a token issued by one is never accepted by another.

### Rotating the JWT key

Set the new value and restart. Access tokens signed with the old key stop working
immediately, but they only live for `Jwt:ExpiresMinutes` (15 by default) anyway:
clients get a `401` and fetch a new access token with their refresh token. Refresh tokens
are stored as hashes that don't depend on this key, so **nobody is logged out**.

## Supplying the secrets in Azure App Service

### Option 1: app settings (environment variables)

App Service → **Settings** → **Environment variables** → **App settings**: add `Jwt__Key`
and `ConnectionStrings__DefaultConnection`. App settings are encrypted at rest and exposed
to the app as environment variables.

```powershell
az webapp config appsettings set --resource-group <rg> --name <app-name> --settings "Jwt__Key=<key>"
```

### Option 2: Key Vault references (recommended)

The secret lives in Key Vault, and the app setting only points at it. No code change is
needed: App Service resolves the reference and hands the app the secret value.

1. Store the secret in Key Vault, e.g. as `JwtKey`.
2. Give the App Service a managed identity (**Identity** → **System assigned** → **On**), and
   grant it the **Key Vault Secrets User** role on the vault.
3. Set the app setting to a reference instead of the value:

   ```
   Jwt__Key = @Microsoft.KeyVault(VaultName=<vault-name>;SecretName=JwtKey)
   ```

Rotating then means adding a new secret version in Key Vault. A reference without a
version picks up the latest version within 24 hours, or immediately on restart.

### The connection string needs no secret at all with a managed identity

With a managed identity for the database (see "Managed identity" in `db/README.md`),
`ConnectionStrings__DefaultConnection` contains no password and can be a plain app
setting. Only `Jwt__Key` then remains secret.

## Other settings for a deployed environment

These aren't secret and have working defaults in `appsettings.json`. Override them per
environment in the same way, e.g. `RateLimiting__Auth__PerIpPermitLimit=20`.

| Setting | Default | When to change it |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Never set `Development` in a deployed environment: it enables Swagger and turns off HSTS |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | not set | **Set to `true` behind a TLS-terminating proxy such as App Service.** Without it the app sees every request as plain HTTP from the proxy's address, so HTTPS redirection loops and all callers share one rate-limit bucket |
| `Cors__AllowedOrigins__0`, `__1`, … | none | Browser-based clients: their exact origins, e.g. `https://app.example.com`. **Required for the web app:** its refresh cookie needs CORS with credentials on `/auth/*` (#68), which is only allowed for these origins. Host the web app on the same site as the API (e.g. `app.` and `api.` of one domain) |
| `Jwt__ExpiresMinutes` | 15 | Access-token lifetime |
| `RefreshTokens__ExpiresDays` | 14 | Refresh-token lifetime |
| `RateLimiting__Auth__*` | Sign-in per IP: a burst of 30 (`PerIpBurstLimit`), refilling at 10 per minute (`PerIpPermitLimit` per `PerIpWindowSeconds`); refresh and logout per IP: 300 per minute (`SessionPerIpPermitLimit` per `SessionPerIpWindowSeconds`); 5 logins per email per 5 minutes | Tighten or loosen `/auth/*` throttling. The reasoning is in [architecture](architecture.md) → "Rate limits" |
| `RateLimiting__Anonymous__*` | 60 per IP per minute | Throttling for other anonymous endpoints, such as `/client-config` |
| `ClientConfig__MinimumVersions__Ios`, `__Android`, `__Web` | `1.0.0` | Raise to make older apps show "update required" (`GET /client-config`). Must be `major.minor.patch` |
| `EventRetention__RetentionDays` | 365 | How long audit events are kept |
| `Progress__BaselineWindowRounds` | 5 | Rounds in a baseline |

All of these are checked at startup, and an invalid value stops the app with a message
naming the setting.

## Email

Password reset (#86) sends a link by email, through **Azure Communication Services
Email**. Without `Email__ConnectionString` the email is only written to the log, which
is right on a developer's machine; anywhere else a warning at startup says so.

| Setting | Default | Notes |
|---|---|---|
| `Email__ConnectionString` | empty | The Communication Services resource's connection string. **Secret:** use a Key Vault reference |
| `Email__SenderAddress` | none | Required with the connection string: a sender on a domain verified in the Email Communication Service, e.g. `DoNotReply@golfprogress.app` |
| `PasswordReset__LinkBaseUrl` | `http://localhost:5173/reset-password` | **Set per environment:** the web app's reset page, e.g. `https://staging.golfprogress.app/reset-password`. The token is added as `?token=` |
| `PasswordReset__TokenLifetimeMinutes` | 30 | How long a reset link works |
| `PasswordReset__MaxPerHour` | 3 | Reset emails one account can be sent per hour |

Setting up the provider: create an **Email Communication Service** with a custom domain
(add the DNS records it asks for), connect it to a **Communication Services** resource,
and copy that resource's connection string. The emails go out from a background queue, so
a request never waits on the provider; a failed send is logged without the address.

The mobile apps send users to the same web page to choose the new password. Opening the
link in the apps themselves needs universal links (iOS) and app links (Android) on the
production domain, which come with the store releases (M4).

## Deployment checklist

1. `Jwt__Key` is set: generated as above, unique to this environment, ideally a Key Vault reference.
2. `ConnectionStrings__DefaultConnection` points at the application account or managed identity, never the admin.
3. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` if the app runs behind a proxy.
4. Database migrations are applied with the migration credential (`db/README.md` → "Running migrations").
5. `Email__ConnectionString`, `Email__SenderAddress` and `PasswordReset__LinkBaseUrl` are set (see "Email"), or password reset can't send its link.
6. After deploying, check the log for startup errors, then call any endpoint over HTTPS.

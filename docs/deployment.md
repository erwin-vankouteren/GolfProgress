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

In Azure, the Bicep sets the provider up (see "API"), in two passes, because a domain
can only be used once it's verified:

1. Set `emailDomain` (e.g. `golfprogress.app`) in the parameters and deploy. That creates
   an **Email Communication Service** with the domain and a `DoNotReply` sender ("Golf
   Progress"), and a **Communication Services** resource. The deployment's output
   `emailDnsRecords` lists the DNS records to add: a verification TXT and SPF TXT on the
   domain, and two DKIM CNAMEs. Add a DMARC record too (`_dmarc` TXT `v=DMARC1; p=none`):
   Gmail and Outlook expect one.
2. Verify the domain, each record type in turn, and wait until all four say `Verified`:

   ```powershell
   foreach ($t in 'Domain','SPF','DKIM','DKIM2') { az communication email domain initiate-verification -g <resource-group> --email-service-name golfprogress-email-<env> --domain-name golfprogress.app --verification-type $t }
   az communication email domain show -g <resource-group> --email-service-name golfprogress-email-<env> --domain-name golfprogress.app --query verificationStates
   ```

3. Set `emailSenderAddress` (e.g. `DoNotReply@golfprogress.app`) and deploy again. That
   links the domain, writes the connection string into the vault as
   `email-connection-string` (nobody copies it), and sets the two `Email__*` settings.

The emails go out from a background queue, so a request never waits on the provider; a
failed send is logged without the address.

The mobile apps send users to the same web page to choose the new password. Opening the
link in the apps themselves needs universal links (iOS) and app links (Android) on the
production domain, which come with the store releases (M4).

## Environments

There are two, each in its own Azure resource group and GitHub environment, built from the
same template (`infra/main.bicep`) and deployed by the same workflows:

| | Staging | Production |
|---|---|---|
| Web app / API | `staging.genics.nl` / `api.staging.genics.nl` | `golfprogress.app` / `api.golfprogress.app` |
| Resource group | `rg-golfprogress-staging` | `rg-golfprogress-production` |
| Parameters | `infra/staging.bicepparam` | `infra/production.bicepparam` |
| GitHub environment, variable prefix | `staging`, `STAGING_` | `production`, `PRODUCTION_` |
| Deploys | after CI passes on `main` | the same commit, after staging, **once a reviewer approves** |
| Database | serverless, pauses after an hour idle (the first request after that waits up to a minute) | Standard S0, always on |
| Backups | point-in-time restore, 7 days | point-in-time restore, 35 days, geo-redundant; weekly backups kept 5 weeks, monthly for a year |
| Key Vault | soft delete, 7 days | soft delete, 90 days, purge protection |
| Logs kept | 30 days | 90 days |
| Alerts | none | email when the API stops answering, or returns more than 10 server errors in 5 minutes |

`.github/workflows/api-deploy.yml` and `web-deploy.yml` run a staging job, then a
production job that waits in GitHub until a required reviewer of the `production`
environment approves it (Actions → the run → **Review deployments**). Both call
`api-deploy-environment.yml` / `web-deploy-environment.yml`, which read the environment's
variables by prefix, e.g. `PRODUCTION_API_URL`.

**One site for the web app and the API.** The refresh cookie is `SameSite=Strict`
(architecture → "Authentication"), so the web app and the API must be on the same site:
sibling subdomains of one domain you own, as in the table. The default
`*.azurestaticapps.net` and `*.azurewebsites.net` names are each a site of their own, so
with them sign-in would not survive a reload. Add both custom domains before testing.

**Production DNS** is an Azure DNS zone `golfprogress.app` in `rg-golfprogress-production`
(the domain itself is registered at Spaceship, whose nameservers point at the zone). It
exists because the web app sits on the bare domain, which needs an alias record to the
Static Web App, and Spaceship has none. The zone holds: `@` A (alias to the Static Web
App), `@` TXT (its domain validation), `api` CNAME to
`golfprogress-api-production.azurewebsites.net`, and `asuid.api` TXT (App Service's domain
verification), plus the email records: `@` TXT (email domain verification and SPF), the two
`selector*-azurecomm-prod-net._domainkey` DKIM CNAMEs and `_dmarc` TXT (see "Email"). It
was set up by hand, not by the Bicep.

## Web app

The web app is static files, served by **Azure Static Web Apps** (#116), built against
its environment's API.

**Setup, per environment** (`<env>` is `staging` or `production`, `<PREFIX>` is
`STAGING` or `PRODUCTION`):

1. Create a Static Web App in the environment's resource group (the Free plan is enough
   for staging; Standard for production, for its SLA), source "Other", and add the custom
   domain.
2. In GitHub, on the environment **`<env>`** (Settings → Environments; `production` has
   a required reviewer and allows only `main`):
   - secret `AZURE_STATIC_WEB_APPS_API_TOKEN`: the Static Web App's deployment token;
   - variable `<PREFIX>_API_URL`: the API, e.g. `https://api.golfprogress.app`;
   - variable `<PREFIX>_WEB_URL`: the web app, e.g. `https://golfprogress.app`;
   - variables `PRIVACY_POLICY_URL` and `TERMS_URL` once they're published (#121): one
     pair for both environments, at repository level.
3. The API's `webOrigin` parameter is the web app's origin: it sets CORS and the
   `/reset-password` link.

**What `public/staticwebapp.config.json` does:**

- **Deep links:** any path that isn't a file falls back to `index.html`, so a link to a
  round opens the app on that round.
- **Security headers** on every response: a Content Security Policy that allows scripts,
  styles and fonts from the app itself and calls to the API's origin only (the workflow
  writes `<PREFIX>_API_URL`'s origin in place of `__API_ORIGIN__`), no framing, no
  plugins; `X-Content-Type-Options: nosniff`; `Referrer-Policy:
  strict-origin-when-cross-origin`; a `Permissions-Policy` turning off camera, microphone,
  location and payment; and HSTS. `e2e/security-headers.spec.ts` runs the app under the
  same headers and fails on any policy violation.

Adding crash reporting (#119) means adding its ingest origin to `connect-src`.

## API

The API runs on **Azure App Service**, with an Azure SQL database it signs in to as its own
managed identity (#24), so no environment holds a database password at all (#115).
`infra/main.bicep` describes one environment in one resource group; each deployment
applies migrations, then deploys.

**What the Bicep creates:**

| Resource | Notes |
|---|---|
| App Service plan (Linux, B1) and app `golfprogress-api-<env>` | .NET 8, HTTPS only, FTP off, always on, system-assigned identity, health check on `/client-config`, every setting below |
| Azure SQL server and database `GolfProgressDb` | **Microsoft Entra authentication only**, administered by an Entra group. Size and backups as in "Environments" |
| Key Vault | Holds `jwt-key` (and `email-connection-string`); the app's identity may read secrets and nothing else |
| With `emailDomain` set (production): an Email Communication Service, its domain and `DoNotReply` sender, and a Communication Services resource | Data kept in Europe; no open or click tracking. See "Email" for the two passes |
| Application Insights and a Log Analytics workspace | The API's logs, requests and exceptions; IP addresses are masked |
| With `alertEmail` set (production): an action group, an availability test and two alerts | The test calls `apiUrl` + `/client-config` every 5 minutes from Amsterdam, London and Paris, and checks the certificate has a week left. "API down" fires when two locations fail; "API errors" on more than 10 `5xx` responses in 5 minutes |

The app settings it sets: `ASPNETCORE_ENVIRONMENT` (`Staging` or `Production`),
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, `Jwt__Key` and `Email__ConnectionString` as
Key Vault references, the passwordless connection string, `Cors__AllowedOrigins__0` and
`PasswordReset__LinkBaseUrl` from `webOrigin`, `ClientConfig__MinimumVersions__*` from
`minimumVersions`, and `APPLICATIONINSIGHTS_CONNECTION_STRING`. The API only sends
telemetry when that last one is set, so nothing leaves a developer's machine.

**Setup, once per environment:**

1. **An Entra group for the SQL admins,** e.g. `golfprogress-sql-admins`, with yourself in
   it (both environments may share it). Put its name and object id
   (`az ad group show -g <name> --query id -o tsv`) and the web app's origin in
   `infra/<env>.bicepparam`.
2. **Deploy the infrastructure** (preview it first with `what-if` in place of `create`):

   ```powershell
   az group create --name rg-golfprogress-<env> --location westeurope
   az deployment group create --resource-group rg-golfprogress-<env> --template-file infra/main.bicep --parameters infra/<env>.bicepparam
   ```

   The outputs name the app, its URL, the SQL server and the vault. Running it again is
   safe: it changes only what differs. The availability test fails until the custom
   domain (step 7) is in place.
3. **The JWT key,** generated as above, into the vault: **a different key per
   environment**. Granting yourself **Key Vault Secrets Officer** on the vault comes
   first, because the vault uses Azure RBAC:

   ```powershell
   az keyvault secret set --vault-name <vault> --name jwt-key --value <key>
   ```

4. **The database user for the API's identity,** as an SQL admin group member (db/README.md
   → "Managed identity", step 3), with `AppIdentityName="golfprogress-api-<env>"`. Steps
   1, 2 and 4 there are done by the Bicep.
5. **A deploy identity for GitHub:** an Entra app registration with a federated credential
   for this repository's environment
   (`repo:erwin-vankouteren/GolfProgress:environment:<env>`: the GitHub repository's name,
   which must match exactly), the **Website Contributor** role on the app, and membership
   of the SQL admin group so it can apply migrations. Production may reuse staging's app
   registration with a second federated credential, or have its own.
6. **In GitHub,** on the `<env>` environment:
   - secrets `AZURE_CLIENT_ID` (the app registration), `AZURE_TENANT_ID`,
     `AZURE_SUBSCRIPTION_ID`. These are identifiers, not passwords: OpenID Connect means no
     Azure secret is stored in GitHub;
   - variable `<PREFIX>_API_APP_NAME`: `golfprogress-api-<env>`;
   - variable `<PREFIX>_SQL_SERVER`: the `sqlServer` output;
   - variable `<PREFIX>_API_URL`, shared with the web app's deployment.
7. **A custom domain** for the API on the web app's site, e.g. `api.golfprogress.app`
   (see "Environments" for why), with an App Service managed certificate.

**Each deployment** builds the app and an EF Core migration bundle, applies any pending
migrations as the deploy identity (the API itself can't change the schema), deploys, and
waits for `/client-config` to answer.

**Email** stays off until the domain is verified and `emailSenderAddress` is set in the
parameters (see "Email"); until then reset emails are only logged. Production needs it.

## Restoring the production database

Azure keeps the backups; a restore makes a **new** database next to the old one, which
you then swap in. Practise it once before launch, and after any change to the backups.

1. Restore to a moment before the problem (UTC), as a new database:

   ```powershell
   az sql db restore --resource-group rg-golfprogress-production --server <sql-server-name> --name GolfProgressDb --dest-name GolfProgressDb-restored --time "2026-11-01T09:30:00Z" --service-objective S0
   ```

   Further back than 35 days: list the long-term backups with `az sql db ltr-backup list`
   and restore one with `az sql db ltr-backup restore`.
2. Check the restored data, e.g. a few users' latest rounds.
3. Swap: rename `GolfProgressDb` to `GolfProgressDb-old` and the restored one to
   `GolfProgressDb` (`az sql db rename`), then restart the API. The restored database
   still has the API identity's user, since it's a copy. Delete the old one once you're
   sure.

## Deployment checklist

1. `Jwt__Key` is set: generated as above, unique to this environment, ideally a Key Vault reference.
2. `ConnectionStrings__DefaultConnection` points at the application account or managed identity, never the admin.
3. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` if the app runs behind a proxy.
4. Database migrations are applied with the migration credential (`db/README.md` → "Running migrations").
5. `Email__ConnectionString`, `Email__SenderAddress` and `PasswordReset__LinkBaseUrl` are set (see "Email"), or password reset can't send its link.
6. After deploying, check the log for startup errors, then call any endpoint over HTTPS.
7. Production only: the alert email reaches you (fire a test from the action group), the availability test is green in Application Insights, and a restore has been practised (see "Restoring the production database").

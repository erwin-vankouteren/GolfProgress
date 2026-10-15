# Database access model

The API runs with a **least-privilege contained database user**; schema changes use an
elevated credential. These are deliberately two different accounts.

| Purpose | Account | Rights | Stored as |
|---|---|---|---|
| Running the API (deployed, recommended) | the app's managed identity | `db_datareader`, `db_datawriter` | nothing secret: see [Managed identity](#managed-identity-recommended-in-azure) |
| Running the API (password fallback) | `golfprogress_app` | `db_datareader`, `db_datawriter` | `ConnectionStrings:DefaultConnection` |
| EF migrations | server admin | `db_owner` | `ConnectionStrings:MigrationConnection` |

`golfprogress_app` is a *contained* user: it exists only inside `GolfProgressDb`, has no
server-level login, and cannot reach `master` or any other database. It can read and write
rows and nothing else — no `CREATE TABLE`, no `ALTER`, no `DROP`.

The API never applies migrations at startup (there is no `Database.Migrate()` call), so it
never needs DDL rights.

## One-time setup

All commands are PowerShell. Run from the repository root.

### 1. Create the application user

Pick a password meeting Azure SQL complexity (3 of: upper, lower, digit, symbol), then:

```powershell
$appPw   = Read-Host 'New password for golfprogress_app'
$adminPw = Read-Host 'Server admin password'

sqlcmd -S "tcp:<your-server>.database.windows.net,1433" -d "GolfProgressDb" -U "<admin-user>" -P $adminPw -N -l 60 -v AppUserPassword="$appPw" -i db/create-app-user.sql
```

The script is idempotent: re-running it rotates the user's password rather than failing.
It prints the resulting role membership, which should list `db_datareader` and `db_datawriter`.

### 2. Preserve the admin string for migrations

Before overwriting `DefaultConnection`, copy the current (admin) value to
`MigrationConnection`:

```powershell
$admin = (dotnet user-secrets list --project src\GolfProgress.Api | Select-String '^ConnectionStrings:DefaultConnection = ').ToString() -replace '^ConnectionStrings:DefaultConnection = ', ''
dotnet user-secrets set "ConnectionStrings:MigrationConnection" $admin --project src\GolfProgress.Api
```

### 3. Point the API at the application user

```powershell
$cs = "Server=tcp:<your-server>.database.windows.net,1433;Initial Catalog=GolfProgressDb;Persist Security Info=False;User ID=golfprogress_app;Password=$appPw;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" $cs --project src\GolfProgress.Api
Remove-Variable appPw, adminPw, cs, admin
```

If the password contains `;`, `=` or `'`, wrap it in single quotes inside the connection
string (`Password='$appPw'`) or ADO.NET will truncate at the separator.

### 4. Verify

The app user should be able to read:

```powershell
dotnet ef migrations list --project src\GolfProgress.Infrastructure --startup-project src\GolfProgress.Api
```

## Rotating the `golfprogress_app` password

Rotate it on a schedule, whenever someone who knew it leaves, or whenever it may have
leaked. `create-app-user.sql` is idempotent: run against an existing user, it **resets the
password** instead of failing, so rotation is the setup procedure run again.

### Where the password lives

Only in the connection strings that use it:

- **locally:** your user-secrets, in
  `%APPDATA%\Microsoft\UserSecrets\84eb63b2-1208-46e7-9f80-1b038879c952\secrets.json`;
- **deployed:** the `ConnectionStrings__DefaultConnection` app setting or Key Vault secret
  of each environment that uses this account.

Nothing else records it. SQL Server stores only a hash, so the password **cannot be
recovered** from the database.

### Steps

1. **Set a new password in the database.** Use the same command as in setup step 1, with
   a new password:

   ```powershell
   $appPw   = Read-Host 'New password for golfprogress_app'
   $adminPw = Read-Host 'Server admin password'

   sqlcmd -S "tcp:<your-server>.database.windows.net,1433" -d "GolfProgressDb" -U "<admin-user>" -P $adminPw -N -l 60 -v AppUserPassword="$appPw" -i db/create-app-user.sql
   ```

   It prints `User golfprogress_app already existed; password rotated.` The old password
   stops working **immediately** for new connections.

2. **Update every connection string that uses the account, straight away.** Until you do,
   a running app keeps its already-open pooled connections but fails to open new ones.
   - **Locally:** set the user-secret again, as in setup step 3.
   - **Deployed:** update the app setting or Key Vault secret. Changing an app setting
     restarts the app; after updating a Key Vault secret, restart the app so it picks up
     the new version.

3. **Verify.** Locally, the `migrations list` command from step 4 should succeed. Deployed,
   call any endpoint that reads data, and check the log for login failures.

4. **Clean up:** `Remove-Variable appPw, adminPw`, and delete any other copy of the old password.

There is a short window between steps 1 and 2 in which new database connections fail, so
rotate at a quiet time. To have no window at all, and no password to rotate, use a
[managed identity](#managed-identity-recommended-in-azure) instead.

### If the password has been lost

For example, after user-secrets were deleted or the machine that held them was replaced.
There is nothing to recover: follow the rotation steps above. Re-running the script with
a new password replaces the lost one, and the user's roles are unchanged.

## Running migrations

Migrations need the elevated credential. Environment variables override user-secrets, so
override `DefaultConnection` for the duration of the command:

```powershell
$env:ConnectionStrings__DefaultConnection = (dotnet user-secrets list --project src\GolfProgress.Api | Select-String '^ConnectionStrings:MigrationConnection = ').ToString() -replace '^ConnectionStrings:MigrationConnection = ', ''

dotnet ef database update --project src\GolfProgress.Infrastructure --startup-project src\GolfProgress.Api

Remove-Item Env:\ConnectionStrings__DefaultConnection
```

Forgetting the override produces a permission error on `CREATE TABLE` / `ALTER TABLE` —
that is the safety net working, not a bug.

## Deployment

Neither credential belongs in `appsettings.json`. Supply them as environment variables
(`ConnectionStrings__DefaultConnection`, `Jwt__Key`) or from Key Vault. Startup validates
both and fails fast with a readable message if either is missing or too weak.
[`docs/deployment.md`](../docs/deployment.md) covers supplying them, and every other
setting a deployed environment needs.

In Azure, prefer a **managed identity** over the `golfprogress_app` password; see below.

## Managed identity (recommended in Azure)

With a managed identity, the API signs in to Azure SQL as itself through Microsoft Entra ID.
The connection string carries no password, so there is nothing to store, rotate or leak.
No code change is needed: `Microsoft.Data.SqlClient` picks the authentication method from
the connection string.

The database user gets exactly the same rights as `golfprogress_app` (`db_datareader` and
`db_datawriter`), so the least-privilege model above is unchanged.

### 1. Give the SQL server a Microsoft Entra admin

Only an Entra principal can create Entra-backed database users; the SQL-authenticated
server admin cannot. If the server has no Entra admin yet, make yourself (or a group you are
in) the admin:

```powershell
$me = az ad signed-in-user show --query id -o tsv
az sql server ad-admin create --resource-group <rg> --server-name <your-server> --display-name "<your-name>" --object-id $me
```

### 2. Turn on the app's managed identity

For an App Service (system-assigned identity, named after the app):

```powershell
az webapp identity assign --resource-group <rg> --name <app-name>
```

### 3. Create the database user for the identity

Signed in with `az login` as the Entra admin, run against `GolfProgressDb` (not `master`).
This uses the modern `sqlcmd` (`winget install sqlcmd`):

```powershell
sqlcmd -S "tcp:<your-server>.database.windows.net,1433" -d "GolfProgressDb" --authentication-method ActiveDirectoryDefault -N -l 60 -v AppIdentityName="<app-name>" -i db/create-app-identity-user.sql
```

The script is idempotent. It prints the role membership, which should list
`db_datareader` and `db_datawriter` with type `EXTERNAL_USER`.

### 4. Point the API at the identity

Set the app's connection string. It contains no secret:

```powershell
$cs = "Server=tcp:<your-server>.database.windows.net,1433;Initial Catalog=GolfProgressDb;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
az webapp config appsettings set --resource-group <rg> --name <app-name> --settings "ConnectionStrings__DefaultConnection=$cs"
```

For a **user-assigned** identity, add `User Id=<identity-client-id>;` so the app picks the
right one.

### 5. Retire the password

Once the API is confirmed working on the identity, remove the old credential so it can no
longer be used:

```sql
DROP USER [golfprogress_app];
```

Then delete any copies of its password, in app settings, Key Vault or elsewhere.

### Local development

Locally there is no managed identity. Either keep using `golfprogress_app` through
user-secrets as described above, or go passwordless with your own Entra account: create a
database user for yourself with the same script (`AppIdentityName` set to your Entra
sign-in name) and use `Authentication=Active Directory Default`, which signs in with your
`az login` session.

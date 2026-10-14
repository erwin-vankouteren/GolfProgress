# Database access model

The API runs with a **least-privilege contained database user**; schema changes use an
elevated credential. These are deliberately two different accounts.

| Purpose | Account | Rights | Stored as |
|---|---|---|---|
| Running the API | `golfprogress_app` | `db_datareader`, `db_datawriter` | `ConnectionStrings:DefaultConnection` |
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

Longer term, prefer a **managed identity** over a password for the application user, which
removes this credential from configuration entirely.

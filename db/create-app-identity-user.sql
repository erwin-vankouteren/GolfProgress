/*
    Creates the least-privilege application user for GolfProgress, authenticated
    by the app's Microsoft Entra managed identity instead of a password (#24).

    Run against the GolfProgressDb database (NOT master), signed in as the server's
    Microsoft Entra admin. A SQL-authenticated admin cannot create Entra users.
    See db/README.md for the full PowerShell walkthrough. In short, using the
    modern sqlcmd (winget install sqlcmd):

        sqlcmd -S "tcp:<server>.database.windows.net,1433" -d GolfProgressDb --authentication-method ActiveDirectoryDefault -N -l 60 -v AppIdentityName="<app-or-identity-name>" -i db/create-app-identity-user.sql

    AppIdentityName is the name of the managed identity: for a system-assigned
    identity that is the App Service (or Container App) name; for a user-assigned
    identity it is the identity resource's name.

    Like create-app-user.sql this is a CONTAINED database user with only
    db_datareader + db_datawriter, but it has no password at all: there is no
    credential to store, rotate or leak. Re-running the script is safe.
*/

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppIdentityName)')
BEGIN
    CREATE USER [$(AppIdentityName)] FROM EXTERNAL PROVIDER;
    PRINT 'Created user $(AppIdentityName) from Microsoft Entra ID.';
END
ELSE
BEGIN
    PRINT 'User $(AppIdentityName) already existed.';
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datareader' AND m.name = N'$(AppIdentityName)')
BEGIN
    ALTER ROLE [db_datareader] ADD MEMBER [$(AppIdentityName)];
    PRINT 'Granted db_datareader.';
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datawriter' AND m.name = N'$(AppIdentityName)')
BEGIN
    ALTER ROLE [db_datawriter] ADD MEMBER [$(AppIdentityName)];
    PRINT 'Granted db_datawriter.';
END

-- Confirm the user's type and effective role membership. type_desc should be
-- EXTERNAL_USER: authenticated by Entra ID, with no password.
SELECT m.name AS [user], m.type_desc, r.name AS [role]
FROM sys.database_role_members rm
JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
WHERE m.name = N'$(AppIdentityName)'
ORDER BY r.name;

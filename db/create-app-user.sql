/*
    Creates the least-privilege application user for GolfProgress.

    Run against the GolfProgressDb database (NOT master) as the server admin.
    See db/README.md for the full PowerShell walkthrough. In short:

        sqlcmd -S "tcp:<server>.database.windows.net,1433" -d GolfProgressDb -U "<admin>" -P "<adminPassword>" -N -l 60 -v AppUserPassword="<newAppPassword>" -i db/create-app-user.sql

    This is a CONTAINED database user: it lives inside GolfProgressDb and has no
    server-level login, so it cannot touch master or any other database.

    It deliberately gets only db_datareader + db_datawriter. That is everything the
    running API needs (it issues SELECT/INSERT/UPDATE/DELETE and no DDL at runtime).
    Schema changes are applied by EF migrations using an elevated credential --
    see db/README.md.
*/

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'golfprogress_app')
BEGIN
    CREATE USER [golfprogress_app] WITH PASSWORD = N'$(AppUserPassword)';
    PRINT 'Created user golfprogress_app.';
END
ELSE
BEGIN
    ALTER USER [golfprogress_app] WITH PASSWORD = N'$(AppUserPassword)';
    PRINT 'User golfprogress_app already existed; password rotated.';
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datareader' AND m.name = N'golfprogress_app')
BEGIN
    ALTER ROLE [db_datareader] ADD MEMBER [golfprogress_app];
    PRINT 'Granted db_datareader.';
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datawriter' AND m.name = N'golfprogress_app')
BEGIN
    ALTER ROLE [db_datawriter] ADD MEMBER [golfprogress_app];
    PRINT 'Granted db_datawriter.';
END

-- Confirm the effective role membership.
SELECT m.name AS [user], r.name AS [role]
FROM sys.database_role_members rm
JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
WHERE m.name = N'golfprogress_app'
ORDER BY r.name;

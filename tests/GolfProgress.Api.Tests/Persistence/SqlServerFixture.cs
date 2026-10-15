using GolfProgress.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace GolfProgress.Api.Tests.Persistence;

/// <summary>
/// Whether the SQL Server tests can run here (#33).
///
/// They need Docker. In CI (GitHub sets CI=true) they always run, so a missing
/// Docker fails loudly instead of quietly skipping the only coverage of real SQL
/// Server behaviour. Locally they are skipped when no Docker engine is found.
/// </summary>
public static class SqlServerAvailability
{
    public static bool ShouldRun { get; } = IsCi() || DockerEngineFound();

    private static bool IsCi() =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    private static bool DockerEngineFound()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
            return true;

        if (!OperatingSystem.IsWindows())
            return File.Exists("/var/run/docker.sock");

        try
        {
            return Directory.GetFiles(@"\\.\pipe\")
                .Any(p => p.EndsWith("docker_engine", StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return false;
        }
    }
}

/// <summary>A fact that runs only where <see cref="SqlServerAvailability.ShouldRun"/>.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerAvailability.ShouldRun)
            Skip = "Needs Docker for a SQL Server container. Runs in CI.";
    }
}

/// <summary>
/// One SQL Server container for the whole persistence collection, with the real
/// EF migrations applied. Tests isolate themselves by using fresh ids rather than
/// a fresh database, which keeps the collection to a single container start.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Pinned so CI always tests against the same engine version.
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        if (!SqlServerAvailability.ShouldRun) return;

        _container = new MsSqlBuilder().WithImage(Image).Build();
        await _container.StartAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "GolfProgressTests"
        }.ConnectionString;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public GolfProgressDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GolfProgressDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}

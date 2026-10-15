using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Boots the real API pipeline (routing, authentication, authorization, controllers)
/// against an in-memory store, so tests exercise the genuine request path rather than
/// calling controller methods directly. Each factory instance gets its own database.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // 64 bytes: comfortably over the 32-byte floor Program.cs enforces for HS256.
    public const string JwtKey = "dGVzdC1vbmx5LXNpZ25pbmcta2V5LWZvci1nb2xmcHJvZ3Jlc3MtaW50ZWdyYXRpb24tdGVzdHM=";
    public const string JwtIssuer = "GolfProgress.Api";
    public const string JwtAudience = "GolfProgress.Mobile";

    /// <summary>
    /// Program.cs validates Jwt options and the connection string while the builder is
    /// still being configured -- before WebApplicationFactory's ConfigureAppConfiguration
    /// callbacks run. Environment variables are read by WebApplication.CreateBuilder
    /// itself, so they are the only injection point early enough to satisfy those guards.
    /// The connection string is never used: the provider is replaced in ConfigureServices.
    /// </summary>
    static ApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection", "Server=not-used-by-tests;Database=none;");
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtAudience);
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
        Environment.SetEnvironmentVariable("Jwt__ExpiresMinutes", "15");
        Environment.SetEnvironmentVariable("RefreshTokens__ExpiresDays", "14");
    }

    private readonly string _databaseName = $"golfprogress-tests-{Guid.NewGuid()}";
    private readonly IInterceptor[] _interceptors;

    /// <param name="interceptors">EF interceptors added to the app's DbContext, e.g. to inject failures.</param>
    public ApiFactory(params IInterceptor[] interceptors) => _interceptors = interceptors;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<GolfProgressDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(GolfProgressDbContext))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<GolfProgressDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName).AddInterceptors(_interceptors));
        });
    }

    /// <summary>Runs an async action against a fresh scoped DbContext.</summary>
    public async Task WithDbAsync(Func<GolfProgressDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GolfProgressDbContext>();
        await action(db);
    }

    /// <summary>Runs a synchronous assertion against a fresh scoped DbContext.</summary>
    public void WithDb(Action<GolfProgressDbContext> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GolfProgressDbContext>();
        action(db);
    }
}

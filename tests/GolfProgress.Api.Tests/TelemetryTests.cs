using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #115: with an Application Insights connection string, as on staging, the API
/// registers telemetry and still starts and serves; without one, nothing is registered.
/// </summary>
public sealed class TelemetryTests
{
    private const string Dummy = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost:1/";

    [Fact]
    public async Task WithAConnectionString_TelemetryIsOn_AndTheApiServes()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(b => b.UseSetting("APPLICATIONINSIGHTS_CONNECTION_STRING", Dummy));

        var response = await factory.CreateClient().GetAsync("/client-config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(factory.Services.GetServices<IHostedService>(), s => s.GetType().FullName!.Contains("ApplicationInsights", StringComparison.OrdinalIgnoreCase)
            || s.GetType().Assembly.GetName().Name!.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WithoutOne_NoTelemetryIsRegistered()
    {
        using var factory = new ApiFactory();
        _ = factory.CreateClient();

        Assert.DoesNotContain(factory.Services.GetServices<IHostedService>(), s => s.GetType().FullName!.Contains("ApplicationInsights", StringComparison.OrdinalIgnoreCase)
            || s.GetType().Assembly.GetName().Name!.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase));
    }
}

using GolfProgress.Api.Configuration;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Identity;
using GolfProgress.Domain.Entities;
using GolfProgress.Api.Caching;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.OpenApi;
using GolfProgress.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
// The "v1" document is also written to api/openapi.json, the contract the clients are
// generated from (#61); OpenApiConfiguration makes it complete enough for typed
// clients (#62). Regenerate it with api/generate-openapi.ps1.
builder.Services.AddSwaggerGen(OpenApiConfiguration.Configure);
builder.Services.AddControllers();

// Errors are returned as RFC 7807 ProblemDetails (#7): NotFoundException as 404,
// anything unhandled as a 500 that does not leak exception details.
builder.Services.AddProblemDetails(options =>
    // Lets a client-reported error be matched to its log entry.
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions.TryAdd(
            "traceId", Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// ---------- Configuration (validated at startup, so misconfiguration fails fast
// with a readable message instead of an obscure crash on first request)

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        o => Encoding.UTF8.GetByteCount(o.Key) >= JwtOptions.MinimumKeyBytes,
        $"Jwt:Key must be at least {JwtOptions.MinimumKeyBytes} bytes for HMAC-SHA256. " +
        "Supply it via user-secrets, the Jwt__Key environment variable, or Key Vault — never appsettings.json.")
    .ValidateOnStart();

builder.Services.AddOptions<RefreshTokenOptions>()
    .Bind(builder.Configuration.GetSection(RefreshTokenOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ProgressOptions>()
    .Bind(builder.Configuration.GetSection(ProgressOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EventRetentionOptions>()
    .Bind(builder.Configuration.GetSection(EventRetentionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ClientCorsOptions>()
    .Bind(builder.Configuration.GetSection(ClientCorsOptions.SectionName))
    .Validate(
        o => o.AllowedOrigins.All(ClientCorsOptions.IsValidOrigin),
        "Cors:AllowedOrigins must be exact origins such as https://app.example.com " +
        "(scheme and host, optional port; no '*', path or trailing slash).")
    .ValidateOnStart();

// CORS (#16): one default policy built from Cors:AllowedOrigins. With no origins
// configured -- the default -- no CORS headers are sent and browsers stay blocked.
// Bearer tokens travel in a header, not a cookie, so credentials are not allowed.
builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
    .Configure<IOptions<ClientCorsOptions>>((cors, client) =>
        cors.AddDefaultPolicy(policy => policy
            .WithOrigins(client.Value.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(ClientCorsOptions.ExposedHeaders)));

builder.Services.AddOptions<AuthRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(AuthRateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Supply it via user-secrets, " +
        "the ConnectionStrings__DefaultConnection environment variable, or Key Vault.");
}

// Password hashing (recommended instead of low-level PBKDF2 for new apps)
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// JWT auth. The signing key is read from the same validated options the token
// service uses, so issuing and validating can never drift apart.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
        };
    });
builder.Services.AddAuthorization();

// Rate limiting for the anonymous /auth/* endpoints (#6). Per client IP here; per
// email for login in LoginThrottle, since this middleware cannot see the body.
//
// Behind a reverse proxy RemoteIpAddress is the proxy's, which would put every
// caller in one partition. Enable forwarded headers for the trusted proxy there
// (on App Service: ASPNETCORE_FORWARDEDHEADERS_ENABLED=true).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Auth, context =>
    {
        var o = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = o.PerIpPermitLimit,
                Window = TimeSpan.FromSeconds(o.PerIpWindowSeconds),
                QueueLimit = 0
            });
    });

    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

        return ValueTask.CompletedTask;
    };
});
builder.Services.AddSingleton<LoginThrottle>();

// Per-user response cache, invalidated on every save that touches a user's data (#14).
builder.Services.AddSingleton<UserScopedCache>();
builder.Services.AddSingleton<CacheInvalidationInterceptor>();

builder.Services.AddDbContext<GolfProgressDbContext>((sp, options) =>
    options.UseSqlServer(connectionString,
    sqloptions =>
    {
        sqloptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    })
    .AddInterceptors(sp.GetRequiredService<CacheInvalidationInterceptor>()));

builder.Services.AddScoped<GolfProgress.Api.Services.ProgressComputationService>();
builder.Services.AddScoped<GolfProgress.Api.Services.MicroWinsService>();
builder.Services.AddScoped<GolfProgress.Api.Services.NextLeverService>();
builder.Services.AddScoped<GolfProgress.Api.Services.MonthlyReviewService>();
builder.Services.AddScoped<GolfProgress.Api.Services.TimelineService>();
builder.Services.AddScoped<GolfProgress.Api.Services.DashboardService>();
builder.Services.AddScoped<GolfProgress.Api.Services.JwtTokenService>();
builder.Services.AddScoped<GolfProgress.Api.Services.RefreshTokenService>();
builder.Services.AddMemoryCache();

// Audit-event retention (#20): purges old Events rows on a timer.
builder.Services.AddScoped<GolfProgress.Api.Services.EventRetentionService>();
builder.Services.AddHostedService<GolfProgress.Api.Services.EventRetentionWorker>();

var app = builder.Build();

// First in the pipeline, so it catches exceptions from everything after it (#7).
app.UseExceptionHandler();

// HTTPS (#16). HSTS tells browsers to use HTTPS from then on; it is skipped in
// Development and for localhost. Redirection sends plain-HTTP requests to HTTPS,
// and does nothing when no HTTPS port is known (as under the test host).
//
// Behind a TLS-terminating proxy (App Service, a load balancer) the app sees plain
// HTTP unless forwarded headers are applied, which would make every request
// redirect. Enable them for the trusted proxy there
// (on App Service: ASPNETCORE_FORWARDEDHEADERS_ENABLED=true).
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseHttpsRedirection();

// Before rate limiting and authentication, so CORS preflight requests are
// answered without counting against the /auth limits or needing a token.
app.UseCors();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();

// Exposed so WebApplicationFactory<Program> can reference the entry point;
// top-level statements otherwise generate an internal Program class.
public partial class Program { }

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
    {
        cors.AddDefaultPolicy(policy => policy
            .WithOrigins(client.Value.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(ClientCorsOptions.ExposedHeaders));

        // /auth/* only (#68): the same origins may also send credentials, so the web
        // app's HttpOnly refresh cookie travels with its sign-in requests.
        cors.AddPolicy(CorsPolicies.AuthWithCredentials, policy => policy
            .WithOrigins(client.Value.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders(ClientCorsOptions.ExposedHeaders));
    });

builder.Services.AddOptions<ClientConfigOptions>()
    .Bind(builder.Configuration.GetSection(ClientConfigOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        o => new[] { o.MinimumVersions.Ios, o.MinimumVersions.Android, o.MinimumVersions.Web }
            .All(v => System.Text.RegularExpressions.Regex.IsMatch(v ?? "", MinimumVersionOptions.Pattern)),
        "ClientConfig:MinimumVersions:Ios, :Android and :Web must each be a version such as 1.0.0 (major.minor.patch).")
    .ValidateOnStart();

builder.Services.AddOptions<AnonymousRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(AnonymousRateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

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
        // A deleted account's tokens stop working at once, not when they expire (#84).
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ActiveUserCheck.OnTokenValidated
        };
    });
builder.Services.AddAuthorization();

// Rate limiting for the anonymous /auth/* endpoints (#6). Per client IP here; per
// email for login in LoginThrottle, since this middleware cannot see the body. Sized
// for many real users behind one carrier or Wi-Fi IP (#85): see docs/architecture.md,
// "Rate limits".
//
// Behind a reverse proxy RemoteIpAddress is the proxy's, which would put every
// caller in one partition. Enable forwarded headers for the trusted proxy there
// (on App Service: ASPNETCORE_FORWARDEDHEADERS_ENABLED=true).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Signing in: a token bucket, so a burst gets through after a quiet spell while
    // the sustained rate stays PerIpPermitLimit per window, refilled one at a time.
    options.AddPolicy(RateLimitPolicies.Auth, context =>
    {
        var o = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

        return RateLimitPartition.GetTokenBucketLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = o.PerIpBurstLimit,
                TokensPerPeriod = 1,
                ReplenishmentPeriod = TimeSpan.FromSeconds((double)o.PerIpWindowSeconds / o.PerIpPermitLimit),
                AutoReplenishment = true,
                QueueLimit = 0
            });
    });

    // Keeping a session: refresh and logout, separately and far more generously.
    options.AddPolicy(RateLimitPolicies.AuthSession, context =>
    {
        var o = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = o.SessionPerIpPermitLimit,
                Window = TimeSpan.FromSeconds(o.SessionPerIpWindowSeconds),
                QueueLimit = 0
            });
    });

    options.AddPolicy(RateLimitPolicies.Anonymous, context =>
    {
        var o = context.RequestServices.GetRequiredService<IOptions<AnonymousRateLimitOptions>>().Value;

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
builder.Services.AddScoped<GolfProgress.Api.Services.AccountDeletionService>();
builder.Services.AddMemoryCache();

// API logs and telemetry in Application Insights (#115), sent only when
// APPLICATIONINSIGHTS_CONNECTION_STRING is set, as on staging; locally and in tests
// nothing leaves the machine.
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    builder.Services.AddApplicationInsightsTelemetry();

// Audit-event retention (#20): purges old Events rows on a timer.
builder.Services.AddScoped<GolfProgress.Api.Services.EventRetentionService>();
builder.Services.AddHostedService<GolfProgress.Api.Services.EventRetentionWorker>();

// Password reset by email (#86). Emails leave through a background outbox, sent with
// Azure Communication Services when Email:ConnectionString is set, otherwise logged.
builder.Services.AddOptions<GolfProgress.Api.Email.EmailOptions>()
    .Bind(builder.Configuration.GetSection(GolfProgress.Api.Email.EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<GolfProgress.Api.Services.PasswordResetOptions>()
    .Bind(builder.Configuration.GetSection(GolfProgress.Api.Services.PasswordResetOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
var emailConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Email:ConnectionString"]);
if (emailConfigured)
    builder.Services.AddSingleton<GolfProgress.Api.Email.IEmailSender, GolfProgress.Api.Email.AzureCommunicationEmailSender>();
else
    builder.Services.AddSingleton<GolfProgress.Api.Email.IEmailSender, GolfProgress.Api.Email.LoggingEmailSender>();
builder.Services.AddSingleton<GolfProgress.Api.Email.ChannelEmailOutbox>();
builder.Services.AddSingleton<GolfProgress.Api.Email.IEmailOutbox>(sp => sp.GetRequiredService<GolfProgress.Api.Email.ChannelEmailOutbox>());
builder.Services.AddHostedService<GolfProgress.Api.Email.EmailSendingWorker>();
builder.Services.AddScoped<GolfProgress.Api.Services.PasswordResetService>();

var app = builder.Build();

if (!emailConfigured && !app.Environment.IsDevelopment())
    app.Logger.LogWarning("Email:ConnectionString is not set: password reset emails are logged, not sent. See docs/deployment.md, \"Email\".");

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

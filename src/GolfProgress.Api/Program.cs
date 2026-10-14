using GolfProgress.Api.Configuration;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Identity;
using GolfProgress.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

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

builder.Services.AddDbContext<GolfProgressDbContext>(options =>
    options.UseSqlServer(connectionString,
    sqloptions =>
    {
        sqloptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

builder.Services.AddScoped<GolfProgress.Api.Services.ProgressComputationService>();
builder.Services.AddScoped<GolfProgress.Api.Services.MicroWinsService>();
builder.Services.AddScoped<GolfProgress.Api.Services.NextLeverService>();
builder.Services.AddScoped<GolfProgress.Api.Services.MonthlyReviewService>();
builder.Services.AddScoped<GolfProgress.Api.Services.TimelineService>();
builder.Services.AddScoped<GolfProgress.Api.Services.DashboardService>();
builder.Services.AddScoped<GolfProgress.Api.Services.JwtTokenService>();
builder.Services.AddScoped<GolfProgress.Api.Services.RefreshTokenService>();
builder.Services.AddMemoryCache();

var app = builder.Build();
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

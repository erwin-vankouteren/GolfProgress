using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Identity;
using GolfProgress.Domain.Entities;
using Microsoft.AspNetCore.Authentication;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// Password hashing (recommended instead of low-level PBKDF2 for new apps) [2](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-10.0)[3](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1?view=aspnetcore-10.0)
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// JWT auth (recommended for API auth) [1](https://learn.microsoft.com/en-us/azure/azure-sql/database/azure-sql-dotnet-quickstart?view=azuresql)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddDbContext<GolfProgressDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
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


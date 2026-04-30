using GolfProgress.Domain.Entities;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace GolfProgress.Api.Services;

public sealed class RefreshTokenService
{
    private readonly GolfProgressDbContext _db;
    private readonly IConfiguration _config;

    public RefreshTokenService(GolfProgressDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public (string rawToken, string tokenHash) GenerateToken()
    {
        // 64 bytes => strong entropy
        var bytes = RandomNumberGenerator.GetBytes(64);
        var raw = Base64UrlEncode(bytes);
        var hash = Sha256Base64(raw);
        return (raw, hash);
    }

    public async Task<RefreshToken> StoreAsync(Guid userId, string tokenHash, CancellationToken ct)
    {
        var days = int.Parse(_config["RefreshTokens:ExpiresDays"] ?? "14");
        var now = DateTimeOffset.UtcNow;

        var rt = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(days)
        };

        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct);
        return rt;
    }

    public async Task<RefreshToken?> FindValidAsync(string rawToken, CancellationToken ct)
    {
        var hash = Sha256Base64(rawToken);
        var now = DateTimeOffset.UtcNow;

        return await _db.RefreshTokens
            .FirstOrDefaultAsync(x =>
                x.TokenHash == hash &&
                x.RevokedAt == null &&
                x.ExpiresAt > now, ct);
    }

    public async Task RotateAsync(RefreshToken oldToken, string newTokenHash, CancellationToken ct)
    {
        oldToken.RevokedAt = DateTimeOffset.UtcNow;
        oldToken.ReplacedByTokenHash = newTokenHash;
        oldToken.ReasonRevoked = "Rotated";

        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(RefreshToken token, string reason, CancellationToken ct)
    {
        token.RevokedAt = DateTimeOffset.UtcNow;
        token.ReasonRevoked = reason;
        await _db.SaveChangesAsync(ct);
    }

    private static string Sha256Base64(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
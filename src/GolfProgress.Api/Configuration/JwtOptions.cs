using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 requires a key of at least 256 bits, so 32 UTF-8 bytes is the floor.
    public const int MinimumKeyBytes = 32;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = "";

    // Never comes from appsettings.json — supplied by user-secrets, an environment
    // variable (Jwt__Key) or Key Vault.
    [Required(AllowEmptyStrings = false)]
    public string Key { get; set; } = "";

    [Range(1, 1440)]
    public int ExpiresMinutes { get; set; } = 15;
}

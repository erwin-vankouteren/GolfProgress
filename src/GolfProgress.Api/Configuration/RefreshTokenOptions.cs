using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshTokens";

    [Range(1, 365)]
    public int ExpiresDays { get; set; } = 14;
}

using GolfProgress.Api.Localization;
using GolfProgress.Api.OpenApi;
using GolfProgress.Domain.Entities;
using Enums = GolfProgress.Domain.Enums;
namespace GolfProgress.Api.Contracts.MicroWins;

public sealed class MicroWinResponse
{
    public Guid MicroWinId { get; init; }
    public Guid RoundId { get; init; }
    public Guid UserId { get; init; }

    [StringEnum(typeof(Enums.MicroWinCategory), Open = true)]
    public string Category { get; init; } = "";
    public string? Metric { get; init; }
    [StringEnum(typeof(Enums.MicroWinDirection))]
    public string Direction { get; init; } = "";
    public decimal? DeltaValue { get; init; }

    [StringEnum(typeof(Enums.ConfidenceLevel))]
    public string ConfidenceLevel { get; init; } = "Low";
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>A stored micro-win, with its title and message in the request's language.</summary>
    public static MicroWinResponse From(MicroWin mw)
    {
        var (title, message) = MicroWinTexts.For(mw, ApiTexts.Current);
        return new MicroWinResponse
        {
            MicroWinId = mw.MicroWinId,
            UserId = mw.UserId,
            RoundId = mw.RoundId,
            Category = mw.Category.ToString(),
            Metric = mw.Metric,
            Direction = mw.Direction.ToString(),
            DeltaValue = mw.DeltaValue,
            ConfidenceLevel = mw.ConfidenceLevel.ToString(),
            Title = title,
            Message = message,
            CreatedAt = mw.CreatedAt
        };
    }
}
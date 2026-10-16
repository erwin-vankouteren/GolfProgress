using GolfProgress.Api.OpenApi;
using GolfProgress.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using Enums = GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Contracts.Users;

/// <summary>
/// PATCH /me (#82). Every field is optional: a field left out of the body is
/// unchanged. Timezone, handicapIndex and typical18Score are cleared by sending
/// null; the other fields can't be null.
///
/// System.Text.Json only calls a setter for a property that is in the JSON, so each
/// setter records that its field was sent. That is how a field sent as null is told
/// apart from one left out.
/// </summary>
public sealed class UpdateMeRequest : IValidatableObject
{
    public const int DisplayNameMaxLength = 100;
    public const int TimezoneMaxLength = 64;
    public const int MaxGoalFocusAreas = 3;

    private readonly HashSet<string> _sent = new();

    private string? _displayName;
    private string? _timezone;
    private string? _units;
    private string? _trackingMode;
    private List<string>? _goalFocus;
    private decimal? _handicapIndex;
    private int? _typical18Score;

    /// <summary>Whether the body included <paramref name="property"/>, even as null.</summary>
    public bool Sent(string property) => _sent.Contains(property);

    [MaxLength(DisplayNameMaxLength)]
    public string? DisplayName { get => _displayName; init => Set(ref _displayName, value); }

    /// <summary>An IANA time zone, e.g. Europe/Amsterdam.</summary>
    [MaxLength(TimezoneMaxLength)]
    public string? Timezone { get => _timezone; init => Set(ref _timezone, value); }

    [StringEnum(typeof(Enums.Units))]
    public string? Units { get => _units; init => Set(ref _units, value); }

    [StringEnum(typeof(Enums.TrackingMode))]
    public string? TrackingMode { get => _trackingMode; init => Set(ref _trackingMode, value); }

    [StringEnum(typeof(GoalFocusArea))]
    public List<string>? GoalFocus { get => _goalFocus; init => Set(ref _goalFocus, value); }

    [Range(typeof(decimal), "-10", "54", ErrorMessage = "HandicapIndex must be between -10 and 54.")]
    public decimal? HandicapIndex { get => _handicapIndex; init => Set(ref _handicapIndex, value); }

    [Range(18, 200, ErrorMessage = "Typical18Score must be between 18 and 200.")]
    public int? Typical18Score { get => _typical18Score; init => Set(ref _typical18Score, value); }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        field = value;
        _sent.Add(property);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // These are nullable only so that MVC doesn't treat them as [Required]; a field
        // that can't be cleared is rejected here when it's sent as null.
        if (Sent(nameof(DisplayName)) && string.IsNullOrWhiteSpace(DisplayName))
            yield return Error(nameof(DisplayName), "DisplayName can't be empty.");

        if (Sent(nameof(Timezone)) && Timezone is not null && !IsIanaTimezone(Timezone))
            yield return Error(nameof(Timezone), "Timezone must be an IANA time zone, e.g. Europe/Amsterdam.");

        if (Sent(nameof(Units)) && !TryParseName<Enums.Units>(Units, out _))
            yield return Error(nameof(Units), $"Units must be one of: {string.Join(", ", Enum.GetNames<Enums.Units>())}.");

        if (Sent(nameof(TrackingMode)) && !TryParseName<Enums.TrackingMode>(TrackingMode, out _))
            yield return Error(nameof(TrackingMode),
                $"TrackingMode must be one of: {string.Join(", ", Enum.GetNames<Enums.TrackingMode>())}.");

        if (Sent(nameof(GoalFocus)))
        {
            if (GoalFocus is null)
                yield return Error(nameof(GoalFocus), "GoalFocus can't be null; send [] to clear it.");
            else if (GoalFocus.Any(f => !TryParseName<GoalFocusArea>(f, out _)))
                yield return Error(nameof(GoalFocus),
                    $"GoalFocus areas must be among: {string.Join(", ", Enum.GetNames<GoalFocusArea>())}.");
            else if (GoalFocus.Distinct(StringComparer.OrdinalIgnoreCase).Count() != GoalFocus.Count)
                yield return Error(nameof(GoalFocus), "GoalFocus can't list an area twice.");
            else if (GoalFocus.Count > MaxGoalFocusAreas)
                yield return Error(nameof(GoalFocus), $"GoalFocus can have at most {MaxGoalFocusAreas} areas.");
        }
    }

    /// <summary>
    /// Parses an enum by name only, ignoring case. Enum.TryParse also accepts numbers
    /// ("1", "42"), which aren't part of the contract.
    /// </summary>
    internal static bool TryParseName<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        return value is not null
            && Enum.GetNames<TEnum>().Contains(value, StringComparer.OrdinalIgnoreCase)
            && Enum.TryParse(value, ignoreCase: true, out result);
    }

    /// <summary>
    /// An IANA id the server knows. Windows ids (W. Europe Standard Time) are found
    /// too, on Windows, so require an IANA one: every client platform reports those.
    /// </summary>
    private static bool IsIanaTimezone(string id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && zone.HasIanaId;

    private static ValidationResult Error(string field, string message) => new(message, new[] { field });
}

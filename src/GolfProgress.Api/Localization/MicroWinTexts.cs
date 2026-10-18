using System.Text.Json;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Localization;

/// <summary>
/// A micro-win's title and message in a reader's language. Micro-wins are stored with
/// their English text, written when the round was logged; each English message belongs
/// to exactly one <see cref="MicroWinText"/>, so it's read back as that text and
/// written out again in the language asked for. That works for micro-wins stored
/// before the API spoke Dutch, too. Text it doesn't recognise is shown as stored.
/// </summary>
public static class MicroWinTexts
{
    private static readonly Dictionary<string, MicroWinText> ByEnglishMessage =
        Enum.GetValues<MicroWinText>().ToDictionary(t => ApiTexts.English.MicroWin(t, "").Message);

    /// <summary>The English title and message to store for a new micro-win.</summary>
    public static (string Title, string Message) Stored(MicroWinText text, ConfidenceLevel confidence, string? focusLabel = null) =>
        Render(ApiTexts.English, text, confidence, focusLabel);

    public static (string Title, string Message) For(MicroWin win, ApiTexts texts)
    {
        var lowConfidence = win.Message.EndsWith(ApiTexts.English.LowConfidenceSuffix, StringComparison.Ordinal);
        var message = lowConfidence ? win.Message[..^ApiTexts.English.LowConfidenceSuffix.Length] : win.Message;

        if (texts == ApiTexts.English || !ByEnglishMessage.TryGetValue(message, out var text))
            return (win.Title, win.Message);

        var (title, rendered) = texts.MicroWin(text, text == MicroWinText.FocusSet ? FocusLabel(win, texts) : null);
        return (title, lowConfidence ? rendered + texts.LowConfidenceSuffix : rendered);
    }

    private static (string Title, string Message) Render(ApiTexts texts, MicroWinText text, ConfidenceLevel confidence, string? focusLabel)
    {
        var (title, message) = texts.MicroWin(text, focusLabel);
        return (title, confidence == ConfidenceLevel.Low ? message + texts.LowConfidenceSuffix : message);
    }

    /// <summary>
    /// The focus area a FocusSet win names, from its evidence. A value that isn't a
    /// known area was shown as typed when it was stored, and still is.
    /// </summary>
    private static string FocusLabel(MicroWin win, ApiTexts texts)
    {
        var focus = win.EvidenceJson.RootElement.ValueKind == JsonValueKind.Object
            && win.EvidenceJson.RootElement.TryGetProperty("focus", out var f) && f.ValueKind == JsonValueKind.String
                ? f.GetString()
                : null;

        return ParseFocus(focus) is GoalFocusArea area ? texts.FocusLabel(area) : focus ?? "";
    }

    /// <summary>
    /// A stored focus area, by name. Underscores are ignored so the earlier snake_case
    /// spellings (tee_shots) still map (#82).
    /// </summary>
    public static GoalFocusArea? ParseFocus(string? focus) =>
        focus is not null
        && Enum.TryParse<GoalFocusArea>(focus.Replace("_", ""), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : null;
}

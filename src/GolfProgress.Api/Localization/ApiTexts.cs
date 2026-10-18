using System.Globalization;
using GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Localization;

/// <summary>The micro-wins the API writes; each has one title and message per language.</summary>
public enum MicroWinText
{
    FewerPenalties,
    PenaltiesHeldSteady,
    PuttingImproved,
    PuttingHeldSteady,
    FewerBlowUpHoles,
    BigMistakesContained,
    RoundLogged,
    LearningCaptured,
    ConsistencyWin,
    ProgressSignalsMatter,
    FocusPutting,
    FocusPenalties,
    FocusSet,
}

/// <summary>The "next focus" recommendations, by their lever key.</summary>
public enum LeverText { Penalties, ThreePutts, DamageControl, DataQuality }

/// <summary>
/// Every sentence the API writes for people to read, in each language the apps come in. Requests choose one with Accept-Language (see
/// <see cref="ApiLanguage"/>); emails use the user's chosen language.
///
/// A language is a subclass, so a new text is an abstract member that every language
/// must implement before the API builds. English is also what's stored in the
/// database, so changing an English text means keeping <see cref="MicroWinTexts"/>
/// able to recognise the old one.
/// </summary>
public abstract class ApiTexts
{
    public static readonly ApiTexts English = new EnglishTexts();
    public static readonly ApiTexts Dutch = new DutchTexts();

    public static IReadOnlyList<ApiTexts> All { get; } = new[] { English, Dutch };

    public static ApiTexts For(AppLanguage language) => language switch
    {
        AppLanguage.Dutch => Dutch,
        _ => English,
    };

    /// <summary>The texts for the request being handled, from its Accept-Language.</summary>
    public static ApiTexts Current => For(ApiLanguage.FromCulture(CultureInfo.CurrentUICulture));

    public abstract AppLanguage Language { get; }

    /// <summary>Formats the numbers inside texts, e.g. a decimal comma in Dutch.</summary>
    public abstract CultureInfo Culture { get; }

    // ------------------------------------------------------------ micro-wins

    /// <param name="focusLabel">For <see cref="MicroWinText.FocusSet"/>: the focus area, from <see cref="FocusLabel"/>.</param>
    public abstract (string Title, string Message) MicroWin(MicroWinText text, string? focusLabel = null);

    /// <summary>Added to a micro-win's message when its confidence is low.</summary>
    public abstract string LowConfidenceSuffix { get; }

    public abstract string FocusLabel(GoalFocusArea area);

    // ------------------------------------------------------------ next focus

    public abstract (string Title, string Recommendation) Lever(LeverText lever);

    /// <param name="delta">The round against the baseline; null when there's no baseline yet.</param>
    public abstract string LeverRationale(LeverText lever, decimal? delta);

    // ------------------------------------------------------------ monthly review

    public abstract string RoundsLogged(int count);
    public abstract string AverageFitnessScore(decimal? average);
    public abstract string TopMicroWinTheme(MicroWinCategory category);
    public abstract string ChangeVsLastMonth(decimal change);

    // ------------------------------------------------------------ email

    public abstract (string Subject, string PlainText, string Html) PasswordResetEmail(string link, int minutes);

    protected static string Html(string text) => System.Net.WebUtility.HtmlEncode(text);
}

internal sealed class EnglishTexts : ApiTexts
{
    public override AppLanguage Language => AppLanguage.English;
    public override CultureInfo Culture => CultureInfo.InvariantCulture;

    public override (string Title, string Message) MicroWin(MicroWinText text, string? focusLabel = null) => text switch
    {
        MicroWinText.FewerPenalties => ("Fewer penalties",
            "You had fewer penalty strokes than your recent average — that’s immediate damage control."),
        MicroWinText.PenaltiesHeldSteady => ("Penalties held steady",
            "Penalties stayed under control today — that’s a strong scoring foundation."),
        MicroWinText.PuttingImproved => ("Putting improved",
            "Your total putts were lower than your recent average — nice progress on the greens."),
        MicroWinText.PuttingHeldSteady => ("Putting held steady",
            "Putting stayed consistent — stability here usually shows up in scores soon."),
        MicroWinText.FewerBlowUpHoles => ("Fewer blow-up holes",
            "Your big-mistake holes were lower than usual — that’s real progress even if score didn’t show it."),
        MicroWinText.BigMistakesContained => ("Big mistakes contained",
            "You kept big mistakes from spiraling — that’s a key improvement signal."),
        MicroWinText.RoundLogged => ("Round logged",
            "You logged the round — that’s how progress becomes visible over time."),
        MicroWinText.LearningCaptured => ("Learning captured",
            "You captured what worked and what to repeat — that speeds up improvement."),
        MicroWinText.ConsistencyWin => ("Consistency win",
            "Logging rounds consistently makes trends show up faster — nice follow-through."),
        MicroWinText.ProgressSignalsMatter => ("Progress signals matter",
            "Even when score is noisy, reducing big mistakes and staying consistent is progress."),
        MicroWinText.FocusPutting => ("Focus-area check: putting",
            "You’re prioritizing putting — tracking it consistently is step one to improving it."),
        MicroWinText.FocusPenalties => ("Focus-area check: penalties",
            "You’re prioritizing penalties — keeping them in view is how they drop over time."),
        MicroWinText.FocusSet => ($"Focus-area check: {focusLabel}",
            "You set a focus area — we’ll tie more insights to it as you log more rounds."),
        _ => throw new ArgumentOutOfRangeException(nameof(text), text, null),
    };

    public override string LowConfidenceSuffix =>
        " (Lower confidence — add one more stat next time for sharper insights.)";

    public override string FocusLabel(GoalFocusArea area) => area switch
    {
        GoalFocusArea.TeeShots => "tee shots",
        _ => area.ToString().ToLowerInvariant(),
    };

    public override (string Title, string Recommendation) Lever(LeverText lever) => lever switch
    {
        LeverText.Penalties => ("Reduce penalty strokes",
            "Next round: choose the safest target when trouble is possible — one conservative decision can remove a penalty."),
        LeverText.ThreePutts => ("Cut down 3‑putts",
            "Next round: on long putts, aim to finish inside a 1–2 meter circle — pace first, line second."),
        LeverText.DamageControl => ("Improve damage control",
            "Next round: after a mistake, play the next shot for position — your goal is bogey, not hero."),
        LeverText.DataQuality => ("Add one stat for sharper insights",
            "Next round: add just one stat (penalties or putts) — you’ll unlock more precise progress insights."),
        _ => throw new ArgumentOutOfRangeException(nameof(lever), lever, null),
    };

    public override string LeverRationale(LeverText lever, decimal? delta) => (lever, delta) switch
    {
        (LeverText.Penalties, decimal d) => $"Penalties vs baseline Δ={d.ToString("0.##", Culture)}",
        (LeverText.Penalties, null) => "Penalties are a high-impact lever.",
        (LeverText.ThreePutts, decimal d) => $"3‑putts vs baseline Δ={d.ToString("0.##", Culture)}",
        (LeverText.ThreePutts, null) => "Three‑putts are a quick scoring lever.",
        (LeverText.DamageControl, decimal d) => $"Blow‑ups vs baseline Δ={d.ToString("0.##", Culture)}",
        (LeverText.DamageControl, null) => "Reducing big holes improves scoring fastest.",
        (LeverText.DataQuality, _) => "This round has minimal stats; one extra stat improves recommendation accuracy.",
        _ => throw new ArgumentOutOfRangeException(nameof(lever), lever, null),
    };

    public override string RoundsLogged(int count) => $"Rounds logged: {count}";

    public override string AverageFitnessScore(decimal? average) => average is decimal a
        ? $"Average fitness score: {a.ToString("0.##", Culture)}"
        : "Fitness score: not enough data yet";

    // The category's own name, as the API has always written it.
    public override string TopMicroWinTheme(MicroWinCategory category) => $"Most common micro‑win theme: {category}";

    public override string ChangeVsLastMonth(decimal change) =>
        $"Change vs last month: {change.ToString("+0.##;-0.##;0", Culture)}";

    public override (string Subject, string PlainText, string Html) PasswordResetEmail(string link, int minutes) => (
        "Reset your GolfProgress password",
        "Someone asked to reset the password for your GolfProgress account.\n\n" +
        $"To choose a new password, open this link within {minutes} minutes:\n{link}\n\n" +
        "If you didn't ask, ignore this email: your password stays as it is.",
        "<p>Someone asked to reset the password for your GolfProgress account.</p>" +
        $"<p>To choose a new password, open this link within {minutes} minutes:<br>" +
        $"<a href=\"{Html(link)}\">Reset your password</a></p>" +
        "<p>If you didn't ask, ignore this email: your password stays as it is.</p>");
}

internal sealed class DutchTexts : ApiTexts
{
    private static readonly CultureInfo NlCulture = CultureInfo.GetCultureInfo("nl-NL");

    public override AppLanguage Language => AppLanguage.Dutch;
    public override CultureInfo Culture => NlCulture;

    public override (string Title, string Message) MicroWin(MicroWinText text, string? focusLabel = null) => text switch
    {
        MicroWinText.FewerPenalties => ("Minder strafslagen",
            "Je had minder strafslagen dan je recente gemiddelde — dat is direct schade beperken."),
        MicroWinText.PenaltiesHeldSteady => ("Strafslagen onder controle",
            "Je hield je strafslagen vandaag onder controle — een sterke basis voor je score."),
        MicroWinText.PuttingImproved => ("Beter geputt",
            "Je had minder putts dan je recente gemiddelde — mooie vooruitgang op de greens."),
        MicroWinText.PuttingHeldSteady => ("Putten stabiel",
            "Je putten bleef stabiel — dat zie je meestal snel terug in je score."),
        MicroWinText.FewerBlowUpHoles => ("Minder uitschieters",
            "Je had minder holes met grote fouten dan normaal — echte vooruitgang, ook als je score het nog niet laat zien."),
        MicroWinText.BigMistakesContained => ("Grote fouten beperkt",
            "Je liet grote fouten niet uit de hand lopen — een belangrijk teken van vooruitgang."),
        MicroWinText.RoundLogged => ("Ronde vastgelegd",
            "Je hebt de ronde vastgelegd — zo wordt je vooruitgang na verloop van tijd zichtbaar."),
        MicroWinText.LearningCaptured => ("Les vastgelegd",
            "Je hebt vastgelegd wat werkte en wat je wilt herhalen — zo word je sneller beter."),
        MicroWinText.ConsistencyWin => ("Goed volgehouden",
            "Door je rondes consequent vast te leggen, zie je trends sneller — mooi volgehouden."),
        MicroWinText.ProgressSignalsMatter => ("Vooruitgang zit in de details",
            "Ook als je score wisselt, zijn minder grote fouten en meer consistentie echte vooruitgang."),
        MicroWinText.FocusPutting => ("Focuscheck: putten",
            "Je legt de nadruk op putten — het consequent bijhouden is de eerste stap om beter te worden."),
        MicroWinText.FocusPenalties => ("Focuscheck: strafslagen",
            "Je legt de nadruk op strafslagen — door ze in de gaten te houden, nemen ze na verloop van tijd af."),
        MicroWinText.FocusSet => ($"Focuscheck: {focusLabel}",
            "Je hebt een focusgebied gekozen — hoe meer rondes je invoert, hoe meer inzichten we eraan koppelen."),
        _ => throw new ArgumentOutOfRangeException(nameof(text), text, null),
    };

    public override string LowConfidenceSuffix =>
        " (Minder zeker — voeg de volgende keer nog één statistiek toe voor scherpere inzichten.)";

    public override string FocusLabel(GoalFocusArea area) => area switch
    {
        GoalFocusArea.Putting => "putten",
        GoalFocusArea.Penalties => "strafslagen",
        GoalFocusArea.TeeShots => "afslagen",
        GoalFocusArea.Consistency => "consistentie",
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, null),
    };

    public override (string Title, string Recommendation) Lever(LeverText lever) => lever switch
    {
        LeverText.Penalties => ("Minder strafslagen",
            "Volgende ronde: kies het veiligste doel als er gevaar dreigt — één voorzichtige keuze kan een strafslag schelen."),
        LeverText.ThreePutts => ("Minder drie‑putts",
            "Volgende ronde: probeer lange putts binnen een cirkel van 1–2 meter te laten eindigen — eerst de snelheid, dan de lijn."),
        LeverText.DamageControl => ("Beter schade beperken",
            "Volgende ronde: speel na een fout de volgende slag op positie — je doel is een bogey, geen heldendaad."),
        LeverText.DataQuality => ("Voeg één statistiek toe voor scherpere inzichten",
            "Volgende ronde: voeg één statistiek toe (strafslagen of putts) — dan krijg je preciezere inzichten in je vooruitgang."),
        _ => throw new ArgumentOutOfRangeException(nameof(lever), lever, null),
    };

    public override string LeverRationale(LeverText lever, decimal? delta) => (lever, delta) switch
    {
        (LeverText.Penalties, decimal d) => $"Strafslagen t.o.v. basislijn Δ={d.ToString("0.##", Culture)}",
        (LeverText.Penalties, null) => "Strafslagen zijn een hefboom met veel impact.",
        (LeverText.ThreePutts, decimal d) => $"Drie‑putts t.o.v. basislijn Δ={d.ToString("0.##", Culture)}",
        (LeverText.ThreePutts, null) => "Minder drie‑putts levert snel slagen op.",
        (LeverText.DamageControl, decimal d) => $"Uitschieters t.o.v. basislijn Δ={d.ToString("0.##", Culture)}",
        (LeverText.DamageControl, null) => "Minder grote holes verbetert je score het snelst.",
        (LeverText.DataQuality, _) => "Deze ronde heeft weinig statistieken; één extra statistiek maakt de aanbeveling nauwkeuriger.",
        _ => throw new ArgumentOutOfRangeException(nameof(lever), lever, null),
    };

    public override string RoundsLogged(int count) => $"Rondes ingevoerd: {count}";

    public override string AverageFitnessScore(decimal? average) => average is decimal a
        ? $"Gemiddelde fitnessscore: {a.ToString("0.##", Culture)}"
        : "Fitnessscore: nog niet genoeg gegevens";

    public override string TopMicroWinTheme(MicroWinCategory category) => "Meest voorkomende soort micro-win: " + category switch
    {
        MicroWinCategory.StatDelta => "statistiek",
        MicroWinCategory.Behavior => "gedrag",
        MicroWinCategory.Narrative => "algemeen",
        MicroWinCategory.Habit => "gewoonte",
        MicroWinCategory.Recovery => "herstel",
        MicroWinCategory.FocusArea => "focusgebied",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    public override string ChangeVsLastMonth(decimal change) =>
        $"Verschil met vorige maand: {change.ToString("+0.##;-0.##;0", Culture)}";

    public override (string Subject, string PlainText, string Html) PasswordResetEmail(string link, int minutes) => (
        "Je GolfProgress-wachtwoord opnieuw instellen",
        "Iemand heeft gevraagd om het wachtwoord van je GolfProgress-account opnieuw in te stellen.\n\n" +
        $"Open deze link binnen {minutes} minuten om een nieuw wachtwoord te kiezen:\n{link}\n\n" +
        "Heb je dit niet zelf gevraagd? Negeer deze e-mail dan: je wachtwoord blijft zoals het is.",
        "<p>Iemand heeft gevraagd om het wachtwoord van je GolfProgress-account opnieuw in te stellen.</p>" +
        $"<p>Open deze link binnen {minutes} minuten om een nieuw wachtwoord te kiezen:<br>" +
        $"<a href=\"{Html(link)}\">Wachtwoord opnieuw instellen</a></p>" +
        "<p>Heb je dit niet zelf gevraagd? Negeer deze e-mail dan: je wachtwoord blijft zoals het is.</p>");
}

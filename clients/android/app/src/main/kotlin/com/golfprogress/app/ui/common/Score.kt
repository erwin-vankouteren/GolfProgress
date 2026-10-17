package com.golfprogress.app.ui.common

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.app.R
import com.golfprogress.app.ui.theme.DesignTokens
import com.golfprogress.app.ui.theme.LocalPalette
import java.math.RoundingMode
import java.text.NumberFormat
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.util.Locale
import kotlin.math.abs
import kotlin.math.roundToInt

/** Pieces that show a score, shared by Home, the round detail and Progress. */

enum class Confidence(
    @StringRes val label: Int,
) {
    Low(R.string.confidence_low),
    Medium(R.string.confidence_medium),
    High(R.string.confidence_high),
    ;

    companion object {
        /** From the API's value, e.g. "Medium"; null when there's none. */
        fun of(value: String?): Confidence? = entries.firstOrNull { it.name == value }
    }
}

/**
 * A confidence level: a coloured dot and its name, never the colour alone (#78), read as
 * "Confidence: Medium". Low confidence also says why when [explain] is set.
 */
@Composable
fun ConfidenceBadge(
    level: Confidence,
    explain: Boolean = false,
) {
    val palette = LocalPalette.current
    val colour =
        when (level) {
            Confidence.Low -> palette.confidenceLow
            Confidence.Medium -> palette.confidenceMedium
            Confidence.High -> palette.confidenceHigh
        }
    val name = stringResource(level.label)
    val description = stringResource(R.string.confidence_description, name)
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xxs)) {
        Row(
            horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs),
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier.clearAndSetSemantics { contentDescription = description },
        ) {
            Text("●", color = colour, style = MaterialTheme.typography.labelLarge)
            Text(name, color = colour, style = MaterialTheme.typography.labelLarge)
        }
        if (explain && level == Confidence.Low) {
            Text(
                stringResource(R.string.confidence_low_explanation),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/** The most a pillar can score (ProgressComputationService.PillarMax). */
const val PILLAR_MAX = 25

enum class Pillar(
    @StringRes val label: Int,
) {
    Execution(R.string.pillar_execution),
    DamageControl(R.string.pillar_damage_control),
    Consistency(R.string.pillar_consistency),
    Habits(R.string.pillar_habits),
    ;

    fun of(score: FitnessScoreResponse): Int =
        when (this) {
            Execution -> score.execution
            DamageControl -> score.damageControl
            Consistency -> score.consistency
            Habits -> score.habits
        }

    @Composable
    fun colour(): Color {
        val palette = LocalPalette.current
        return when (this) {
            Execution -> palette.pillarExecution
            DamageControl -> palette.pillarDamageControl
            Consistency -> palette.pillarConsistency
            Habits -> palette.pillarHabits
        }
    }
}

/** One pillar: its name and "18 / 25" as text; the bar repeats it for sighted users only. */
@Composable
fun PillarBar(
    pillar: Pillar,
    value: Int,
    modifier: Modifier = Modifier,
) {
    val label = stringResource(pillar.label)
    val score = stringResource(R.string.pillar_score, value, PILLAR_MAX)
    Column(
        modifier = modifier.fillMaxWidth().semantics(mergeDescendants = true) {},
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs),
    ) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text(label, style = MaterialTheme.typography.bodyMedium)
            Text(score, style = MaterialTheme.typography.labelLarge)
        }
        LinearProgressIndicator(
            progress = { value.coerceIn(0, PILLAR_MAX) / PILLAR_MAX.toFloat() },
            color = pillar.colour(),
            trackColor = MaterialTheme.colorScheme.surfaceVariant,
            strokeCap = StrokeCap.Round,
            drawStopIndicator = {},
            modifier = Modifier.fillMaxWidth().height(8.dp).clearAndSetSemantics {},
        )
    }
}

@Composable
fun PillarBars(score: FitnessScoreResponse) {
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md)) {
        Pillar.entries.forEach { PillarBar(it, it.of(score)) }
    }
}

/** A date the API sends as yyyy-MM-dd, e.g. "3 May 2026" in the user's locale. */
fun formatDate(
    iso: String,
    locale: Locale,
): String = LocalDate.parse(iso).format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(locale))

@Composable
fun formatDate(iso: String): String = formatDate(iso, currentLocale())

@Composable
fun currentLocale(): Locale = LocalConfiguration.current.locales[0]

/** "▲ Up 4 on last month": the arrow is decoration; the words carry the direction. */
@Composable
fun MonthChange(
    change: Double,
    @StringRes up: Int = R.string.up_on_last_month,
    @StringRes down: Int = R.string.down_on_last_month,
    @StringRes same: Int = R.string.same_as_last_month,
) {
    val rounded = change.roundToInt()
    val palette = LocalPalette.current
    val text =
        when {
            rounded > 0 -> stringResource(up, rounded)
            rounded < 0 -> stringResource(down, abs(rounded))
            else -> stringResource(same)
        }
    val arrow =
        if (rounded > 0) {
            "▲ "
        } else if (rounded < 0) {
            "▼ "
        } else {
            ""
        }
    Text(
        text = arrow + text,
        color = if (rounded > 0) palette.success else MaterialTheme.colorScheme.onSurfaceVariant,
        style = MaterialTheme.typography.labelLarge,
        modifier = Modifier.clearAndSetSemantics { contentDescription = text },
    )
}

/**
 * A number with at most one decimal, rounded half up as the web app's Intl does (88.25
 * is 88.3), not half to even as NumberFormat does by default.
 */
fun oneDecimal(
    value: Double,
    locale: Locale,
): String =
    NumberFormat
        .getNumberInstance(locale)
        .apply {
            maximumFractionDigits = 1
            roundingMode = RoundingMode.HALF_UP
        }.format(value)

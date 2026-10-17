package com.golfprogress.app.progress

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.DateRange
import androidx.compose.material3.Button
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuAnchorType
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.api.MicroWinCategory
import com.golfprogress.api.generated.models.MonthlyReviewResponse
import com.golfprogress.api.generated.models.MonthlyTimelinePointResponse
import com.golfprogress.api.generated.models.RoundTimelinePointResponse
import com.golfprogress.api.generated.models.UserBaselineResponse
import com.golfprogress.app.R
import com.golfprogress.app.rounds.Fact
import com.golfprogress.app.rounds.FormProblem
import com.golfprogress.app.ui.common.Confidence
import com.golfprogress.app.ui.common.ConfidenceBadge
import com.golfprogress.app.ui.common.EmptyState
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.MonthChange
import com.golfprogress.app.ui.common.SectionCard
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.currentLocale
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.common.oneDecimal
import com.golfprogress.app.ui.theme.DesignTokens
import com.golfprogress.app.ui.theme.LocalPalette
import java.text.NumberFormat
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import kotlin.math.roundToInt

/**
 * Progress (#109): rounds over time, the monthly trend, the baseline and monthly reviews.
 * Every chart has a summary sentence and "Show as table".
 */
@Composable
fun ProgressScreen(
    onLogRound: () -> Unit,
    viewModel: ProgressViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.refresh() }

    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        when (val count = state.roundCount) {
            Load.Loading -> Skeleton(blocks = 3)
            is Load.Failed -> LoadError(R.string.progress_load_error, viewModel::retry)
            is Load.Loaded ->
                if (count.value == 0) {
                    EmptyState(R.string.progress_empty, action = R.string.log_first_round, onAction = onLogRound)
                } else {
                    TimelineSection(state, count.value, viewModel)
                    MonthlySection(state.monthly, viewModel::retryMonthly)
                    BaselineSection(state.baseline, viewModel::retryBaseline)
                    ReviewSection(state, viewModel, online)
                }
        }
    }
}

// ---------------------------------------------------------------- rounds over time

private val rangeLabels =
    mapOf(
        RangeKey.ThreeMonths to R.string.range_3m,
        RangeKey.SixMonths to R.string.range_6m,
        RangeKey.TwelveMonths to R.string.range_12m,
        RangeKey.AllTime to R.string.range_all,
        RangeKey.Custom to R.string.range_custom,
    )

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun TimelineSection(
    state: ProgressState,
    totalRounds: Int,
    viewModel: ProgressViewModel,
) {
    SectionCard(R.string.timeline_title) {
        Text(stringResource(R.string.period), style = MaterialTheme.typography.labelLarge)
        FlowRow(horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
            RangeKey.entries.forEach { key ->
                FilterChip(
                    selected = state.range == key,
                    onClick = { viewModel.setRange(key) },
                    label = { Text(stringResource(rangeLabels.getValue(key))) },
                    modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
                )
            }
        }
        if (state.range == RangeKey.Custom) {
            OptionalDateField(R.string.range_from, state.custom.from) { viewModel.setCustom(state.custom.copy(from = it)) }
            OptionalDateField(R.string.range_to, state.custom.to) { viewModel.setCustom(state.custom.copy(to = it)) }
            if (!state.customValid) {
                Text(
                    stringResource(R.string.range_order),
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
                )
            }
        }

        when (val timeline = state.timeline) {
            Load.Loading -> Skeleton(blocks = 1, blockHeight = 220)
            is Load.Failed -> LoadError(R.string.timeline_load_error, viewModel::retryTimeline)
            is Load.Loaded ->
                TimelineContent(
                    timeline.value,
                    allRoundsShown =
                        state.range == RangeKey.AllTime || timeline.value.size >= totalRounds,
                )
        }
    }
}

@Composable
private fun TimelineContent(
    points: List<RoundTimelinePointResponse>,
    allRoundsShown: Boolean,
) {
    val locale = currentLocale()
    val summary = timelineSummary(points)
    if (summary == null) {
        Hint(stringResource(R.string.no_rounds_in_period))
        return
    }
    val sentence =
        pluralStringResource(
            R.plurals.timeline_summary,
            summary.rounds,
            summary.rounds,
            mediumDate(summary.firstDate, locale),
            mediumDate(summary.lastDate, locale),
        ) +
            (summary.score?.let { " " + stringResource(R.string.timeline_score_summary, it.first, it.last, it.average) } ?: "")
    Text(sentence, style = MaterialTheme.typography.bodyLarge)
    if (points.size >= TIMELINE_TAKE) Hint(stringResource(R.string.timeline_capped, TIMELINE_TAKE))

    val short = roundsShortOfTrend(points)
    val chart = timelineChart(points)
    if (short > 0) {
        val scored = points.count { it.fitnessScoreTotal != null }
        LowData(
            if (allRoundsShown) {
                pluralStringResource(R.plurals.more_rounds_for_trend, short, short)
            } else {
                pluralStringResource(R.plurals.few_rounds_in_period, scored, scored)
            },
        )
        TimelineTable(points)
        return
    }
    if (chart.strokesHoles != null && strokesLeaveOutRounds(points)) Hint(stringResource(R.string.strokes_leave_out, chart.strokesHoles))
    ChartOrTable(
        chart = {
            Legend(chart.strokesHoles)
            TimelineChart(chart, points.map { it.playedAt }, Modifier.fillMaxWidth().height(240.dp))
        },
        table = { TimelineTable(points) },
    )
}

@Composable
private fun Legend(strokesHoles: Int?) {
    Row(horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg)) {
        Text(
            "— " + stringResource(R.string.fitness_score),
            color = MaterialTheme.colorScheme.primary,
            style = MaterialTheme.typography.bodySmall,
        )
        if (strokesHoles != null) {
            Text(
                "- - " + stringResource(R.string.strokes_line, strokesHoles),
                color = LocalPalette.current.info,
                style = MaterialTheme.typography.bodySmall,
            )
        }
    }
}

/** Newest first, like the round list. */
@Composable
private fun TimelineTable(points: List<RoundTimelinePointResponse>) {
    val locale = currentLocale()
    Table(
        headers = listOf(R.string.date, R.string.holes, R.string.strokes, R.string.fitness_score),
        rows =
            points.reversed().map { p ->
                listOf(
                    mediumDate(p.playedAt, locale),
                    p.holes.toString(),
                    p.totalStrokes.toString(),
                    p.fitnessScoreTotal?.toString() ?: stringResource(R.string.not_scored),
                )
            },
    )
}

// ---------------------------------------------------------------- monthly trend

@Composable
private fun MonthlySection(
    monthly: Load<List<MonthlyTimelinePointResponse>>,
    onRetry: () -> Unit,
) {
    SectionCard(R.string.monthly_trend_title) {
        when (monthly) {
            Load.Loading -> Skeleton(blocks = 1, blockHeight = 220)
            is Load.Failed -> LoadError(R.string.monthly_load_error, onRetry)
            is Load.Loaded -> MonthlyContent(monthly.value)
        }
    }
}

@Composable
private fun MonthlyContent(items: List<MonthlyTimelinePointResponse>) {
    val locale = currentLocale()
    val summary = monthlySummary(items)
    if (summary == null) {
        Hint(stringResource(R.string.monthly_empty))
        return
    }
    if (!hasMonthlyTrend(items)) {
        Text(
            stringResource(R.string.monthly_summary_one, monthName(summary.last.month, locale), summary.last.average),
            style = MaterialTheme.typography.bodyLarge,
        )
        LowData(stringResource(R.string.monthly_needs_another_month))
        MonthlyTable(items)
        return
    }
    Text(
        pluralStringResource(
            R.plurals.monthly_summary,
            summary.months,
            summary.months,
            summary.first.average,
            monthName(summary.first.month, locale),
            summary.last.average,
            monthName(summary.last.month, locale),
        ),
        style = MaterialTheme.typography.bodyLarge,
    )
    ChartOrTable(
        chart = { MonthlyChart(items, Modifier.fillMaxWidth().height(220.dp)) },
        table = { MonthlyTable(items) },
    )
}

@Composable
private fun MonthlyTable(items: List<MonthlyTimelinePointResponse>) {
    val locale = currentLocale()
    val signed = NumberFormat.getIntegerInstance(locale)
    Table(
        headers = listOf(R.string.month, R.string.rounds_column, R.string.month_average, R.string.change_column),
        rows =
            items.reversed().map { item ->
                listOf(
                    monthName(item.monthStart, locale),
                    item.roundsLogged.toString(),
                    item.fitnessAvg?.roundToInt()?.toString() ?: stringResource(R.string.no_scored_rounds),
                    item.fitnessChange
                        ?.roundToInt()
                        ?.let { (if (it > 0) "+" else "") + signed.format(it) }
                        .orEmpty(),
                )
            },
    )
}

// ---------------------------------------------------------------- baseline

@Composable
private fun BaselineSection(
    baseline: Load<UserBaselineResponse?>,
    onRetry: () -> Unit,
) {
    SectionCard(R.string.baseline) {
        when (baseline) {
            Load.Loading -> Skeleton(blocks = 1, blockHeight = 120)
            is Load.Failed -> LoadError(R.string.baseline_load_error, onRetry)
            is Load.Loaded ->
                baseline.value?.let { BaselineContent(it) } ?: Hint(stringResource(R.string.baseline_empty))
        }
    }
}

@Composable
private fun BaselineContent(baseline: UserBaselineResponse) {
    val locale = currentLocale()
    val format = { value: Double -> oneDecimal(value, locale) }
    Hint(stringResource(R.string.baseline_intro))
    baseline.avgScore?.let { Fact(stringResource(R.string.strokes), format(it)) }
    baseline.scoreStdDev?.let { Fact(stringResource(R.string.spread), stringResource(R.string.spread_value, format(it))) }
    listOf(
        R.string.putts to baseline.avgPutts,
        R.string.penalties to baseline.avgPenalties,
        R.string.three_putts to baseline.avgThreePutts,
        R.string.blow_up_holes to baseline.avgBlowUpHoles,
        R.string.fairways to baseline.avgFairwaysHit,
        R.string.greens to baseline.avgGir,
    ).forEach { (label, value) -> value?.let { Fact(stringResource(label), format(it)) } }
    Fact(stringResource(R.string.stats_tracked), NumberFormat.getPercentInstance(currentLocale()).format(baseline.completenessRatio))
    Text(
        pluralStringResource(R.plurals.baseline_based_on, baseline.sampleSize, baseline.sampleSize),
        style = MaterialTheme.typography.bodyLarge,
    )
    Confidence.of(baseline.confidenceLevel.value)?.let { ConfidenceBadge(it, explain = true) }
}

// ---------------------------------------------------------------- monthly review

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ReviewSection(
    state: ProgressState,
    viewModel: ProgressViewModel,
    online: Boolean,
) {
    val locale = currentLocale()
    SectionCard(R.string.monthly_review_title) {
        val months = state.monthly as? Load.Loaded ?: return@SectionCard Skeleton(blocks = 1, blockHeight = 64)
        val month = state.reviewMonth ?: return@SectionCard
        var open by rememberSaveable { mutableStateOf(false) }
        ExposedDropdownMenuBox(expanded = open, onExpandedChange = { open = it }) {
            OutlinedTextField(
                value = monthName(month, locale),
                onValueChange = {},
                readOnly = true,
                label = { Text(stringResource(R.string.month)) },
                trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = open) },
                modifier = Modifier.fillMaxWidth().menuAnchor(ExposedDropdownMenuAnchorType.PrimaryNotEditable),
            )
            ExposedDropdownMenu(expanded = open, onDismissRequest = { open = false }) {
                months.value.reversed().forEach { item ->
                    val name = monthName(item.monthStart, locale)
                    DropdownMenuItem(
                        text = { Text(if (item.hasReview) stringResource(R.string.month_reviewed, name) else name) },
                        onClick = {
                            open = false
                            viewModel.chooseMonth(item.monthStart)
                        },
                    )
                }
            }
        }

        when (val review = state.review) {
            null, Load.Loading -> Skeleton(blocks = 1, blockHeight = 96)
            is Load.Failed -> LoadError(R.string.review_load_error, { viewModel.retryReview() })
            is Load.Loaded -> {
                review.value?.let { ReviewCard(it, state.reviewPoint?.roundsLogged) }
                    ?: Hint(
                        stringResource(
                            if (state.nothingToReview) R.string.review_no_rounds else R.string.review_none,
                            monthName(month, locale),
                        ),
                    )
                if (!state.nothingToReview) ReviewForm(state, review.value != null, viewModel, online)
            }
        }
    }
}

@Composable
private fun ReviewCard(
    review: MonthlyReviewResponse,
    monthRounds: Int?,
) {
    val locale = currentLocale()
    Fact(stringResource(R.string.month_average), review.fitnessAvg?.roundToInt()?.toString() ?: stringResource(R.string.no_scored_rounds))
    (review.roundsLogged ?: monthRounds)?.let {
        Fact(stringResource(R.string.rounds_column), pluralStringResource(R.plurals.rounds_played, it, it))
    }
    // A category added after this build (#134) is left out rather than misnamed.
    themeLabel(MicroWinCategory.of(review.topMicroWinCategory))?.let { Fact(stringResource(R.string.review_theme), stringResource(it)) }
    review.focusNextMonth?.let { Fact(stringResource(R.string.review_focus), it) }
    review.fitnessChange?.let {
        MonthChange(it, up = R.string.up_on_previous_month, down = R.string.down_on_previous_month, same = R.string.same_as_previous_month)
    }
    Hint(
        stringResource(
            R.string.review_created_on,
            review.computedAt.format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(locale)),
        ),
    )
}

@StringRes
private fun themeLabel(category: MicroWinCategory): Int? =
    when (category) {
        MicroWinCategory.StatDelta -> R.string.theme_stat_delta
        MicroWinCategory.Behavior -> R.string.theme_behavior
        MicroWinCategory.Narrative -> R.string.theme_narrative
        MicroWinCategory.Habit -> R.string.theme_habit
        MicroWinCategory.Recovery -> R.string.theme_recovery
        MicroWinCategory.FocusArea -> R.string.theme_focus_area
        MicroWinCategory.Unknown -> null
    }

@Composable
private fun ReviewForm(
    state: ProgressState,
    exists: Boolean,
    viewModel: ProgressViewModel,
    online: Boolean,
) {
    val error = state.focusApiError ?: if (state.focusTooLong) stringResource(R.string.focus_too_long, MAX_FOCUS_LENGTH) else null
    OutlinedTextField(
        value = state.focus,
        onValueChange = viewModel::setFocus,
        label = { Text(stringResource(R.string.focus_next_month)) },
        isError = error != null,
        supportingText = { Text(error ?: stringResource(R.string.focus_hint)) },
        singleLine = true,
        modifier = Modifier.fillMaxWidth(),
    )
    state.reviewProblem?.let {
        Text(
            stringResource(if (it == FormProblem.SaveFailed) R.string.review_failed else R.string.auth_cant_connect),
            color = MaterialTheme.colorScheme.error,
            modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
        )
    }
    if (!online) Hint(stringResource(R.string.offline_cant_review))
    Button(
        onClick = viewModel::createReview,
        enabled = online && !state.creating,
        modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
    ) {
        Text(
            stringResource(
                when {
                    state.creating -> R.string.creating_review
                    exists -> R.string.update_review
                    else -> R.string.create_review
                },
            ),
        )
    }
    if (state.reviewReady) {
        Text(
            stringResource(R.string.review_ready),
            color = LocalPalette.current.success,
            fontWeight = FontWeight.SemiBold,
            modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
        )
    }
}

// ---------------------------------------------------------------- shared

/** The chart, or its numbers as a table: TalkBack users get the summary and the table. */
@Composable
private fun ChartOrTable(
    chart: @Composable () -> Unit,
    table: @Composable () -> Unit,
) {
    var asTable by rememberSaveable { mutableStateOf(false) }
    OutlinedButton(onClick = { asTable = !asTable }, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
        Text(stringResource(if (asTable) R.string.show_as_chart else R.string.show_as_table))
    }
    if (asTable) table() else chart()
}

/** Rows read one at a time by TalkBack, each as "Date: 3 May 2026, Holes: 18, …". */
@Composable
private fun Table(
    @StringRes headers: List<Int>,
    rows: List<List<String>>,
) {
    val names = headers.map { stringResource(it) }
    Column {
        Row(Modifier.fillMaxWidth().padding(vertical = DesignTokens.Spacing.xs)) {
            names.forEach {
                Text(
                    it,
                    Modifier.weight(1f),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        rows.forEach { row ->
            HorizontalDivider()
            val description = names.zip(row).joinToString(", ") { (name, value) -> "$name: $value" }
            Row(
                Modifier
                    .fillMaxWidth()
                    .heightIn(min = DesignTokens.touchTarget)
                    .padding(vertical = DesignTokens.Spacing.xs)
                    .semantics(mergeDescendants = true) { contentDescription = description },
            ) {
                row.forEach { Text(it, Modifier.weight(1f), style = MaterialTheme.typography.bodyMedium) }
            }
        }
    }
}

@Composable
private fun Hint(text: String) {
    Text(text, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
}

@Composable
private fun LowData(text: String) {
    Text(
        text,
        style = MaterialTheme.typography.bodyLarge,
        modifier =
            Modifier
                .fillMaxWidth()
                .padding(vertical = DesignTokens.Spacing.xs),
    )
}

/** A date that may be empty, chosen with the date picker. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun OptionalDateField(
    @StringRes label: Int,
    date: LocalDate?,
    onChange: (LocalDate) -> Unit,
) {
    var picking by rememberSaveable { mutableStateOf(false) }
    OutlinedTextField(
        value = date?.let { mediumDate(it.toString(), currentLocale()) }.orEmpty(),
        onValueChange = {},
        readOnly = true,
        label = { Text(stringResource(label)) },
        trailingIcon = {
            IconButton(onClick = { picking = true }) {
                Icon(Icons.Filled.DateRange, contentDescription = stringResource(R.string.choose_date_for, stringResource(label)))
            }
        },
        modifier = Modifier.fillMaxWidth(),
    )
    if (picking) {
        val picker = rememberDatePickerState(initialSelectedDateMillis = date?.atStartOfDay(ZoneOffset.UTC)?.toInstant()?.toEpochMilli())
        DatePickerDialog(
            onDismissRequest = { picking = false },
            confirmButton = {
                TextButton(onClick = {
                    picker.selectedDateMillis?.let { onChange(Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate()) }
                    picking = false
                }) { Text(stringResource(R.string.ok)) }
            },
            dismissButton = { TextButton(onClick = { picking = false }) { Text(stringResource(R.string.cancel)) } },
        ) { DatePicker(state = picker) }
    }
}

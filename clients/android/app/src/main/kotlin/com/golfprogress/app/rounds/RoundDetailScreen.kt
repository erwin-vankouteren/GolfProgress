package com.golfprogress.app.rounds

import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.text.font.FontWeight
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.generated.models.ReflectionResponse
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.app.R
import com.golfprogress.app.ui.common.Confidence
import com.golfprogress.app.ui.common.ConfidenceBadge
import com.golfprogress.app.ui.common.ConfirmDialog
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.Pillar
import com.golfprogress.app.ui.common.PillarBar
import com.golfprogress.app.ui.common.SectionCard
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.currentLocale
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.theme.DesignTokens
import java.text.NumberFormat
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.util.UUID
import kotlin.math.abs

/**
 * A round's detail (#97, docs/clients/ux-design.md → "Rounds"). [reflections] adds to the
 * reflection card, e.g. the form to add one (#101).
 */
@Composable
fun RoundDetailScreen(
    onEdit: (UUID) -> Unit,
    onDeleted: () -> Unit,
    viewModel: RoundDetailViewModel = hiltViewModel(),
    reflections: @Composable () -> Unit = {},
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.refresh() }
    LaunchedEffect(state.deleted) { if (state.deleted) onDeleted() }
    RoundDetailContent(
        state = state,
        onRetry = viewModel::retry,
        onEdit = { onEdit(viewModel.roundId) },
        onAskToDelete = viewModel::askToDelete,
        onConfirmDelete = viewModel::confirmDelete,
        onCancelDelete = viewModel::cancelDelete,
        reflections = reflections,
    )
}

@Composable
fun RoundDetailContent(
    state: RoundDetailState,
    onRetry: () -> Unit,
    onEdit: () -> Unit,
    onAskToDelete: () -> Unit,
    onConfirmDelete: () -> Unit,
    onCancelDelete: () -> Unit,
    online: Boolean = isOnline(),
    reflections: @Composable () -> Unit = {},
) {
    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        when (val detail = state.detail) {
            Load.Loading -> Skeleton(blocks = 4)
            is Load.Failed -> LoadError(R.string.round_load_error, onRetry)
            is Load.Loaded ->
                if (detail.value == null) {
                    RoundGone()
                } else {
                    Detail(detail.value, reflections)
                    Button(
                        onClick = onEdit,
                        enabled = online,
                        modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
                    ) { Text(stringResource(R.string.edit_round)) }
                    OutlinedButton(
                        onClick = onAskToDelete,
                        enabled = online,
                        modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
                    ) { Text(stringResource(R.string.delete_round), color = MaterialTheme.colorScheme.error) }
                }
        }
    }

    if (state.confirmDelete) {
        ConfirmDialog(
            title = R.string.delete_round_title,
            body = R.string.delete_round_body,
            confirm = R.string.delete_round,
            busy = state.deleting,
            error = if (state.deleteFailed) stringResource(R.string.delete_round_failed) else null,
            onConfirm = onConfirmDelete,
            onDismiss = onCancelDelete,
        )
    }
}

@Composable
private fun Detail(
    detail: RoundDetail,
    reflections: @Composable () -> Unit,
) {
    val round = detail.round
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        Text(
            formatDate(round.playedAt),
            style = MaterialTheme.typography.headlineMedium,
            modifier = Modifier.semantics { heading() },
        )
        Text(roundSummary(round), style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }

    ScoreCard(round, detail.score)

    if (detail.microWins.isNotEmpty()) {
        SectionCard(R.string.micro_wins) {
            detail.microWins.forEach { win ->
                Column {
                    Text("✓ " + win.title, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.SemiBold)
                    Text(win.message, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }

    detail.nextLever?.let { lever ->
        SectionCard(R.string.next_focus) {
            Text(lever.title, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.SemiBold)
            Text(lever.recommendation, style = MaterialTheme.typography.bodyLarge)
            Text(lever.rationale, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }

    StatsCard(round)
    ReflectionsCard(detail.reflections, reflections)
}

/** "Hilversum · 18 holes · 88 strokes", or without the course when there's none. */
@Composable
fun roundSummary(round: RoundResponse): String {
    val strokes = pluralStringResource(R.plurals.strokes_count, round.totalStrokes, round.totalStrokes)
    val holes = stringResource(R.string.holes_option, round.holes)
    return listOfNotNull(round.courseName, holes, strokes).joinToString(" · ")
}

@Composable
private fun ScoreCard(
    round: RoundResponse,
    score: FitnessScoreResponse?,
) {
    SectionCard(R.string.fitness_score) {
        if (score == null) {
            Text(stringResource(R.string.not_scored_yet), style = MaterialTheme.typography.bodyLarge)
            return@SectionCard
        }
        val description = stringResource(R.string.fitness_score_value, score.scoreTotal)
        Row(horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md), verticalAlignment = Alignment.CenterVertically) {
            Text(
                score.scoreTotal.toString(),
                style = MaterialTheme.typography.displaySmall,
                modifier = Modifier.clearAndSetSemantics { contentDescription = description },
            )
            Confidence.of(score.confidenceLevel.value)?.let { ConfidenceBadge(it, explain = true) }
        }
        val explanations = explainPillars(round, score)
        Pillar.entries.forEach { pillar -> PillarDisclosure(pillar, pillar.of(score), explanations.getValue(pillar)) }
    }
}

/** A pillar that opens to say why it scored what it did; TalkBack hears "collapsed" or "expanded". */
@Composable
private fun PillarDisclosure(
    pillar: Pillar,
    value: Int,
    sentences: List<Sentence>,
) {
    var open by rememberSaveable(pillar) { mutableStateOf(false) }
    val expanded = stringResource(R.string.expanded)
    val collapsed = stringResource(R.string.collapsed)
    Column(
        modifier =
            Modifier
                .fillMaxWidth()
                .animateContentSize()
                .clickable(role = Role.Button, onClickLabel = stringResource(R.string.show_why)) { open = !open }
                .heightIn(min = DesignTokens.touchTarget)
                .semantics { stateDescription = if (open) expanded else collapsed },
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs),
    ) {
        PillarBar(pillar, value)
        if (open) {
            sentences.forEach {
                Text(it.text(), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

@Composable
private fun StatsCard(round: RoundResponse) {
    SectionCard(R.string.stats) {
        val tracked = trackedStats(round)
        if (tracked.isEmpty()) {
            Text(stringResource(R.string.no_stats_tracked), style = MaterialTheme.typography.bodyLarge)
        } else {
            tracked.forEach { Fact(stringResource(it.label), round.stat(it).toString()) }
        }
        round.teeName?.let { Fact(stringResource(R.string.tee), it) }
        round.notes?.let { Fact(stringResource(R.string.notes), it) }
    }
}

@Composable
private fun ReflectionsCard(
    reflections: List<ReflectionResponse>,
    more: @Composable () -> Unit,
) {
    SectionCard(R.string.reflection) {
        if (reflections.isEmpty()) {
            Text(stringResource(R.string.no_reflection), style = MaterialTheme.typography.bodyLarge)
        }
        reflections.forEach { ReflectionView(it) }
        more()
    }
}

@Composable
fun ReflectionView(reflection: ReflectionResponse) {
    val locale = currentLocale()
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        Text(
            stringResource(
                R.string.reflected_on,
                reflection.createdAt.format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(locale)),
            ),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        reflection.focusRating?.let { Fact(stringResource(R.string.focus), stringResource(R.string.out_of_five, it)) }
        reflection.moodRating?.let { Fact(stringResource(R.string.mood), stringResource(R.string.out_of_five, it)) }
        reflection.frustrationSpillover?.let {
            Fact(stringResource(R.string.frustration_carried_over), stringResource(if (it) R.string.yes else R.string.no))
        }
        reflection.whatWorked?.let { Fact(stringResource(R.string.what_worked), it) }
        reflection.whatToRepeat?.let { Fact(stringResource(R.string.what_to_repeat), it) }
        reflection.oneChangeNext?.let { Fact(stringResource(R.string.one_change_next), it) }
    }
}

/** A label above its value, read together. */
@Composable
fun Fact(
    label: String,
    value: String,
) {
    Column(Modifier.semantics(mergeDescendants = true) {}) {
        Text(label, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, style = MaterialTheme.typography.bodyLarge)
    }
}

val StatField.label: Int
    get() =
        when (this) {
            StatField.TotalPutts -> R.string.putts
            StatField.Penalties -> R.string.penalties
            StatField.FairwaysHit -> R.string.fairways
            StatField.Gir -> R.string.greens
            StatField.ThreePutts -> R.string.three_putts
            StatField.BlowUpHoles -> R.string.blow_up_holes
        }

@Composable
private fun Sentence.text(): String {
    val locale = currentLocale()
    val oneDecimal = { value: Double -> NumberFormat.getNumberInstance(locale).apply { maximumFractionDigits = 1 }.format(value) }
    return when (this) {
        Sentence.FirstRound -> stringResource(R.string.explain_first_round)
        is Sentence.ComparedWithAverage ->
            when {
                difference < 0 -> stringResource(R.string.explain_better, strokes, oneDecimal(abs(difference)), oneDecimal(average))
                difference > 0 -> stringResource(R.string.explain_worse, strokes, oneDecimal(difference), oneDecimal(average))
                else -> stringResource(R.string.explain_level, strokes, oneDecimal(average))
            }
        is Sentence.NineHoleEquivalent -> stringResource(R.string.explain_nine_holes, strokes, per18)
        Sentence.DamageNotTracked -> stringResource(R.string.explain_damage_not_tracked)
        is Sentence.Damage -> {
            val p =
                when (penalties) {
                    null -> stringResource(R.string.explain_penalties_not_tracked)
                    0 -> stringResource(R.string.explain_no_penalties)
                    else -> pluralStringResource(R.plurals.explain_penalties, penalties, penalties)
                }
            val b =
                when (blowUps) {
                    null -> stringResource(R.string.explain_blow_ups_not_tracked)
                    0 -> stringResource(R.string.explain_no_blow_ups)
                    else -> pluralStringResource(R.plurals.explain_blow_ups, blowUps, blowUps)
                }
            stringResource(R.string.explain_damage, p, b)
        }
        Sentence.ConsistencyNeedsRounds -> stringResource(R.string.explain_consistency_needs_rounds)
        Sentence.ConsistencyAtOrBetter -> stringResource(R.string.explain_consistency_at_or_better)
        is Sentence.ConsistencyWorse -> stringResource(R.string.explain_consistency_worse, oneDecimal(worseBy), oneDecimal(spread))
        Sentence.HabitsUnknown -> stringResource(R.string.explain_habits_unknown)
        is Sentence.Habits ->
            stringResource(
                R.string.explain_habits,
                pluralStringResource(R.plurals.rounds_count, rounds, rounds),
                pluralStringResource(R.plurals.practice_sessions_count, practice, practice),
            )
        Sentence.Reflected -> stringResource(R.string.explain_reflected)
        Sentence.NotReflected -> stringResource(R.string.explain_not_reflected)
    }
}

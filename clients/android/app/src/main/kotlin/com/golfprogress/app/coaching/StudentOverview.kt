package com.golfprogress.app.coaching

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
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.DashboardRoundSummary
import com.golfprogress.api.generated.models.RoundTimelinePointResponse
import com.golfprogress.api.generated.models.StudentOverviewResponse
import com.golfprogress.api.generated.models.StudentReflectionResponse
import com.golfprogress.app.R
import com.golfprogress.app.data.StudentRepository
import com.golfprogress.app.goals.YourGoalsForStudent
import com.golfprogress.app.rounds.Fact
import com.golfprogress.app.ui.common.Confidence
import com.golfprogress.app.ui.common.ConfidenceBadge
import com.golfprogress.app.ui.common.ConfirmDialog
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.MonthChange
import com.golfprogress.app.ui.common.SectionCard
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.currentLocale
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.common.load
import com.golfprogress.app.ui.common.oneDecimal
import com.golfprogress.app.ui.theme.DesignTokens
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.text.NumberFormat
import java.util.UUID
import javax.inject.Inject

data class StudentOverviewState(
    /** Loaded with null: the coach no longer coaches them. */
    val overview: Load<StudentOverviewResponse?> = Load.Loading,
    val confirmStop: Boolean = false,
    val stopping: Boolean = false,
    /** Why stopping failed: an [ApiProblem] or a connection error. */
    val stopError: Throwable? = null,
    /** Set once stopped: the screen goes back to Students. */
    val stopped: Boolean = false,
)

/** The fitness score of the oldest and newest scored recent round: one score isn't a trend. */
data class FitnessTrend(
    val first: Int,
    val last: Int,
    val rounds: Int,
)

fun fitnessTrend(rounds: List<RoundTimelinePointResponse>): FitnessTrend? {
    val scores = rounds.mapNotNull { it.fitnessScoreTotal }
    return if (scores.size < 2) null else FitnessTrend(scores.first(), scores.last(), scores.size)
}

/**
 * A student's overview (#208, docs/coach-access.md → "Screens"): what a coach needs before a
 * lesson, from GET /coach/students/{golferId}/overview, and Stop coaching after asking.
 */
@HiltViewModel
class StudentOverviewViewModel
    @Inject
    constructor(
        private val repository: StudentRepository,
        savedState: SavedStateHandle,
    ) : ViewModel() {
        val golferId: UUID = checkNotNull(savedState.studentId())
        private val mutableState = MutableStateFlow(StudentOverviewState())
        val state: StateFlow<StudentOverviewState> = mutableState.asStateFlow()

        init {
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(overview = Load.Loading) }
            reload()
        }

        /** Back in view, e.g. after setting a goal: quietly, keeping what's shown if it fails. */
        fun refresh() {
            val current = state.value
            if (current.overview is Load.Loaded && !current.stopped) reload()
        }

        private fun reload() {
            viewModelScope.launch {
                val overview = load { repository.overview(golferId) }
                mutableState.update {
                    if (overview is Load.Failed && it.overview is Load.Loaded) it else it.copy(overview = overview)
                }
            }
        }

        fun askStop() = mutableState.update { it.copy(confirmStop = true, stopError = null) }

        fun cancelStop() = mutableState.update { if (it.stopping) it else it.copy(confirmStop = false, stopError = null) }

        fun confirmStop() {
            if (state.value.stopping) return
            mutableState.update { it.copy(stopping = true, stopError = null) }
            viewModelScope.launch {
                val error = (load { repository.stopCoaching(golferId) } as? Load.Failed)?.error
                // Removed by the golfer meanwhile: what was asked for has happened.
                val done = error == null || (error is ApiProblem && error.status == 404)
                mutableState.update {
                    if (done) {
                        it.copy(stopping = false, confirmStop = false, stopped = true)
                    } else {
                        it.copy(stopping = false, stopError = error)
                    }
                }
            }
        }
    }

/** Where the overview leads: the student's own screens, read-only. */
data class StudentOverviewActions(
    val onOpenRound: (UUID) -> Unit = {},
    val onOpenRounds: () -> Unit = {},
    val onOpenPractice: () -> Unit = {},
    val onOpenGoal: (UUID) -> Unit = {},
    val onSetGoal: () -> Unit = {},
    val onStopped: () -> Unit = {},
)

@Composable
fun StudentOverviewScreen(
    actions: StudentOverviewActions,
    viewModel: StudentOverviewViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.refresh() }
    LaunchedEffect(state.stopped) { if (state.stopped) actions.onStopped() }
    StudentOverviewContent(state, actions, online, viewModel::retry, viewModel::askStop)

    if (state.confirmStop) {
        ConfirmDialog(
            title = R.string.stop_coaching_title,
            body = R.string.stop_coaching_body,
            confirm = R.string.stop_coaching,
            busy = state.stopping,
            error =
                state.stopError?.let {
                    stringResource(if (it is ApiProblem) R.string.stop_coaching_failed else R.string.auth_cant_connect)
                },
            onConfirm = viewModel::confirmStop,
            onDismiss = viewModel::cancelStop,
        )
    }
}

@Composable
fun StudentOverviewContent(
    state: StudentOverviewState,
    actions: StudentOverviewActions,
    online: Boolean = true,
    onRetry: () -> Unit = {},
    onStop: () -> Unit = {},
) {
    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        when (val loaded = state.overview) {
            Load.Loading -> Skeleton(blocks = 4)
            is Load.Failed -> LoadError(R.string.student_overview_load_error, onRetry)
            is Load.Loaded -> {
                val student = loaded.value
                if (student == null) {
                    // Removed by the golfer, or never a student: nothing about them to show.
                    Text(stringResource(R.string.student_gone), style = MaterialTheme.typography.bodyLarge)
                } else {
                    Overview(student, actions, online)
                    OutlinedButton(
                        onClick = onStop,
                        enabled = online,
                        modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
                    ) { Text(stringResource(R.string.stop_coaching), color = MaterialTheme.colorScheme.error) }
                }
            }
        }
    }
}

@Composable
private fun Overview(
    student: StudentOverviewResponse,
    actions: StudentOverviewActions,
    online: Boolean,
) {
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        Text(student.displayName, style = MaterialTheme.typography.headlineMedium, modifier = Modifier.semantics { heading() })
        Text(
            studentShares(student.shareReflections, student.sharePractice),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }

    val lastRound = student.lastRound
    if (lastRound == null) {
        Text(stringResource(R.string.student_no_rounds, student.displayName), style = MaterialTheme.typography.bodyLarge)
    } else {
        LastRound(lastRound, actions.onOpenRound)
        student.nextFocus?.let { lever ->
            SectionCard(R.string.their_next_focus) {
                Text(lever.title, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.SemiBold)
                Text(lever.recommendation, style = MaterialTheme.typography.bodyLarge)
                Text(lever.rationale, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        student.baseline?.let { baseline ->
            baseline.avgScore?.let { average ->
                SectionCard(R.string.baseline) {
                    Stat(R.string.baseline_average, oneDecimal(average, currentLocale()))
                    Text(
                        pluralStringResource(R.plurals.student_baseline_based_on, baseline.sampleSize, baseline.sampleSize),
                        style = MaterialTheme.typography.bodyMedium,
                    )
                    Confidence.of(baseline.confidenceLevel.value)?.let { ConfidenceBadge(it, explain = true) }
                }
            }
        }
        student.monthlyFitnessAvg?.let { average ->
            SectionCard(R.string.this_month) {
                Stat(R.string.month_average, NumberFormat.getIntegerInstance(currentLocale()).format(average))
                student.monthlyFitnessChange?.let { MonthChange(it) }
            }
        }
        RecentRounds(student.recentRounds, actions)
    }

    if (student.sharePractice) {
        YourGoalsForStudent(student.activeGoals, actions.onOpenGoal, actions.onSetGoal, online)
        OutlinedButton(
            onClick = actions.onOpenPractice,
            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
        ) { Text(stringResource(R.string.practice_sessions)) }
    }

    Reflections(student, actions.onOpenRound)
}

@Composable
private fun LastRound(
    round: DashboardRoundSummary,
    onOpenRound: (UUID) -> Unit,
) {
    SectionCard(R.string.last_round) {
        Link(
            pluralStringResource(R.plurals.last_round_link, round.totalStrokes, formatDate(round.playedAt), round.totalStrokes),
        ) { onOpenRound(round.roundId) }
        val score = round.fitnessScore
        val confidence = Confidence.of(round.fitnessConfidence?.value)
        if (score != null && confidence != null) {
            val description = stringResource(R.string.fitness_score_value, score)
            Row(horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md), verticalAlignment = Alignment.CenterVertically) {
                Text(
                    score.toString(),
                    style = MaterialTheme.typography.displaySmall,
                    modifier = Modifier.semantics { contentDescription = description },
                )
                ConfidenceBadge(confidence, explain = true)
            }
        } else {
            Text(stringResource(R.string.not_scored_yet), style = MaterialTheme.typography.bodyLarge)
        }
    }
}

@Composable
private fun RecentRounds(
    rounds: List<RoundTimelinePointResponse>,
    actions: StudentOverviewActions,
) {
    SectionCard(R.string.recent_rounds) {
        fitnessTrend(rounds)?.let {
            Text(stringResource(R.string.student_trend, it.first, it.last, it.rounds), style = MaterialTheme.typography.bodyLarge)
        }
        // Newest first, as in the round list.
        rounds.asReversed().forEach { round ->
            val strokes =
                pluralStringResource(R.plurals.last_round_link, round.totalStrokes, formatDate(round.playedAt), round.totalStrokes)
            val score = round.fitnessScoreTotal?.let { stringResource(R.string.fitness_score_value, it) }
            Link(listOfNotNull(strokes, score).joinToString(" · ")) { actions.onOpenRound(round.roundId) }
        }
        OutlinedButton(
            onClick = actions.onOpenRounds,
            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
        ) { Text(stringResource(R.string.all_rounds)) }
    }
}

@Composable
private fun Reflections(
    student: StudentOverviewResponse,
    onOpenRound: (UUID) -> Unit,
) {
    SectionCard(R.string.latest_reflections) {
        when {
            !student.shareReflections ->
                Text(stringResource(R.string.reflections_not_shared, student.displayName), style = MaterialTheme.typography.bodyLarge)
            student.latestReflections.isEmpty() ->
                Text(stringResource(R.string.no_reflections_yet), style = MaterialTheme.typography.bodyLarge)
            else -> student.latestReflections.forEach { Reflection(it, onOpenRound) }
        }
    }
}

@Composable
private fun Reflection(
    reflection: StudentReflectionResponse,
    onOpenRound: (UUID) -> Unit,
) {
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        val roundId = reflection.roundId
        val playedAt = reflection.playedAt
        if (roundId != null && playedAt != null) Link(formatDate(playedAt)) { onOpenRound(roundId) }
        reflection.focusRating?.let { Fact(stringResource(R.string.focus), stringResource(R.string.out_of_five, it)) }
        reflection.moodRating?.let { Fact(stringResource(R.string.mood), stringResource(R.string.out_of_five, it)) }
        reflection.whatWorked?.let { Fact(stringResource(R.string.what_worked), it) }
        reflection.whatToRepeat?.let { Fact(stringResource(R.string.what_to_repeat), it) }
        reflection.oneChangeNext?.let { Fact(stringResource(R.string.one_change_next), it) }
    }
}

/** A row that opens something: the text in the link colour, and a chevron TalkBack skips. */
@Composable
private fun Link(
    text: String,
    onClick: () -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget).clickable(role = Role.Button, onClick = onClick),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            text,
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.primary,
            fontWeight = FontWeight.SemiBold,
            modifier = Modifier.weight(1f),
        )
        Text("›", color = MaterialTheme.colorScheme.primary, modifier = Modifier.clearAndSetSemantics {})
    }
}

@Composable
private fun Stat(
    label: Int,
    value: String,
) {
    Column {
        Text(stringResource(label), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, style = MaterialTheme.typography.titleLarge)
    }
}

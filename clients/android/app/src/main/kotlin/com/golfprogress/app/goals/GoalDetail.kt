package com.golfprogress.app.goals

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.SessionType
import com.golfprogress.api.generated.models.GoalOutcomeResponse
import com.golfprogress.api.generated.models.PracticeGoalDetailResponse
import com.golfprogress.api.generated.models.PracticeGoalResponse
import com.golfprogress.api.knownSessionType
import com.golfprogress.app.R
import com.golfprogress.app.data.GoalsRepository
import com.golfprogress.app.practice.sessionTypeName
import com.golfprogress.app.rounds.Fact
import com.golfprogress.app.ui.common.Confidence
import com.golfprogress.app.ui.common.ConfidenceBadge
import com.golfprogress.app.ui.common.ConfirmDialog
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.SectionCard
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.currentLocale
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.common.load
import com.golfprogress.app.ui.common.oneDecimal
import com.golfprogress.app.ui.theme.DesignTokens
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.util.UUID
import javax.inject.Inject

data class GoalDetailState(
    /** Loaded with null: the goal is no longer there. */
    val goal: Load<PracticeGoalDetailResponse?> = Load.Loading,
    /** Null while it loads or when it couldn't: the rest of the goal still shows. */
    val outcome: Load<GoalOutcomeResponse> = Load.Loading,
    val confirmDismiss: Boolean = false,
    val changing: Boolean = false,
    val changeFailed: Boolean = false,
)

/**
 * A goal from the golfer's coach (#231): what it is, progress, the linked sessions and
 * "Did it work?" (#236); Mark done, and Dismiss after asking.
 */
@HiltViewModel
class GoalDetailViewModel
    @Inject
    constructor(
        private val goals: GoalsRepository,
        savedState: SavedStateHandle,
    ) : ViewModel() {
        private val id: UUID = UUID.fromString(checkNotNull(savedState.get<String>(GOAL_ID)))
        private val mutableState = MutableStateFlow(GoalDetailState())
        val state: StateFlow<GoalDetailState> = mutableState.asStateFlow()

        init {
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(goal = Load.Loading, outcome = Load.Loading) }
            reload()
        }

        fun retryOutcome() {
            mutableState.update { it.copy(outcome = Load.Loading) }
            viewModelScope.launch {
                val outcome = load { goals.outcome(id) }
                mutableState.update { it.copy(outcome = outcome) }
            }
        }

        private fun reload() {
            viewModelScope.launch {
                coroutineScope {
                    val goal = async { load { goals.goal(id) } }
                    val outcome = async { load { goals.outcome(id) } }
                    val loaded = goal.await()
                    mutableState.update { it.copy(goal = loaded, outcome = outcome.await()) }
                }
            }
        }

        fun markDone() = change(done = true)

        fun askDismiss() = mutableState.update { it.copy(confirmDismiss = true, changeFailed = false) }

        fun cancelDismiss() = mutableState.update { if (it.changing) it else it.copy(confirmDismiss = false) }

        fun confirmDismiss() = change(done = false)

        private fun change(done: Boolean) {
            if (state.value.changing) return
            mutableState.update { it.copy(changing = true, changeFailed = false) }
            viewModelScope.launch {
                val result = load { goals.setStatus(id, done) }
                mutableState.update { current ->
                    when (result) {
                        is Load.Loaded -> {
                            val detail = (current.goal as? Load.Loaded)?.value
                            current.copy(
                                changing = false,
                                confirmDismiss = false,
                                goal = Load.Loaded(detail?.copy(goal = result.value)),
                            )
                        }
                        else -> current.copy(changing = false, changeFailed = true)
                    }
                }
            }
        }

        companion object {
            /** The navigation argument naming the goal. */
            const val GOAL_ID = "goalId"
        }
    }

@Composable
fun GoalDetailScreen(
    onOpenSession: (UUID) -> Unit,
    viewModel: GoalDetailViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        when (val loaded = state.goal) {
            Load.Loading -> Skeleton(blocks = 3)
            is Load.Failed -> LoadError(R.string.goal_load_error, viewModel::retry)
            is Load.Loaded -> {
                val detail = loaded.value
                if (detail == null) {
                    Text(stringResource(R.string.goal_gone), style = MaterialTheme.typography.bodyLarge)
                } else {
                    GoalContent(detail, state.outcome, viewModel::retryOutcome, onOpenSession)
                    if (detail.goal.isOpen) {
                        if (state.changeFailed && !state.confirmDismiss) {
                            Text(stringResource(R.string.goal_change_failed), color = MaterialTheme.colorScheme.error)
                        }
                        Button(
                            onClick = viewModel::markDone,
                            enabled = online && !state.changing,
                            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
                        ) { Text(stringResource(R.string.mark_goal_done)) }
                        OutlinedButton(
                            onClick = viewModel::askDismiss,
                            enabled = online && !state.changing,
                            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
                        ) { Text(stringResource(R.string.dismiss_goal)) }
                    }
                }
            }
        }
    }

    if (state.confirmDismiss) {
        ConfirmDialog(
            title = R.string.dismiss_goal_title,
            body = R.string.dismiss_goal_body,
            confirm = R.string.dismiss_goal,
            busy = state.changing,
            error = if (state.changeFailed) stringResource(R.string.goal_change_failed) else null,
            onConfirm = viewModel::confirmDismiss,
            onDismiss = viewModel::cancelDismiss,
        )
    }
}

@Composable
private fun GoalContent(
    detail: PracticeGoalDetailResponse,
    outcome: Load<GoalOutcomeResponse>,
    onRetryOutcome: () -> Unit,
    onOpenSession: (UUID) -> Unit,
) {
    val goal = detail.goal
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        Text(goal.title, style = MaterialTheme.typography.headlineMedium, modifier = Modifier.semantics { heading() })
        val from = goal.coachName?.let { stringResource(R.string.goal_from, it) } ?: stringResource(R.string.goal_from_former_coach)
        Text(
            listOf(
                from,
                stringResource(R.string.goal_period, formatDate(goal.startsOn), formatDate(goal.endsOn)),
                stringResource(statusLabel(goal.status)),
            ).joinToString(" · "),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }

    SectionCard(R.string.goal_what) {
        goal.details?.let { Text(it, style = MaterialTheme.typography.bodyLarge) }
        GoalProgress(goal)
        goal.focusArea?.let { Fact(stringResource(R.string.goal_focus_area), stringResource(focusLabel(it))) }
        goal.sessionType?.let { Fact(stringResource(R.string.session_type), sessionTypeName(SessionType.of(it))) }
    }

    SectionCard(R.string.did_it_work) {
        when (outcome) {
            Load.Loading -> Skeleton(blocks = 1)
            is Load.Failed -> LoadError(R.string.outcome_load_error, onRetryOutcome)
            is Load.Loaded -> Outcome(outcome.value)
        }
    }

    SectionCard(R.string.goal_sessions) {
        if (detail.sessions.isEmpty()) {
            Text(stringResource(R.string.goal_no_sessions), style = MaterialTheme.typography.bodyLarge)
        }
        detail.sessions.forEach { session ->
            val text = formatDate(session.practicedAt) + " · " + sessionTypeName(session.knownSessionType)
            Text(
                text,
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.primary,
                modifier =
                    Modifier
                        .fillMaxWidth()
                        .heightIn(min = DesignTokens.touchTarget)
                        .clickable(role = Role.Button) { onOpenSession(session.practiceSessionId) }
                        .padding(vertical = DesignTokens.Spacing.sm),
            )
        }
    }
}

/** "3 of 6 sessions", with a bar that repeats it for sighted users. */
@Composable
fun GoalProgress(goal: PracticeGoalResponse) {
    val sessions = pluralStringResource(R.plurals.goal_progress, goal.targetSessions, goal.linkedSessions, goal.targetSessions)
    val text =
        listOfNotNull(
            sessions,
            stringResource(R.string.goal_ended).takeIf { goal.status == PracticeGoalResponse.Status.Ended },
            stringResource(R.string.goal_reached).takeIf { goal.reached && goal.status == PracticeGoalResponse.Status.Active },
        ).joinToString(" · ")
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        Text(text, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        LinearProgressIndicator(
            progress = { (goal.linkedSessions.toFloat() / goal.targetSessions).coerceAtMost(1f) },
            modifier = Modifier.fillMaxWidth().clearAndSetSemantics {},
        )
    }
}

@Composable
private fun Outcome(outcome: GoalOutcomeResponse) {
    val locale = currentLocale()
    val stat = outcome.stat?.let { stringResource(statLabel(it)) }
    Text(
        when (outcome.verdict) {
            GoalOutcomeResponse.Verdict.Improved -> stringResource(R.string.verdict_improved, stat.orEmpty())
            GoalOutcomeResponse.Verdict.Worse -> stringResource(R.string.verdict_worse, stat.orEmpty())
            GoalOutcomeResponse.Verdict.Same -> stringResource(R.string.verdict_same, stat.orEmpty())
            GoalOutcomeResponse.Verdict.CantTellYet -> stringResource(R.string.verdict_cant_tell)
            GoalOutcomeResponse.Verdict.NotMeasured -> stringResource(R.string.verdict_not_measured)
        },
        style = MaterialTheme.typography.bodyLarge,
    )
    if (stat == null) return
    val roundsBefore = pluralStringResource(R.plurals.outcome_rounds, outcome.roundsBefore, outcome.roundsBefore)
    val roundsDuring = pluralStringResource(R.plurals.outcome_rounds, outcome.roundsDuring, outcome.roundsDuring)
    val value = { v: Double?, rounds: String -> (v?.let { oneDecimal(it, locale) } ?: "–") + " " + rounds }
    Fact(stringResource(R.string.outcome_before), value(outcome.before, roundsBefore))
    Fact(stringResource(R.string.outcome_during), value(outcome.during, roundsDuring))
    Text(
        stat + " · " + stringResource(R.string.outcome_window, formatDate(outcome.duringFrom), formatDate(outcome.duringTo)),
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
    Confidence.of(outcome.confidence.value)?.let { ConfidenceBadge(it, explain = true) }
}

private fun statusLabel(status: PracticeGoalResponse.Status): Int =
    when (status) {
        PracticeGoalResponse.Status.Active -> R.string.goal_active
        PracticeGoalResponse.Status.Ended -> R.string.goal_ended
        PracticeGoalResponse.Status.Done -> R.string.goal_done
        PracticeGoalResponse.Status.Dismissed -> R.string.goal_dismissed
    }

private fun focusLabel(area: PracticeGoalResponse.FocusArea): Int =
    when (area) {
        PracticeGoalResponse.FocusArea.Putting -> R.string.goal_putting
        PracticeGoalResponse.FocusArea.Penalties -> R.string.goal_penalties
        PracticeGoalResponse.FocusArea.TeeShots -> R.string.goal_tee_shots
        PracticeGoalResponse.FocusArea.Consistency -> R.string.goal_consistency
    }

private fun statLabel(stat: GoalOutcomeResponse.Stat): Int =
    when (stat) {
        GoalOutcomeResponse.Stat.PuttsPer18 -> R.string.stat_putts_per_18
        GoalOutcomeResponse.Stat.PenaltiesPer18 -> R.string.stat_penalties_per_18
        GoalOutcomeResponse.Stat.FairwaysPer18 -> R.string.stat_fairways_per_18
        GoalOutcomeResponse.Stat.ScoreSpread -> R.string.stat_score_spread
    }

/** Goals from your coach, above the sessions on Practice (#231); nothing without any. */
@Composable
fun GoalsFromYourCoach(
    goals: List<PracticeGoalResponse>,
    onOpen: (UUID) -> Unit,
) {
    if (goals.isEmpty()) return
    SectionCard(R.string.goals_from_your_coach) {
        goals.forEach { goal ->
            Column(
                modifier =
                    Modifier
                        .fillMaxWidth()
                        .heightIn(min = DesignTokens.touchTarget)
                        .clickable(role = Role.Button) { onOpen(goal.practiceGoalId) }
                        .semantics(mergeDescendants = true) {}
                        .padding(vertical = DesignTokens.Spacing.xs),
                verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs),
            ) {
                Text(goal.title, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.SemiBold)
                GoalProgress(goal)
            }
        }
    }
}

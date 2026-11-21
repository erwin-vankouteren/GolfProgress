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
import androidx.compose.runtime.LaunchedEffect
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
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.SessionType
import com.golfprogress.api.generated.models.GoalOutcomeResponse
import com.golfprogress.api.generated.models.PracticeGoalDetailResponse
import com.golfprogress.api.generated.models.PracticeGoalResponse
import com.golfprogress.api.knownSessionType
import com.golfprogress.app.R
import com.golfprogress.app.coaching.studentId
import com.golfprogress.app.data.GoalReader
import com.golfprogress.app.data.GoalsRepository
import com.golfprogress.app.data.StudentGoals
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
    /** The coach asked to remove the goal (#208), waiting for them to confirm. */
    val confirmRemove: Boolean = false,
    val removeProblem: RemoveGoalProblem? = null,
    /** Set once removed: the screen goes back. */
    val removed: Boolean = false,
)

/** Why the coach couldn't remove a goal. */
enum class RemoveGoalProblem { HasSessions, Failed, CantConnect }

/**
 * A goal from the golfer's coach (#231): what it is, progress, the linked sessions and
 * "Did it work?" (#236); Mark done, and Dismiss after asking. On a student's screens, a goal
 * the coach set for them (#208), which they can change, or remove after asking.
 */
@HiltViewModel
class GoalDetailViewModel
    @Inject
    constructor(
        private val goals: GoalsRepository,
        savedState: SavedStateHandle,
    ) : ViewModel() {
        val goalId: UUID = UUID.fromString(checkNotNull(savedState.get<String>(GOAL_ID)))
        private val id get() = goalId

        /** The student the goal is for, when the coach looks at it; null for the golfer's own. */
        val studentId: UUID? = savedState.studentId()
        private val studentGoals: StudentGoals? = studentId?.let(goals::student)
        private val reader: GoalReader = studentGoals ?: goals
        private val mutableState = MutableStateFlow(GoalDetailState())
        val state: StateFlow<GoalDetailState> = mutableState.asStateFlow()

        init {
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(goal = Load.Loading, outcome = Load.Loading) }
            reload()
        }

        /** Back in view, e.g. after the coach changed it: quietly, keeping what's shown if it fails. */
        fun refresh() {
            val current = state.value
            if (current.goal is Load.Loaded && !current.removed) reload()
        }

        fun retryOutcome() {
            mutableState.update { it.copy(outcome = Load.Loading) }
            viewModelScope.launch {
                val outcome = load { reader.outcome(id) }
                mutableState.update { it.copy(outcome = outcome) }
            }
        }

        fun askRemove() = mutableState.update { it.copy(confirmRemove = true, removeProblem = null) }

        fun cancelRemove() = mutableState.update { if (it.changing) it else it.copy(confirmRemove = false, removeProblem = null) }

        fun confirmRemove() {
            val student = studentGoals ?: return
            if (state.value.changing) return
            mutableState.update { it.copy(changing = true, removeProblem = null) }
            viewModelScope.launch {
                val error = (load { student.remove(id) } as? Load.Failed)?.error
                val status = (error as? ApiProblem)?.status
                mutableState.update {
                    when {
                        // Removed, or gone already: what was asked for has happened.
                        error == null || status == 404 -> it.copy(changing = false, confirmRemove = false, removed = true)
                        status == 409 -> it.copy(changing = false, removeProblem = RemoveGoalProblem.HasSessions)
                        error is ApiProblem -> it.copy(changing = false, removeProblem = RemoveGoalProblem.Failed)
                        else -> it.copy(changing = false, removeProblem = RemoveGoalProblem.CantConnect)
                    }
                }
            }
        }

        private fun reload() {
            viewModelScope.launch {
                coroutineScope {
                    val goal = async { load { reader.goal(id) } }
                    val outcome = async { load { reader.outcome(id) } }
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
    /** On a student's screens (#208): Change goal opens the form for this goal. */
    onChange: (UUID) -> Unit = {},
    /** On a student's screens: once the coach removed it. */
    onRemoved: () -> Unit = {},
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.refresh() }
    LaunchedEffect(state.removed) { if (state.removed) onRemoved() }
    val forCoach = viewModel.studentId != null
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
                    GoalContent(detail, state.outcome, viewModel::retryOutcome, onOpenSession, forCoach)
                    // The coach changes or removes their goal until the golfer marks it done or dismisses it.
                    if (forCoach && detail.goal.isOpen) {
                        Button(
                            onClick = { onChange(viewModel.goalId) },
                            enabled = online && !state.changing,
                            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
                        ) { Text(stringResource(R.string.change_goal)) }
                        OutlinedButton(
                            onClick = viewModel::askRemove,
                            enabled = online && !state.changing,
                            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
                        ) { Text(stringResource(R.string.remove_goal)) }
                    } else if (!forCoach && detail.goal.isOpen) {
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

    if (state.confirmRemove) {
        ConfirmDialog(
            title = R.string.remove_goal_title,
            body = R.string.remove_goal_body,
            confirm = R.string.remove_goal,
            busy = state.changing,
            error =
                state.removeProblem?.let {
                    stringResource(
                        when (it) {
                            RemoveGoalProblem.HasSessions -> R.string.goal_has_sessions
                            RemoveGoalProblem.Failed -> R.string.goal_change_failed
                            RemoveGoalProblem.CantConnect -> R.string.auth_cant_connect
                        },
                    )
                },
            onConfirm = viewModel::confirmRemove,
            onDismiss = viewModel::cancelRemove,
        )
    }
}

@Composable
private fun GoalContent(
    detail: PracticeGoalDetailResponse,
    outcome: Load<GoalOutcomeResponse>,
    onRetryOutcome: () -> Unit,
    onOpenSession: (UUID) -> Unit,
    /** The coach's own goal for a student: who it's from goes without saying. */
    forCoach: Boolean = false,
) {
    val goal = detail.goal
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        Text(goal.title, style = MaterialTheme.typography.headlineMedium, modifier = Modifier.semantics { heading() })
        val from = goal.coachName?.let { stringResource(R.string.goal_from, it) } ?: stringResource(R.string.goal_from_former_coach)
        Text(
            listOfNotNull(
                from.takeUnless { forCoach },
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
            Text(
                stringResource(if (forCoach) R.string.goal_no_sessions_coach else R.string.goal_no_sessions),
                style = MaterialTheme.typography.bodyLarge,
            )
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
    SectionCard(R.string.goals_from_your_coach) { GoalRows(goals, onOpen) }
}

/**
 * The goals a coach set for a student (#208), on the student's practice and overview, with
 * Set a practice goal: shown even before the first, as that's where it's set.
 */
@Composable
fun YourGoalsForStudent(
    goals: List<PracticeGoalResponse>,
    onOpen: (UUID) -> Unit,
    onSetGoal: () -> Unit,
    online: Boolean = isOnline(),
) {
    SectionCard(R.string.your_goals_for_student) {
        if (goals.isEmpty()) {
            Text(
                stringResource(R.string.no_goals_yet),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        GoalRows(goals, onOpen)
        OutlinedButton(
            onClick = onSetGoal,
            enabled = online,
            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
        ) { Text(stringResource(R.string.set_practice_goal)) }
    }
}

@Composable
private fun GoalRows(
    goals: List<PracticeGoalResponse>,
    onOpen: (UUID) -> Unit,
) {
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

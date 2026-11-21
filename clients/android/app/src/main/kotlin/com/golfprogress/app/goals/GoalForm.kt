package com.golfprogress.app.goals

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardType
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.SessionType
import com.golfprogress.api.generated.models.PracticeGoalRequest
import com.golfprogress.api.generated.models.PracticeGoalResponse
import com.golfprogress.app.R
import com.golfprogress.app.coaching.studentId
import com.golfprogress.app.data.GoalsRepository
import com.golfprogress.app.practice.sessionTypeName
import com.golfprogress.app.rounds.DateField
import com.golfprogress.app.rounds.FormProblem
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.common.load
import com.golfprogress.app.ui.theme.DesignTokens
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.LocalDate
import java.time.temporal.ChronoUnit
import java.util.UUID
import javax.inject.Inject

// ---------------------------------------------------------------- rules (as the web app's goals.ts)

const val GOAL_TITLE_MAX = 100
const val GOAL_DETAILS_MAX = 1000
const val GOAL_TARGET_MAX = 50
const val GOAL_DAYS_MAX = 56L

/** The form's fields, with the API's names for its 400s. */
enum class GoalField(
    val apiName: String,
) {
    Title("title"),
    Details("details"),
    FocusArea("focusArea"),
    SessionType("sessionType"),
    TargetSessions("targetSessions"),
    StartsOn("startsOn"),
    EndsOn("endsOn"),
}

sealed interface GoalError {
    data object Required : GoalError

    data class TooLong(
        val max: Int,
    ) : GoalError

    data class TargetRange(
        val max: Int,
    ) : GoalError

    data object EndBeforeStart : GoalError

    data class PeriodTooLong(
        val weeks: Int,
    ) : GoalError

    data object EndInPast : GoalError
}

data class GoalFormValues(
    val title: String = "",
    val details: String = "",
    /** Null: none. */
    val focusArea: PracticeGoalRequest.FocusArea? = null,
    /** Null: any type of session. */
    val sessionType: PracticeGoalRequest.SessionType? = null,
    val targetSessions: String = "6",
    val startsOn: LocalDate,
    val endsOn: LocalDate,
)

/** A new goal: from today, for four weeks, six sessions. */
fun emptyGoal(today: LocalDate): GoalFormValues = GoalFormValues(startsOn = today, endsOn = today.plusDays(28))

/** An existing goal in the form, to change it. A choice this build doesn't know is left empty. */
fun goalToForm(goal: PracticeGoalResponse): GoalFormValues =
    GoalFormValues(
        title = goal.title,
        details = goal.details.orEmpty(),
        focusArea = goal.focusArea?.let { area -> PracticeGoalRequest.FocusArea.entries.firstOrNull { it.value == area.value } },
        sessionType = PracticeGoalRequest.SessionType.entries.firstOrNull { it.value == goal.sessionType },
        targetSessions = goal.targetSessions.toString(),
        startsOn = LocalDate.parse(goal.startsOn),
        endsOn = LocalDate.parse(goal.endsOn),
    )

private fun String.length() = trim().codePointCount(0, trim().length)

fun validateGoal(
    values: GoalFormValues,
    today: LocalDate,
): Map<GoalField, GoalError> =
    buildMap {
        when {
            values.title.length() == 0 -> put(GoalField.Title, GoalError.Required)
            values.title.length() > GOAL_TITLE_MAX -> put(GoalField.Title, GoalError.TooLong(GOAL_TITLE_MAX))
        }
        if (values.details.length() > GOAL_DETAILS_MAX) put(GoalField.Details, GoalError.TooLong(GOAL_DETAILS_MAX))
        val target = values.targetSessions.trim().toIntOrNull()
        if (target == null || target !in 1..GOAL_TARGET_MAX) put(GoalField.TargetSessions, GoalError.TargetRange(GOAL_TARGET_MAX))
        when {
            values.endsOn < values.startsOn -> put(GoalField.EndsOn, GoalError.EndBeforeStart)
            ChronoUnit.DAYS.between(values.startsOn, values.endsOn) > GOAL_DAYS_MAX ->
                put(GoalField.EndsOn, GoalError.PeriodTooLong((GOAL_DAYS_MAX / 7).toInt()))
            values.endsOn < today -> put(GoalField.EndsOn, GoalError.EndInPast)
        }
    }

/** The POST or PUT body; optional choices left empty go as null. */
fun GoalFormValues.toRequest(): PracticeGoalRequest =
    PracticeGoalRequest(
        title = title.trim(),
        endsOn = endsOn.toString(),
        details = details.trim().ifEmpty { null },
        focusArea = focusArea,
        sessionType = sessionType,
        targetSessions = targetSessions.trim().toIntOrNull() ?: 0,
        startsOn = startsOn.toString(),
    )

// ---------------------------------------------------------------- view model

data class GoalFormState(
    val values: GoalFormValues,
    /** Changing a goal: it loads first; null when setting a new one. Loaded with null: it's gone. */
    val editing: Load<PracticeGoalResponse?>? = null,
    val touched: Set<GoalField> = emptySet(),
    val submitted: Boolean = false,
    val apiErrors: Map<GoalField, String> = emptyMap(),
    val problem: FormProblem? = null,
    val saving: Boolean = false,
    /** Set once saved: the goal's id. */
    val savedId: UUID? = null,
    val today: LocalDate,
) {
    val errors: Map<GoalField, GoalError> get() = validateGoal(values, today)

    /** The API's message, or a rule once the field was touched or Save pressed. */
    fun messageFor(field: GoalField): GoalMessage? =
        apiErrors[field]?.let { GoalMessage.FromApi(it) }
            ?: errors[field]?.takeIf { submitted || field in touched }?.let { GoalMessage.Rule(it) }
}

sealed interface GoalMessage {
    data class Rule(
        val error: GoalError,
    ) : GoalMessage

    data class FromApi(
        val text: String,
    ) : GoalMessage
}

/**
 * Set a practice goal, or change one (#208, docs/coach-access.md → "Practice goals"): title,
 * details, focus area, session type, target sessions and period, for a student who shares
 * practice. With a goal id it changes that goal; the whole goal is sent again.
 */
@HiltViewModel
class GoalFormViewModel
    @Inject
    constructor(
        goals: GoalsRepository,
        savedState: SavedStateHandle,
    ) : ViewModel() {
        val studentId: UUID = checkNotNull(savedState.studentId())
        private val goalId: UUID? = savedState.get<String>(GoalDetailViewModel.GOAL_ID)?.let(UUID::fromString)
        private val student = goals.student(studentId)
        private val today = LocalDate.now()
        private val mutableState =
            MutableStateFlow(GoalFormState(values = emptyGoal(today), editing = goalId?.let { Load.Loading }, today = today))
        val state: StateFlow<GoalFormState> = mutableState.asStateFlow()

        init {
            if (goalId != null) loadGoal(goalId)
        }

        fun retry() {
            val id = goalId ?: return
            mutableState.update { it.copy(editing = Load.Loading) }
            loadGoal(id)
        }

        private fun loadGoal(id: UUID) {
            viewModelScope.launch {
                val loaded = load { student.goal(id)?.goal }
                mutableState.update {
                    val goal = (loaded as? Load.Loaded)?.value
                    it.copy(editing = loaded, values = goal?.let(::goalToForm) ?: it.values)
                }
            }
        }

        fun setTitle(text: String) = edit(GoalField.Title) { copy(title = text) }

        fun setDetails(text: String) = edit(GoalField.Details) { copy(details = text) }

        fun setFocusArea(area: PracticeGoalRequest.FocusArea?) = edit(GoalField.FocusArea) { copy(focusArea = area) }

        fun setSessionType(type: PracticeGoalRequest.SessionType?) = edit(GoalField.SessionType) { copy(sessionType = type) }

        fun setTargetSessions(text: String) = edit(GoalField.TargetSessions) { copy(targetSessions = text) }

        fun setStartsOn(date: LocalDate) = edit(GoalField.StartsOn) { copy(startsOn = date) }

        fun setEndsOn(date: LocalDate) = edit(GoalField.EndsOn) { copy(endsOn = date) }

        fun save() {
            val current = state.value
            if (current.saving) return
            if (current.errors.isNotEmpty()) {
                mutableState.update { it.copy(submitted = true) }
                return
            }
            mutableState.update { it.copy(submitted = true, saving = true, problem = null, apiErrors = emptyMap()) }
            viewModelScope.launch {
                try {
                    val request = current.values.toRequest()
                    val saved = goalId?.let { student.change(it, request) } ?: student.set(request)
                    mutableState.update { it.copy(saving = false, savedId = saved.practiceGoalId) }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: ApiProblem) {
                    val fields =
                        if (e.status == 400) {
                            GoalField.entries.mapNotNull { f -> e.messagesFor(f.apiName).firstOrNull()?.let { f to it } }.toMap()
                        } else {
                            emptyMap()
                        }
                    mutableState.update {
                        it.copy(saving = false, apiErrors = fields, problem = if (fields.isEmpty()) FormProblem.SaveFailed else null)
                    }
                } catch (_: Exception) {
                    mutableState.update { it.copy(saving = false, problem = FormProblem.CantConnect) }
                }
            }
        }

        private fun edit(
            field: GoalField,
            change: GoalFormValues.() -> GoalFormValues,
        ) = mutableState.update {
            it.copy(values = it.values.change(), touched = it.touched + field, apiErrors = it.apiErrors - field, problem = null)
        }
    }

// ---------------------------------------------------------------- screen

/** [onSaved] gets the goal's id once it's saved. */
@Composable
fun GoalFormScreen(
    onSaved: (UUID) -> Unit,
    viewModel: GoalFormViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.savedId) { state.savedId?.let(onSaved) }
    GoalFormContent(
        state = state,
        actions =
            GoalFormActions(
                onTitle = viewModel::setTitle,
                onDetails = viewModel::setDetails,
                onFocusArea = viewModel::setFocusArea,
                onSessionType = viewModel::setSessionType,
                onTargetSessions = viewModel::setTargetSessions,
                onStartsOn = viewModel::setStartsOn,
                onEndsOn = viewModel::setEndsOn,
                onSave = viewModel::save,
                onRetry = viewModel::retry,
            ),
        online = online,
    )
}

data class GoalFormActions(
    val onTitle: (String) -> Unit = {},
    val onDetails: (String) -> Unit = {},
    val onFocusArea: (PracticeGoalRequest.FocusArea?) -> Unit = {},
    val onSessionType: (PracticeGoalRequest.SessionType?) -> Unit = {},
    val onTargetSessions: (String) -> Unit = {},
    val onStartsOn: (LocalDate) -> Unit = {},
    val onEndsOn: (LocalDate) -> Unit = {},
    val onSave: () -> Unit = {},
    val onRetry: () -> Unit = {},
)

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun GoalFormContent(
    state: GoalFormState,
    actions: GoalFormActions,
    online: Boolean = true,
) {
    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        when (val editing = state.editing) {
            Load.Loading -> {
                Skeleton(blocks = 3)
                return@Column
            }
            is Load.Failed -> {
                LoadError(R.string.goal_load_error, actions.onRetry)
                return@Column
            }
            is Load.Loaded ->
                if (editing.value == null) {
                    Text(stringResource(R.string.goal_gone), style = MaterialTheme.typography.bodyLarge)
                    return@Column
                }
            null -> Unit
        }
        val values = state.values

        TextField(R.string.goal_title, values.title, actions.onTitle, state.messageFor(GoalField.Title))
        TextField(R.string.goal_details, values.details, actions.onDetails, state.messageFor(GoalField.Details), minLines = 3)

        Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
            Text(stringResource(R.string.goal_focus_area), style = MaterialTheme.typography.labelLarge)
            FlowRow(Modifier.selectableGroup(), horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
                Chip(stringResource(R.string.goal_no_focus_area), values.focusArea == null) { actions.onFocusArea(null) }
                PracticeGoalRequest.FocusArea.entries.forEach { area ->
                    Chip(stringResource(focusAreaLabel(area)), values.focusArea == area) { actions.onFocusArea(area) }
                }
            }
            Hint(stringResource(R.string.goal_focus_area_hint))
        }

        Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
            Text(stringResource(R.string.session_type), style = MaterialTheme.typography.labelLarge)
            FlowRow(Modifier.selectableGroup(), horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
                Chip(stringResource(R.string.goal_any_session_type), values.sessionType == null) { actions.onSessionType(null) }
                PracticeGoalRequest.SessionType.entries.forEach { type ->
                    Chip(sessionTypeName(SessionType.of(type.value)), values.sessionType == type) { actions.onSessionType(type) }
                }
            }
        }

        TextField(
            R.string.goal_target_sessions,
            values.targetSessions,
            actions.onTargetSessions,
            state.messageFor(GoalField.TargetSessions),
            keyboard = KeyboardType.Number,
        )
        DateField(
            values.startsOn,
            actions.onStartsOn,
            label = R.string.goal_starts,
            supporting = state.messageFor(GoalField.StartsOn)?.text(),
            isError = state.messageFor(GoalField.StartsOn) != null,
        )
        val endsMessage = state.messageFor(GoalField.EndsOn)
        DateField(
            values.endsOn,
            actions.onEndsOn,
            label = R.string.goal_ends,
            supporting = endsMessage?.text() ?: stringResource(R.string.goal_ends_hint),
            isError = endsMessage != null,
        )

        if (!online) {
            Text(
                stringResource(R.string.goal_offline),
                style = MaterialTheme.typography.bodyMedium,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
            )
        }
        state.problem?.let {
            Text(
                stringResource(if (it == FormProblem.SaveFailed) R.string.goal_save_failed else R.string.auth_cant_connect),
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
            )
        }
        Button(
            onClick = actions.onSave,
            enabled = online && !state.saving,
            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
        ) { Text(stringResource(if (state.saving) R.string.saving else R.string.save)) }
    }
}

@Composable
private fun TextField(
    @StringRes label: Int,
    value: String,
    onChange: (String) -> Unit,
    message: GoalMessage?,
    minLines: Int = 1,
    keyboard: KeyboardType = KeyboardType.Text,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onChange,
        label = { Text(stringResource(label)) },
        isError = message != null,
        supportingText = message?.let { { Text(it.text()) } },
        minLines = minLines,
        singleLine = minLines == 1,
        keyboardOptions = KeyboardOptions(keyboardType = keyboard),
        modifier = Modifier.fillMaxWidth(),
    )
}

@Composable
private fun Chip(
    label: String,
    selected: Boolean,
    onClick: () -> Unit,
) {
    FilterChip(
        selected = selected,
        onClick = onClick,
        label = { Text(label) },
        modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
    )
}

@Composable
private fun Hint(text: String) {
    Text(text, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
}

@Composable
private fun GoalMessage.text(): String =
    when (this) {
        is GoalMessage.FromApi -> text
        is GoalMessage.Rule ->
            when (val e = error) {
                GoalError.Required -> stringResource(R.string.goal_title_required)
                is GoalError.TooLong -> stringResource(R.string.at_most_characters, e.max)
                is GoalError.TargetRange -> stringResource(R.string.goal_target_range, e.max)
                GoalError.EndBeforeStart -> stringResource(R.string.goal_end_before_start)
                is GoalError.PeriodTooLong -> stringResource(R.string.goal_too_long, e.weeks)
                GoalError.EndInPast -> stringResource(R.string.goal_end_in_past)
            }
    }

private fun focusAreaLabel(area: PracticeGoalRequest.FocusArea): Int =
    when (area) {
        PracticeGoalRequest.FocusArea.Putting -> R.string.goal_putting
        PracticeGoalRequest.FocusArea.Penalties -> R.string.goal_penalties
        PracticeGoalRequest.FocusArea.TeeShots -> R.string.goal_tee_shots
        PracticeGoalRequest.FocusArea.Consistency -> R.string.goal_consistency
    }

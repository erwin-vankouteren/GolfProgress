package com.golfprogress.app.rounds

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.data.RoundsRepository
import com.golfprogress.app.data.UserRepository
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.load
import com.golfprogress.app.ui.common.valueOrNull
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.LocalDate
import java.util.UUID
import javax.inject.Inject

/** A message under a field: one of the form's own rules, or the API's words. */
sealed interface FieldMessage {
    data class Rule(
        val error: RoundError,
    ) : FieldMessage

    data class FromApi(
        val text: String,
    ) : FieldMessage
}

enum class FormProblem { SaveFailed, CantConnect }

data class RoundFormState(
    /** The profile, for the tracking mode and the typical score the stepper starts at. */
    val me: Load<UserResponse> = Load.Loading,
    /** Editing: the round as saved; null when logging a new one. Null inside means it's gone. */
    val editing: Load<RoundResponse?>? = null,
    val values: RoundFormValues = RoundFormValues(playedAt = LocalDate.now()),
    val touched: Set<RoundField> = emptySet(),
    val submitted: Boolean = false,
    val apiErrors: Map<RoundField, String> = emptyMap(),
    val problem: FormProblem? = null,
    val saving: Boolean = false,
    /** Set once saved: the screen opens the round and the form is done. */
    val savedRoundId: UUID? = null,
    /** Asked for by Save when a field is wrong: the screen moves focus there. */
    val focus: RoundField? = null,
) {
    /** Everything needed to show the form has loaded. */
    val ready: Boolean get() = me is Load.Loaded && (editing == null || editing.valueOrNull != null)

    /** Editing a round that no longer exists. */
    val gone: Boolean get() = editing is Load.Loaded && editing.value == null

    val failed: Boolean get() = me is Load.Failed || editing is Load.Failed

    val stats: List<StatField>
        get() {
            val mode = me.valueOrNull?.trackingMode
            return editing?.valueOrNull?.let { statFieldsForEdit(mode, it) } ?: statFieldsFor(mode)
        }

    val errors: Map<RoundField, RoundError> get() = validateRound(values, stats)

    /** What to show under [field]: the API's message, or a rule once touched or submitted. */
    fun messageFor(field: RoundField): FieldMessage? =
        apiErrors[field]?.let { FieldMessage.FromApi(it) }
            ?: errors[field]?.takeIf { submitted || field in touched }?.let { FieldMessage.Rule(it) }
}

/**
 * Log round (#94) and Edit round (#97), one form (docs/clients/ux-design.md → "Rounds").
 * Only strokes are required; the stats shown follow the tracking mode, and when editing
 * also every stat the round has, so saving can't quietly clear one. Validated as the user
 * types with the API's rules; the API's 400s still show next to their fields. Saving
 * scores the round, or rescores it and what it affects.
 *
 * Editing when the navigation arguments carry a "roundId".
 */
@HiltViewModel
class RoundFormViewModel
    @Inject
    constructor(
        private val user: UserRepository,
        private val rounds: RoundsRepository,
        savedState: SavedStateHandle,
    ) : ViewModel() {
        private val roundId: UUID? = savedState.get<String>(ROUND_ID)?.let(UUID::fromString)
        private val mutableState = MutableStateFlow(RoundFormState(editing = roundId?.let { Load.Loading }))
        val state: StateFlow<RoundFormState> = mutableState.asStateFlow()

        init {
            loadAll()
        }

        fun retry() {
            mutableState.update { it.copy(me = Load.Loading, editing = roundId?.let { Load.Loading }) }
            loadAll()
        }

        private fun loadAll() {
            viewModelScope.launch {
                val me = async { load { user.me() } }
                val round = roundId?.let { id -> async { load { rounds.round(id) } } }
                val editing = round?.await()
                mutableState.update { state ->
                    state.copy(
                        me = me.await(),
                        editing = editing,
                        values = editing?.valueOrNull?.let(::formValuesFrom) ?: state.values,
                    )
                }
            }
        }

        fun setDate(date: LocalDate) = edit(RoundField.PlayedAt) { copy(playedAt = date) }

        fun setHoles(holes: Int) = edit(RoundField.Holes) { copy(holes = holes) }

        fun setCourse(text: String) = edit(RoundField.CourseName) { copy(courseName = text) }

        fun setTee(text: String) = edit(RoundField.TeeName) { copy(teeName = text) }

        fun setNotes(text: String) = edit(RoundField.Notes) { copy(notes = text) }

        fun setStrokes(text: String) = edit(RoundField.TotalStrokes) { copy(totalStrokes = text) }

        fun stepStrokes(delta: Int) {
            val typical =
                state.value.me.valueOrNull
                    ?.typical18Score
            edit(RoundField.TotalStrokes) { copy(totalStrokes = stepStrokes(this, delta, typical)) }
        }

        fun setStat(
            field: StatField,
            text: String,
        ) = edit(RoundField.of(field)) { copy(stats = stats + (field to text)) }

        fun stepStat(
            field: StatField,
            delta: Int,
        ) = edit(RoundField.of(field)) { copy(stats = stats + (field to stepStat(field, this, delta))) }

        /** The screen moved focus where it was asked to. */
        fun focusHandled() = mutableState.update { it.copy(focus = null) }

        fun save() {
            val current = state.value
            if (current.saving || current.savedRoundId != null || !current.ready) return

            val firstWrong = RoundField.entries.firstOrNull { it in current.errors }
            mutableState.update { it.copy(submitted = true, problem = null, focus = firstWrong) }
            if (firstWrong != null) return

            mutableState.update { it.copy(saving = true) }
            viewModelScope.launch {
                try {
                    val request = toRequest(current.values, current.stats)
                    val saved = roundId?.let { rounds.update(it, request) } ?: rounds.create(request)
                    mutableState.update { it.copy(saving = false, savedRoundId = saved.roundId) }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: ApiProblem) {
                    val fields =
                        if (e.status == 400) {
                            RoundField.entries
                                .mapNotNull { field -> e.messagesFor(field.apiName).firstOrNull()?.let { field to it } }
                                .toMap()
                        } else {
                            emptyMap()
                        }
                    mutableState.update {
                        it.copy(
                            saving = false,
                            apiErrors = fields,
                            problem = if (fields.isEmpty()) FormProblem.SaveFailed else null,
                            focus = RoundField.entries.firstOrNull { field -> field in fields },
                            // Deleted on another device while being edited.
                            editing = if (e.status == 404 && roundId != null) Load.Loaded(null) else it.editing,
                        )
                    }
                } catch (_: Exception) {
                    mutableState.update { it.copy(saving = false, problem = FormProblem.CantConnect) }
                }
            }
        }

        private fun edit(
            field: RoundField,
            change: RoundFormValues.() -> RoundFormValues,
        ) = mutableState.update {
            it.copy(
                values = it.values.change(),
                touched = it.touched + field,
                apiErrors = it.apiErrors - field,
            )
        }

        companion object {
            /** The navigation argument naming the round to edit. */
            const val ROUND_ID = "roundId"
        }
    }

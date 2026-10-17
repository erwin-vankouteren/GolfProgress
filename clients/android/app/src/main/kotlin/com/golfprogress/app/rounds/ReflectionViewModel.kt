package com.golfprogress.app.rounds

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.CreateReflectionRequest
import com.golfprogress.app.data.RoundsRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.util.UUID
import javax.inject.Inject

// The reflection form's rules (#101), the same as the web app's reflectionForm.ts and the
// API's CreateReflectionRequest: every field is optional, but at least one is needed, and
// text is at most 1,000 characters, counted as people count them.

/** CreateReflectionRequest.TextMaxLength. */
const val REFLECTION_TEXT_MAX = 1000

enum class ReflectionText(
    val apiName: String,
) {
    WhatWorked("whatWorked"),
    WhatToRepeat("whatToRepeat"),
    OneChangeNext("oneChangeNext"),
}

data class ReflectionValues(
    val focusRating: Int? = null,
    val moodRating: Int? = null,
    /** Null until answered: "no" is an answer too. */
    val frustrationSpillover: Boolean? = null,
    val text: Map<ReflectionText, String> = emptyMap(),
) {
    fun text(field: ReflectionText): String = text[field].orEmpty()

    /** Whether anything is filled in; spaces alone don't count. */
    val hasContent: Boolean
        get() =
            focusRating != null ||
                moodRating != null ||
                frustrationSpillover != null ||
                ReflectionText.entries.any { text(it).isNotBlank() }

    /** The text fields over the limit. */
    val tooLong: List<ReflectionText>
        get() = ReflectionText.entries.filter { text(it).codePointCount(0, text(it).length) > REFLECTION_TEXT_MAX }

    /** The POST body: trimmed text, and nothing sent for what was left empty. */
    fun toRequest() =
        CreateReflectionRequest(
            focusRating = focusRating,
            moodRating = moodRating,
            frustrationSpillover = frustrationSpillover,
            whatWorked = text(ReflectionText.WhatWorked).trim().ifEmpty { null },
            whatToRepeat = text(ReflectionText.WhatToRepeat).trim().ifEmpty { null },
            oneChangeNext = text(ReflectionText.OneChangeNext).trim().ifEmpty { null },
        )
}

data class ReflectionState(
    val values: ReflectionValues = ReflectionValues(),
    val submitted: Boolean = false,
    val apiErrors: Map<ReflectionText, String> = emptyMap(),
    val problem: FormProblem? = null,
    val saving: Boolean = false,
    val saved: Boolean = false,
) {
    /** "Fill in at least one thing", once Save was pressed with nothing. */
    val needsSomething: Boolean get() = submitted && !values.hasContent

    fun tooLong(field: ReflectionText): Boolean = field in values.tooLong
}

/** Add a reflection to a round (#101), from its detail. */
@HiltViewModel
class ReflectionViewModel
    @Inject
    constructor(
        private val rounds: RoundsRepository,
        savedState: SavedStateHandle,
    ) : ViewModel() {
        private val roundId: UUID = UUID.fromString(checkNotNull(savedState.get<String>(RoundFormViewModel.ROUND_ID)))
        private val mutableState = MutableStateFlow(ReflectionState())
        val state: StateFlow<ReflectionState> = mutableState.asStateFlow()

        fun setFocus(rating: Int?) = edit { copy(focusRating = rating) }

        fun setMood(rating: Int?) = edit { copy(moodRating = rating) }

        fun setFrustration(carried: Boolean) = edit { copy(frustrationSpillover = carried) }

        fun setText(
            field: ReflectionText,
            value: String,
        ) {
            edit { copy(text = text + (field to value)) }
            mutableState.update { it.copy(apiErrors = it.apiErrors - field) }
        }

        fun save() {
            val current = state.value
            if (current.saving || current.saved) return
            mutableState.update { it.copy(submitted = true, problem = null) }
            if (!current.values.hasContent || current.values.tooLong.isNotEmpty()) return

            mutableState.update { it.copy(saving = true) }
            viewModelScope.launch {
                try {
                    rounds.addReflection(roundId, current.values.toRequest())
                    mutableState.update { it.copy(saving = false, saved = true) }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: ApiProblem) {
                    val fields =
                        if (e.status == 400) {
                            ReflectionText.entries
                                .mapNotNull { field -> e.messagesFor(field.apiName).firstOrNull()?.let { field to it } }
                                .toMap()
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

        private fun edit(change: ReflectionValues.() -> ReflectionValues) = mutableState.update { it.copy(values = it.values.change()) }
    }

package com.golfprogress.app.practice

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
import com.golfprogress.api.generated.models.PracticeSessionResponse
import com.golfprogress.app.data.PracticeRepository
import com.golfprogress.app.rounds.FormProblem
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.load
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.LocalDate
import java.util.UUID
import javax.inject.Inject

// ---------------------------------------------------------------- list

data class PracticeListState(
    val first: Load<Unit> = Load.Loading,
    val sessions: List<PracticeSessionResponse> = emptyList(),
    val total: Int? = null,
    val loadingMore: Boolean = false,
    val moreFailed: Boolean = false,
    val refreshing: Boolean = false,
) {
    val hasMore: Boolean get() = total?.let { sessions.size < it } ?: false
}

/** The practice list (#105): newest first, a page at a time until X-Total-Count. */
@HiltViewModel
class PracticeListViewModel
    @Inject
    constructor(
        private val repository: PracticeRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(PracticeListState())
        val state: StateFlow<PracticeListState> = mutableState.asStateFlow()
        private var loading: Job? = null

        init {
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(first = Load.Loading) }
            reload()
        }

        fun refresh() {
            if (state.value.first is Load.Loaded) mutableState.update { it.copy(refreshing = true) }
            reload()
        }

        private fun reload() {
            loading?.cancel()
            loading =
                viewModelScope.launch {
                    val page = load { repository.page(skip = 0, take = PAGE_SIZE) }
                    mutableState.update {
                        when (page) {
                            is Load.Loaded ->
                                it.copy(
                                    first = Load.Loaded(Unit),
                                    sessions = page.value.items,
                                    total = page.value.total,
                                    moreFailed = false,
                                )
                            is Load.Failed -> if (it.first is Load.Loaded) it else it.copy(first = page)
                            Load.Loading -> it
                        }.copy(refreshing = false, loadingMore = false)
                    }
                }
        }

        fun loadMore() {
            val current = state.value
            if (!current.hasMore || current.loadingMore || current.refreshing || current.first !is Load.Loaded) return
            mutableState.update { it.copy(loadingMore = true, moreFailed = false) }
            loading =
                viewModelScope.launch {
                    when (val page = load { repository.page(skip = current.sessions.size, take = PAGE_SIZE) }) {
                        is Load.Loaded ->
                            mutableState.update {
                                val total = if (page.value.items.isEmpty()) it.sessions.size else page.value.total ?: it.total
                                it.copy(sessions = it.sessions + page.value.items, total = total, loadingMore = false)
                            }
                        else -> mutableState.update { it.copy(loadingMore = false, moreFailed = true) }
                    }
                }
        }

        companion object {
            const val PAGE_SIZE = 20
        }
    }

// ---------------------------------------------------------------- log

/** Why a tag couldn't be added, under the tag input. */
enum class TagProblem { Duplicate, TooLong, TooMany }

data class LogPracticeState(
    val values: PracticeFormValues = PracticeFormValues(practicedAt = LocalDate.now()),
    val tagInput: String = "",
    val tagProblem: TagProblem? = null,
    val touched: Set<PracticeField> = emptySet(),
    val submitted: Boolean = false,
    val apiErrors: Map<PracticeField, String> = emptyMap(),
    val problem: FormProblem? = null,
    val saving: Boolean = false,
    val savedId: UUID? = null,
    val focus: PracticeField? = null,
) {
    val errors: Map<PracticeField, PracticeError> get() = validatePractice(values)

    /** The API's message, or a rule once the field was touched or Save pressed. */
    fun messageFor(field: PracticeField): PracticeMessage? =
        apiErrors[field]?.let { PracticeMessage.FromApi(it) }
            ?: errors[field]?.takeIf { submitted || field in touched }?.let { PracticeMessage.Rule(it) }
}

/** A message under a field: one of the form's rules, or the API's words. */
sealed interface PracticeMessage {
    data class Rule(
        val error: PracticeError,
    ) : PracticeMessage

    data class FromApi(
        val text: String,
    ) : PracticeMessage
}

/** Log practice (#105): date, type, duration, venue, goal, tags and notes. */
@HiltViewModel
class LogPracticeViewModel
    @Inject
    constructor(
        private val repository: PracticeRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(LogPracticeState())
        val state: StateFlow<LogPracticeState> = mutableState.asStateFlow()

        fun setDate(date: LocalDate) = edit(PracticeField.PracticedAt) { copy(practicedAt = date) }

        fun setType(type: CreatePracticeSessionRequest.SessionType) = edit(PracticeField.SessionType) { copy(sessionType = type) }

        fun setDuration(text: String) = edit(PracticeField.DurationMinutes) { copy(durationMinutes = text) }

        fun setVenue(text: String) = edit(PracticeField.VenueName) { copy(venueName = text) }

        fun setGoal(text: String) = edit(PracticeField.PrimaryGoal) { copy(primaryGoal = text) }

        fun setNotes(text: String) = edit(PracticeField.Notes) { copy(notes = text) }

        /** Typing a comma adds the tag before it, as Enter does. */
        fun setTagInput(text: String) {
            if (text.endsWith(",")) {
                mutableState.update { it.copy(tagInput = text.dropLast(1)) }
                commitTag()
            } else {
                mutableState.update { it.copy(tagInput = text, tagProblem = null) }
            }
        }

        /** Turns the tag input into a chip. False when it couldn't. */
        fun commitTag(): Boolean {
            val current = state.value
            return when (val result = addTag(current.values.tags, current.tagInput)) {
                is AddTag.Added -> {
                    edit(PracticeField.Tags) { copy(tags = result.tags) }
                    mutableState.update { it.copy(tagInput = "", tagProblem = null) }
                    true
                }
                AddTag.Refused.Empty -> true
                AddTag.Refused.Duplicate -> refuse(TagProblem.Duplicate)
                AddTag.Refused.TooLong -> refuse(TagProblem.TooLong)
                AddTag.Refused.TooMany -> refuse(TagProblem.TooMany)
            }
        }

        private fun refuse(problem: TagProblem): Boolean {
            mutableState.update { it.copy(tagProblem = problem) }
            return false
        }

        fun removeTag(tag: String) = edit(PracticeField.Tags) { copy(tags = tags - tag) }

        fun focusHandled() = mutableState.update { it.copy(focus = null) }

        fun save() {
            if (state.value.saving || state.value.savedId != null) return
            // A tag still being typed counts, as if Enter had been pressed.
            if (!commitTag()) {
                mutableState.update { it.copy(submitted = true, focus = PracticeField.Tags) }
                return
            }
            val current = state.value
            val firstWrong = PracticeField.entries.firstOrNull { it in current.errors }
            mutableState.update { it.copy(submitted = true, problem = null, focus = firstWrong) }
            if (firstWrong != null) return

            mutableState.update { it.copy(saving = true) }
            viewModelScope.launch {
                try {
                    val saved = repository.create(current.values.toRequest())
                    mutableState.update { it.copy(saving = false, savedId = saved.practiceSessionId) }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: ApiProblem) {
                    val fields =
                        if (e.status == 400) {
                            PracticeField.entries
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
                            focus = PracticeField.entries.firstOrNull { f -> f in fields },
                        )
                    }
                } catch (_: Exception) {
                    mutableState.update { it.copy(saving = false, problem = FormProblem.CantConnect) }
                }
            }
        }

        private fun edit(
            field: PracticeField,
            change: PracticeFormValues.() -> PracticeFormValues,
        ) = mutableState.update {
            it.copy(values = it.values.change(), touched = it.touched + field, apiErrors = it.apiErrors - field)
        }
    }

// ---------------------------------------------------------------- detail

data class PracticeDetailState(
    /** Loaded with null: the session no longer exists. */
    val session: Load<PracticeSessionResponse?> = Load.Loading,
    val confirmDelete: Boolean = false,
    val deleting: Boolean = false,
    val deleteFailed: Boolean = false,
    val deleted: Boolean = false,
)

/** A practice session's detail (#105): every field, and Delete. */
@HiltViewModel
class PracticeDetailViewModel
    @Inject
    constructor(
        private val repository: PracticeRepository,
        savedState: SavedStateHandle,
    ) : ViewModel() {
        private val id: UUID = UUID.fromString(checkNotNull(savedState.get<String>(SESSION_ID)))
        private val mutableState = MutableStateFlow(PracticeDetailState())
        val state: StateFlow<PracticeDetailState> = mutableState.asStateFlow()

        init {
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(session = Load.Loading) }
            reload()
        }

        private fun reload() {
            viewModelScope.launch {
                val session = load { repository.session(id) }
                mutableState.update { it.copy(session = session) }
            }
        }

        fun askToDelete() = mutableState.update { it.copy(confirmDelete = true, deleteFailed = false) }

        fun cancelDelete() = mutableState.update { it.copy(confirmDelete = false, deleteFailed = false) }

        fun confirmDelete() {
            if (state.value.deleting) return
            mutableState.update { it.copy(deleting = true, deleteFailed = false) }
            viewModelScope.launch {
                val result = load { repository.delete(id) }
                val goneAlready = (result as? Load.Failed)?.error.let { it is ApiProblem && it.status == 404 }
                mutableState.update {
                    if (result is Load.Loaded || goneAlready) {
                        it.copy(deleting = false, confirmDelete = false, deleted = true)
                    } else {
                        it.copy(deleting = false, deleteFailed = true)
                    }
                }
            }
        }

        companion object {
            /** The navigation argument naming the session. */
            const val SESSION_ID = "sessionId"
        }
    }

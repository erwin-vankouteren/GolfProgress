package com.golfprogress.app.feedback

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.FeedbackMessageResponse
import com.golfprogress.app.data.CoachingRepository
import com.golfprogress.app.data.FeedbackRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.OffsetDateTime
import java.util.UUID
import javax.inject.Inject

/** Why a message couldn't be sent, saved or deleted. */
enum class FeedbackProblem { Empty, TooLong, TooMany, Failed, CantConnect }

/** What the section shows. */
sealed interface FeedbackView {
    data object Loading : FeedbackView

    /** No coach can see the item, and none wrote about it: nothing to show. */
    data object Hidden : FeedbackView

    data object Failed : FeedbackView

    data class Threads(
        val threads: List<GolferThread>,
    ) : FeedbackView
}

data class FeedbackState(
    val view: FeedbackView = FeedbackView.Loading,
    /** What's being written to each coach. */
    val drafts: Map<UUID, String> = emptyMap(),
    val sendingTo: UUID? = null,
    /** The problem with the message to a coach, shown under their field. */
    val sendProblem: Pair<UUID, FeedbackProblem>? = null,
    /** The message being edited, and its text so far. */
    val editing: Pair<UUID, String>? = null,
    val saving: Boolean = false,
    val saveProblem: FeedbackProblem? = null,
    val confirmDelete: UUID? = null,
    val deleting: Boolean = false,
    val deleteProblem: FeedbackProblem? = null,
)

/**
 * "Feedback from your coach" under a round, reflection or practice session (#217,
 * docs/coach-access.md → "Feedback"): a thread per coach, Reply, and editing and deleting
 * your own messages. Seeing it marks it read. [start] picks the item.
 */
@HiltViewModel
class FeedbackViewModel
    @Inject
    constructor(
        private val feedback: FeedbackRepository,
        private val coaching: CoachingRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(FeedbackState())
        val state: StateFlow<FeedbackState> = mutableState.asStateFlow()

        private var item: Triple<UUID, FeedbackTarget, UUID>? = null
        private var markedUpTo: OffsetDateTime? = null

        /** Shows [targetId]'s threads for [golferId]; again for the same item does nothing. */
        fun start(
            golferId: UUID,
            target: FeedbackTarget,
            targetId: UUID,
        ) {
            val next = Triple(golferId, target, targetId)
            if (item == next) return
            item = next
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(view = FeedbackView.Loading) }
            reload()
        }

        private fun reload() {
            val (golferId, target, targetId) = item ?: return
            viewModelScope.launch {
                // Each on its own: a golfer who never had a coach sees nothing, not an error,
                // when either fails; an error shows only where there'd be a thread.
                val (messages, coaches) =
                    coroutineScope {
                        val messages = async { attempt { feedback.thread(golferId, target, targetId) } }
                        val coaches = async { attempt { coaching.coaches().coaches } }
                        messages.await() to coaches.await()
                    }
                val threads = golferThreads(messages.orEmpty(), coaches.orEmpty(), target)
                val view =
                    when {
                        threads.isEmpty() -> FeedbackView.Hidden
                        messages == null || coaches == null -> FeedbackView.Failed
                        else -> FeedbackView.Threads(threads)
                    }
                mutableState.update { it.copy(view = view) }
                if (messages != null) markRead(golferId, messages)
            }
        }

        private suspend fun markRead(
            golferId: UUID,
            messages: List<FeedbackMessageResponse>,
        ) {
            val newest = newestMessageAt(messages) ?: return
            if (markedUpTo?.let { newest <= it } == true) return
            markedUpTo = newest
            // Not being marked read isn't worth an error on screen: it's tried again next time.
            attempt { feedback.markRead(golferId, newest) }
        }

        fun setDraft(
            coachId: UUID,
            text: String,
        ) = mutableState.update {
            it.copy(drafts = it.drafts + (coachId to text), sendProblem = it.sendProblem?.takeIf { p -> p.first != coachId })
        }

        fun send(coachId: UUID) {
            val (golferId, target, targetId) = item ?: return
            val current = state.value
            if (current.sendingTo != null) return
            val text = current.drafts[coachId].orEmpty()
            checkFeedbackText(text)?.let { error ->
                mutableState.update { it.copy(sendProblem = coachId to error.asProblem()) }
                return
            }
            mutableState.update { it.copy(sendingTo = coachId, sendProblem = null) }
            viewModelScope.launch {
                try {
                    feedback.write(golferId, target, targetId, coachId, text.trim())
                    mutableState.update { it.copy(sendingTo = null, drafts = it.drafts - coachId) }
                    reload()
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    mutableState.update { it.copy(sendingTo = null, sendProblem = coachId to problemOf(e)) }
                }
            }
        }

        fun startEdit(message: FeedbackMessageResponse) =
            mutableState.update { it.copy(editing = message.feedbackMessageId to message.text, saveProblem = null) }

        fun setEditText(text: String) =
            mutableState.update { s -> s.copy(editing = s.editing?.let { it.first to text }, saveProblem = null) }

        fun cancelEdit() = mutableState.update { if (it.saving) it else it.copy(editing = null, saveProblem = null) }

        fun saveEdit() {
            val (id, text) = state.value.editing ?: return
            if (state.value.saving) return
            checkFeedbackText(text)?.let { error ->
                mutableState.update { it.copy(saveProblem = error.asProblem()) }
                return
            }
            mutableState.update { it.copy(saving = true, saveProblem = null) }
            viewModelScope.launch {
                try {
                    feedback.edit(id, text.trim())
                    mutableState.update { it.copy(saving = false, editing = null) }
                    reload()
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    mutableState.update { it.copy(saving = false, saveProblem = problemOf(e)) }
                }
            }
        }

        fun askDelete(id: UUID) = mutableState.update { it.copy(confirmDelete = id, deleteProblem = null) }

        fun cancelDelete() = mutableState.update { if (it.deleting) it else it.copy(confirmDelete = null) }

        fun confirmDelete() {
            val id = state.value.confirmDelete ?: return
            if (state.value.deleting) return
            mutableState.update { it.copy(deleting = true, deleteProblem = null) }
            viewModelScope.launch {
                try {
                    feedback.delete(id)
                    mutableState.update { it.copy(deleting = false, confirmDelete = null) }
                    reload()
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    // Gone already: what was asked for has happened.
                    val gone = e is ApiProblem && e.status == 404
                    mutableState.update {
                        it.copy(
                            deleting = false,
                            confirmDelete = if (gone) null else id,
                            deleteProblem = if (gone) null else problemOf(e),
                        )
                    }
                    if (gone) reload()
                }
            }
        }

        private fun FeedbackTextError.asProblem() =
            when (this) {
                FeedbackTextError.Empty -> FeedbackProblem.Empty
                FeedbackTextError.TooLong -> FeedbackProblem.TooLong
            }

        private fun problemOf(e: Exception): FeedbackProblem =
            when {
                e is ApiProblem && e.status == 429 -> FeedbackProblem.TooMany
                e is ApiProblem -> FeedbackProblem.Failed
                else -> FeedbackProblem.CantConnect
            }

        private suspend fun <T> attempt(block: suspend () -> T): T? =
            try {
                block()
            } catch (e: CancellationException) {
                throw e
            } catch (_: Exception) {
                null
            }
    }

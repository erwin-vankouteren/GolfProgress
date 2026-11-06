package com.golfprogress.app.coaching

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.CoachesResponse
import com.golfprogress.app.data.CoachingRepository
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.load
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.util.UUID
import javax.inject.Inject

// The rules of inviting a coach (#186), the same as the web app's coachForm.ts.

enum class CoachEmailError { Required, Invalid }

private val emailShape = Regex("""^[^\s@]+@[^\s@]+\.[^\s@]+$""")

fun checkCoachEmail(email: String): CoachEmailError? {
    val trimmed = email.trim()
    return when {
        trimmed.isEmpty() -> CoachEmailError.Required
        !emailShape.matches(trimmed) -> CoachEmailError.Invalid
        else -> null
    }
}

enum class SharedItem { Rounds, Reflections, Practice }

/** What a coach sees: rounds and progress always, the other two when the golfer shares them. */
fun sharedItems(
    shareReflections: Boolean,
    sharePractice: Boolean,
): List<SharedItem> =
    buildList {
        add(SharedItem.Rounds)
        if (shareReflections) add(SharedItem.Reflections)
        if (sharePractice) add(SharedItem.Practice)
    }

enum class InviteProblem { Failed, CantConnect, TooManyToday }

enum class CancelProblem { Failed, CantConnect }

/** The invitation just sent, for the confirmation. */
data class SentInvitation(
    val email: String,
    val resent: Boolean,
)

data class CoachesState(
    val coaches: Load<CoachesResponse> = Load.Loading,
    val email: String = "",
    val shareReflections: Boolean = true,
    val sharePractice: Boolean = true,
    val emailError: CoachEmailError? = null,
    /** The API's message, e.g. that this person is already your coach. */
    val emailApiError: String? = null,
    val sending: Boolean = false,
    val sent: SentInvitation? = null,
    val inviteProblem: InviteProblem? = null,
    val cancelling: Set<UUID> = emptySet(),
    val cancelProblem: CancelProblem? = null,
)

/**
 * Profile → Coaches (#186, docs/coach-access.md → "Screens"): who coaches you, who's been
 * invited, and Invite a coach. Changing what a coach sees and removing them is #191.
 */
@HiltViewModel
class CoachesViewModel
    @Inject
    constructor(
        private val coaching: CoachingRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(CoachesState())
        val state: StateFlow<CoachesState> = mutableState.asStateFlow()

        init {
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(coaches = Load.Loading) }
            reload()
        }

        private fun reload() {
            viewModelScope.launch {
                val coaches = load { coaching.coaches() }
                mutableState.update { it.copy(coaches = coaches) }
            }
        }

        fun setEmail(email: String) = mutableState.update { it.copy(email = email, emailError = null, emailApiError = null, sent = null) }

        fun setShareReflections(share: Boolean) = mutableState.update { it.copy(shareReflections = share, sent = null) }

        fun setSharePractice(share: Boolean) = mutableState.update { it.copy(sharePractice = share, sent = null) }

        fun send() {
            val current = state.value
            if (current.sending) return
            val problem = checkCoachEmail(current.email)
            mutableState.update { it.copy(emailError = problem, emailApiError = null, inviteProblem = null, sent = null) }
            if (problem != null) return

            mutableState.update { it.copy(sending = true) }
            viewModelScope.launch {
                try {
                    val invitation = coaching.invite(current.email.trim(), current.shareReflections, current.sharePractice)
                    mutableState.update {
                        it.copy(
                            email = "",
                            shareReflections = true,
                            sharePractice = true,
                            sending = false,
                            sent = SentInvitation(invitation.email, invitation.resent),
                        )
                    }
                    reload()
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    val onEmail = (e as? ApiProblem)?.takeIf { it.status == 400 }?.messagesFor("email")?.firstOrNull()
                    mutableState.update {
                        it.copy(
                            sending = false,
                            emailApiError = onEmail,
                            inviteProblem = if (onEmail == null) inviteProblemOf(e) else null,
                        )
                    }
                }
            }
        }

        fun cancel(id: UUID) {
            if (id in state.value.cancelling) return
            mutableState.update { it.copy(cancelling = it.cancelling + id, cancelProblem = null) }
            viewModelScope.launch {
                try {
                    coaching.cancelInvitation(id)
                    mutableState.update { it.copy(cancelling = it.cancelling - id) }
                    reload()
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    // Accepted, declined or expired meanwhile: the list shows what's true now.
                    val gone = e is ApiProblem && e.status == 404
                    mutableState.update {
                        it.copy(
                            cancelling = it.cancelling - id,
                            cancelProblem =
                                when {
                                    gone -> null
                                    e is ApiProblem -> CancelProblem.Failed
                                    else -> CancelProblem.CantConnect
                                },
                        )
                    }
                    if (gone) reload()
                }
            }
        }

        private fun inviteProblemOf(e: Exception): InviteProblem =
            when {
                e is ApiProblem && e.status == 429 -> InviteProblem.TooManyToday
                e is ApiProblem -> InviteProblem.Failed
                else -> InviteProblem.CantConnect
            }
    }

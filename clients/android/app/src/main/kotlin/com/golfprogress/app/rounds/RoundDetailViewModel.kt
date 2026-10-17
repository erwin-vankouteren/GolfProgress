package com.golfprogress.app.rounds

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.generated.models.MicroWinResponse
import com.golfprogress.api.generated.models.NextLeverResponse
import com.golfprogress.api.generated.models.ReflectionResponse
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.app.data.RoundsRepository
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.load
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

/** Everything the round detail shows, loaded together. */
data class RoundDetail(
    val round: RoundResponse,
    val score: FitnessScoreResponse?,
    val microWins: List<MicroWinResponse>,
    val nextLever: NextLeverResponse?,
    /** Newest first. */
    val reflections: List<ReflectionResponse>,
)

data class RoundDetailState(
    /** Loaded with null: the round no longer exists, e.g. deleted on another device. */
    val detail: Load<RoundDetail?> = Load.Loading,
    val confirmDelete: Boolean = false,
    val deleting: Boolean = false,
    val deleteFailed: Boolean = false,
    /** Set once deleted: the screen goes back to the list. */
    val deleted: Boolean = false,
)

/**
 * A round's detail (#97): the score with its pillars and why, micro-wins, the next
 * focus, the stats and its reflections, from GET /rounds/{id}, /fitness-score,
 * /micro-wins, /next-lever and /reflections. Reloads when it comes back into view, e.g.
 * after editing.
 */
@HiltViewModel
class RoundDetailViewModel
    @Inject
    constructor(
        private val repository: RoundsRepository,
        savedState: SavedStateHandle,
    ) : ViewModel() {
        val roundId: UUID = UUID.fromString(checkNotNull(savedState.get<String>(RoundFormViewModel.ROUND_ID)))
        private val mutableState = MutableStateFlow(RoundDetailState())
        val state: StateFlow<RoundDetailState> = mutableState.asStateFlow()

        init {
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(detail = Load.Loading) }
            reload()
        }

        /** Back in view: quietly, keeping what's shown if it fails. */
        fun refresh() {
            if (state.value.detail is Load.Loaded && !state.value.deleted) reload()
        }

        private fun reload() {
            viewModelScope.launch {
                val detail = load { loadDetail() }
                mutableState.update {
                    if (detail is Load.Failed && it.detail is Load.Loaded) it else it.copy(detail = detail)
                }
            }
        }

        private suspend fun loadDetail(): RoundDetail? =
            coroutineScope {
                val round = async { repository.round(roundId) }
                val score = async { repository.fitnessScore(roundId) }
                val wins = async { repository.microWins(roundId) }
                val lever = async { repository.nextLever(roundId) }
                val reflections = async { repository.reflections(roundId) }
                round.await()?.let {
                    RoundDetail(it, score.await(), wins.await(), lever.await(), reflections.await().sortedByDescending { r -> r.createdAt })
                }
            }

        fun askToDelete() = mutableState.update { it.copy(confirmDelete = true, deleteFailed = false) }

        fun cancelDelete() = mutableState.update { it.copy(confirmDelete = false, deleteFailed = false) }

        fun confirmDelete() {
            if (state.value.deleting) return
            mutableState.update { it.copy(deleting = true, deleteFailed = false) }
            viewModelScope.launch {
                val result = load { repository.delete(roundId) }
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
    }

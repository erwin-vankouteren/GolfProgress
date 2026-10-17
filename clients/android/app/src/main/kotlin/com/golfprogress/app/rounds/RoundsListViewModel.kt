package com.golfprogress.app.rounds

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.app.data.RoundsRepository
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.load
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.util.UUID
import javax.inject.Inject

data class RoundsListState(
    /** The first page: until it arrives the list shows a skeleton, or an error. */
    val first: Load<Unit> = Load.Loading,
    val rounds: List<RoundResponse> = emptyList(),
    /** From X-Total-Count; null if the API didn't say. */
    val total: Int? = null,
    val loadingMore: Boolean = false,
    val moreFailed: Boolean = false,
    val refreshing: Boolean = false,
    /** Asked to delete, waiting for the user to confirm. */
    val confirmDelete: RoundResponse? = null,
    val deleting: Boolean = false,
    val deleteFailed: Boolean = false,
) {
    val hasMore: Boolean get() = total?.let { rounds.size < it } ?: false
}

/**
 * The round list (#97): newest first, a page at a time with skip/take until
 * X-Total-Count. Reloads from the top when it comes back into view, so a round logged,
 * edited or deleted elsewhere shows.
 */
@HiltViewModel
class RoundsListViewModel
    @Inject
    constructor(
        private val repository: RoundsRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(RoundsListState())
        val state: StateFlow<RoundsListState> = mutableState.asStateFlow()
        private var loading: Job? = null

        init {
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(first = Load.Loading) }
            reload()
        }

        /** Pull to refresh, or back in view: the first page again, keeping the list until it arrives. */
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
                                it.copy(first = Load.Loaded(Unit), rounds = page.value.items, total = page.value.total, moreFailed = false)
                            // A failed refresh keeps the list on screen.
                            is Load.Failed -> if (it.first is Load.Loaded) it else it.copy(first = page)
                            Load.Loading -> it
                        }.copy(refreshing = false, loadingMore = false)
                    }
                }
        }

        /** Near the end of the list, or "Show more rounds" after a failure. */
        fun loadMore() {
            val current = state.value
            if (!current.hasMore || current.loadingMore || current.refreshing || current.first !is Load.Loaded) return
            mutableState.update { it.copy(loadingMore = true, moreFailed = false) }
            loading =
                viewModelScope.launch {
                    when (val page = load { repository.page(skip = current.rounds.size, take = PAGE_SIZE) }) {
                        is Load.Loaded ->
                            mutableState.update {
                                // An empty page means the total was stale: stop asking.
                                val total = if (page.value.items.isEmpty()) it.rounds.size else page.value.total ?: it.total
                                it.copy(rounds = it.rounds + page.value.items, total = total, loadingMore = false)
                            }
                        else -> mutableState.update { it.copy(loadingMore = false, moreFailed = true) }
                    }
                }
        }

        fun askToDelete(round: RoundResponse) = mutableState.update { it.copy(confirmDelete = round, deleteFailed = false) }

        fun cancelDelete() = mutableState.update { it.copy(confirmDelete = null, deleteFailed = false) }

        fun confirmDelete() {
            val round = state.value.confirmDelete ?: return
            if (state.value.deleting) return
            mutableState.update { it.copy(deleting = true, deleteFailed = false) }
            viewModelScope.launch {
                when (load { repository.delete(round.roundId) }) {
                    is Load.Loaded -> mutableState.update { it.withoutRound(round.roundId) }
                    else -> mutableState.update { it.copy(deleting = false, deleteFailed = true) }
                }
            }
        }

        private fun RoundsListState.withoutRound(roundId: UUID) =
            copy(
                rounds = rounds.filterNot { it.roundId == roundId },
                total = total?.minus(1),
                confirmDelete = null,
                deleting = false,
            )

        companion object {
            const val PAGE_SIZE = 20
        }
    }

package com.golfprogress.app.dashboard

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.generated.models.DashboardResponse
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.app.data.DashboardRepository
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.load
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class DashboardState(
    val dashboard: Load<DashboardResponse> = Load.Loading,
    /** The last round's pillars: the dashboard itself doesn't carry them. */
    val pillars: FitnessScoreResponse? = null,
    /** Pull to refresh in progress, with the previous dashboard still on screen. */
    val refreshing: Boolean = false,
)

/**
 * Home (#90, docs/clients/ux-design.md → "Home: dashboard"). Loads on creation, and again
 * whenever the screen comes back into view, so a round logged on another device shows.
 */
@HiltViewModel
class DashboardViewModel
    @Inject
    constructor(
        private val repository: DashboardRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(DashboardState())
        val state: StateFlow<DashboardState> = mutableState.asStateFlow()
        private var loading: Job? = null

        init {
            reload()
        }

        /** Try again after an error: back to the skeleton. */
        fun retry() {
            mutableState.update { it.copy(dashboard = Load.Loading) }
            reload()
        }

        /** Pull to refresh, or coming back to Home: keeps what's shown until the new one arrives. */
        fun refresh() {
            if (state.value.dashboard is Load.Loaded) mutableState.update { it.copy(refreshing = true) }
            reload()
        }

        private fun reload() {
            loading?.cancel()
            loading =
                viewModelScope.launch {
                    val result = load { repository.dashboard() }
                    // A failed refresh keeps the dashboard already on screen.
                    mutableState.update {
                        if (result is Load.Failed && it.dashboard is Load.Loaded) {
                            it.copy(refreshing = false)
                        } else {
                            it.copy(dashboard = result, refreshing = false)
                        }
                    }
                    val roundId = (result as? Load.Loaded)?.value?.lastRound?.roundId ?: return@launch
                    val pillars = load { repository.fitnessScore(roundId) }
                    (pillars as? Load.Loaded)?.value?.let { score -> mutableState.update { it.copy(pillars = score) } }
                }
        }
    }

package com.golfprogress.app.progress

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.MonthlyReviewResponse
import com.golfprogress.api.generated.models.MonthlyTimelinePointResponse
import com.golfprogress.api.generated.models.RoundTimelinePointResponse
import com.golfprogress.api.generated.models.UserBaselineResponse
import com.golfprogress.app.data.ProgressRepository
import com.golfprogress.app.rounds.FormProblem
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.load
import com.golfprogress.app.ui.common.valueOrNull
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.LocalDate
import javax.inject.Inject

data class ProgressState(
    /** Before anything else: with no rounds there are no trends to show. */
    val roundCount: Load<Int> = Load.Loading,
    val range: RangeKey = RangeKey.TwelveMonths,
    val custom: DateRange = DateRange(),
    val timeline: Load<List<RoundTimelinePointResponse>> = Load.Loading,
    val monthly: Load<List<MonthlyTimelinePointResponse>> = Load.Loading,
    val baseline: Load<UserBaselineResponse?> = Load.Loading,
    val reviewMonth: String? = null,
    /** Null until a month is chosen; loaded null when that month has no review yet. */
    val review: Load<MonthlyReviewResponse?>? = null,
    val focus: String = "",
    val focusTooLong: Boolean = false,
    val focusApiError: String? = null,
    val creating: Boolean = false,
    val reviewProblem: FormProblem? = null,
    /** Set when a review was just created, to announce "Review ready." */
    val reviewReady: Boolean = false,
) {
    val customValid: Boolean get() = range != RangeKey.Custom || custom.isValid()

    /** The chosen month's point in the monthly trend. */
    val reviewPoint: MonthlyTimelinePointResponse? get() = monthly.valueOrNull?.firstOrNull { it.monthStart == reviewMonth }

    /** A month without rounds has nothing to review (an existing review still shows). */
    val nothingToReview: Boolean get() = review?.valueOrNull == null && reviewPoint?.roundsLogged == 0
}

/**
 * Progress (#109, docs/clients/ux-design.md → "Progress"): rounds over time for a chosen
 * period, the monthly trend, the baseline, and monthly reviews created on request.
 * Reloads when it comes back into view, so a round logged elsewhere shows.
 */
@HiltViewModel
class ProgressViewModel
    @Inject
    constructor(
        private val repository: ProgressRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(ProgressState())
        val state: StateFlow<ProgressState> = mutableState.asStateFlow()
        private var timelineJob: Job? = null
        private var reviewJob: Job? = null

        init {
            loadAll()
        }

        fun retry() {
            mutableState.update { ProgressState(range = it.range, custom = it.custom) }
            loadAll()
        }

        /** Back in view: everything again, quietly. */
        fun refresh() {
            if (state.value.roundCount is Load.Loaded) loadAll(quietly = true)
        }

        private fun loadAll(quietly: Boolean = false) {
            viewModelScope.launch {
                val count = load { repository.roundCount() }
                mutableState.update { if (quietly && count is Load.Failed) it else it.copy(roundCount = count) }
                if ((count as? Load.Loaded)?.value?.let { it > 0 } != true) return@launch

                loadTimeline()
                launch {
                    val baseline = load { repository.baseline() }
                    mutableState.update { if (quietly && baseline is Load.Failed) it else it.copy(baseline = baseline) }
                }
                val monthly = load { repository.monthly(TREND_MONTHS) }
                mutableState.update { if (quietly && monthly is Load.Failed) it else it.copy(monthly = monthly) }
                val month = state.value.reviewMonth ?: (monthly as? Load.Loaded)?.value?.let(::defaultReviewMonth)
                if (month != null) loadReview(month)
            }
        }

        fun retryTimeline() = loadTimeline()

        fun retryMonthly() = loadAll()

        fun retryBaseline() {
            viewModelScope.launch {
                mutableState.update { it.copy(baseline = Load.Loading) }
                val baseline = load { repository.baseline() }
                mutableState.update { it.copy(baseline = baseline) }
            }
        }

        fun setRange(range: RangeKey) {
            mutableState.update { it.copy(range = range) }
            if (state.value.customValid) loadTimeline()
        }

        fun setCustom(range: DateRange) {
            mutableState.update { it.copy(custom = range) }
            if (state.value.customValid) loadTimeline()
        }

        private fun loadTimeline() {
            val current = state.value
            val query = rangeQuery(current.range, LocalDate.now(), current.custom)
            timelineJob?.cancel()
            timelineJob =
                viewModelScope.launch {
                    // The previous period stays on screen while the next one loads.
                    if (current.timeline !is Load.Loaded) mutableState.update { it.copy(timeline = Load.Loading) }
                    val timeline = load { repository.timeline(query.from?.toString(), query.to?.toString(), TIMELINE_TAKE) }
                    mutableState.update { it.copy(timeline = timeline) }
                }
        }

        fun chooseMonth(monthStart: String) = loadReview(monthStart)

        fun retryReview() = state.value.reviewMonth?.let(::loadReview)

        private fun loadReview(monthStart: String) {
            reviewJob?.cancel()
            mutableState.update {
                it.copy(
                    reviewMonth = monthStart,
                    review = if (it.reviewMonth == monthStart && it.review is Load.Loaded) it.review else Load.Loading,
                    reviewReady = false,
                    reviewProblem = null,
                    focusApiError = null,
                    focusTooLong = false,
                )
            }
            reviewJob =
                viewModelScope.launch {
                    val review = load { repository.review(monthStart) }
                    mutableState.update {
                        it.copy(review = review, focus = (review as? Load.Loaded)?.value?.focusNextMonth.orEmpty())
                    }
                }
        }

        fun setFocus(text: String) = mutableState.update { it.copy(focus = text, focusTooLong = false, focusApiError = null) }

        fun createReview() {
            val current = state.value
            val month = current.reviewMonth ?: return
            if (current.creating) return
            val focus =
                when (val checked = checkFocus(current.focus)) {
                    CheckedFocus.TooLong -> {
                        mutableState.update { it.copy(focusTooLong = true) }
                        return
                    }
                    is CheckedFocus.Ok -> checked.value
                }
            mutableState.update { it.copy(creating = true, reviewProblem = null, reviewReady = false) }
            viewModelScope.launch {
                try {
                    val review = repository.createReview(month, focus)
                    mutableState.update {
                        it.copy(creating = false, review = Load.Loaded(review), focus = review.focusNextMonth.orEmpty(), reviewReady = true)
                    }
                    // The month picker marks reviewed months.
                    val monthly = load { repository.monthly(TREND_MONTHS) }
                    if (monthly is Load.Loaded) mutableState.update { it.copy(monthly = monthly) }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: ApiProblem) {
                    val field = if (e.status == 400) e.messagesFor("focusNextMonth").firstOrNull() else null
                    mutableState.update {
                        it.copy(
                            creating = false,
                            focusApiError = field,
                            reviewProblem =
                                if (field ==
                                    null
                                ) {
                                    FormProblem.SaveFailed
                                } else {
                                    null
                                },
                        )
                    }
                } catch (_: Exception) {
                    mutableState.update { it.copy(creating = false, reviewProblem = FormProblem.CantConnect) }
                }
            }
        }
    }

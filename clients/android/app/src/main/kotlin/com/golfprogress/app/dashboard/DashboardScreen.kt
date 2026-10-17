package com.golfprogress.app.dashboard

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.api.generated.models.DashboardResponse
import com.golfprogress.api.generated.models.DashboardRoundSummary
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.app.R
import com.golfprogress.app.ui.common.Confidence
import com.golfprogress.app.ui.common.ConfidenceBadge
import com.golfprogress.app.ui.common.EmptyState
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.MonthChange
import com.golfprogress.app.ui.common.PillarBars
import com.golfprogress.app.ui.common.SectionCard
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.currentLocale
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.theme.DesignTokens
import java.text.NumberFormat
import java.util.UUID

/** Home (#90): the last round, the next focus, the baseline, micro-wins and this month. */
@Composable
fun DashboardScreen(
    onLogRound: () -> Unit,
    onOpenRound: (UUID) -> Unit,
    viewModel: DashboardViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    // Back on Home, e.g. from another tab or app: a round logged meanwhile shows.
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.refresh() }
    DashboardContent(state, viewModel::retry, viewModel::refresh, onLogRound, onOpenRound)
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardContent(
    state: DashboardState,
    onRetry: () -> Unit,
    onRefresh: () -> Unit,
    onLogRound: () -> Unit,
    onOpenRound: (UUID) -> Unit,
) {
    PullToRefreshBox(
        isRefreshing = state.refreshing,
        onRefresh = onRefresh,
        modifier = Modifier.fillMaxSize(),
    ) {
        LazyColumn(
            contentPadding = PaddingValues(DesignTokens.Spacing.lg),
            verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
            modifier = Modifier.fillMaxSize(),
        ) {
            when (val dashboard = state.dashboard) {
                Load.Loading -> item { Skeleton(blocks = 4) }
                is Load.Failed -> item { LoadError(R.string.dashboard_load_error, onRetry) }
                is Load.Loaded -> {
                    val lastRound = dashboard.value.lastRound
                    if (lastRound == null) {
                        item {
                            EmptyState(R.string.home_empty, action = R.string.log_first_round, onAction = onLogRound)
                        }
                    } else {
                        cards(dashboard.value, lastRound, state.pillars, onOpenRound)
                    }
                }
            }
        }
    }
}

private fun androidx.compose.foundation.lazy.LazyListScope.cards(
    dashboard: DashboardResponse,
    lastRound: DashboardRoundSummary,
    pillars: FitnessScoreResponse?,
    onOpenRound: (UUID) -> Unit,
) {
    item { LastRoundCard(lastRound, pillars, onOpenRound) }

    dashboard.nextLever?.let { lever ->
        item {
            SectionCard(R.string.next_focus) {
                Text(lever.title, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.SemiBold)
                Text(lever.recommendation, style = MaterialTheme.typography.bodyLarge)
                Text(lever.rationale, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }

    dashboard.baselineAvgScore?.let { average ->
        item {
            SectionCard(R.string.baseline) {
                Stat(R.string.baseline_average, oneDecimal(average))
                Confidence.of(dashboard.baselineConfidence?.value)?.let { ConfidenceBadge(it, explain = true) }
            }
        }
    }

    if (dashboard.microWins.isNotEmpty()) {
        item {
            SectionCard(R.string.micro_wins) {
                dashboard.microWins.take(3).forEach { win ->
                    Text("✓ " + win.title, style = MaterialTheme.typography.bodyLarge)
                }
            }
        }
    }

    dashboard.monthlyFitnessAvg?.let { average ->
        item {
            SectionCard(R.string.this_month) {
                Stat(R.string.month_average, wholeNumber(average))
                dashboard.monthlyFitnessChange?.let { MonthChange(it) }
            }
        }
    }
}

@Composable
private fun LastRoundCard(
    round: DashboardRoundSummary,
    pillars: FitnessScoreResponse?,
    onOpenRound: (UUID) -> Unit,
) {
    SectionCard(R.string.last_round) {
        Row(
            modifier =
                Modifier
                    .fillMaxWidth()
                    .heightIn(min = DesignTokens.touchTarget)
                    .clickable(role = Role.Button, onClickLabel = stringResource(R.string.open_round)) { onOpenRound(round.roundId) },
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(
                pluralStringResource(R.plurals.last_round_link, round.totalStrokes, formatDate(round.playedAt), round.totalStrokes),
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.primary,
                fontWeight = FontWeight.SemiBold,
            )
            Text("›", color = MaterialTheme.colorScheme.primary, modifier = Modifier.clearAndSetSemantics {})
        }
        val score = round.fitnessScore
        val confidence = Confidence.of(round.fitnessConfidence?.value)
        if (score != null && confidence != null) {
            val description = stringResource(R.string.fitness_score_value, score)
            Row(
                horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    score.toString(),
                    style = MaterialTheme.typography.displaySmall,
                    modifier = Modifier.semantics { contentDescription = description },
                )
                ConfidenceBadge(confidence, explain = true)
            }
        } else {
            Text(stringResource(R.string.not_scored_yet), style = MaterialTheme.typography.bodyLarge)
        }
        pillars?.let { PillarBars(it) }
    }
}

@Composable
private fun Stat(
    label: Int,
    value: String,
) {
    Column {
        Text(stringResource(label), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, style = MaterialTheme.typography.titleLarge)
    }
}

@Composable
private fun oneDecimal(value: Double): String =
    NumberFormat.getNumberInstance(currentLocale()).apply { maximumFractionDigits = 1 }.format(value)

@Composable
private fun wholeNumber(value: Double): String = NumberFormat.getIntegerInstance(currentLocale()).format(value)

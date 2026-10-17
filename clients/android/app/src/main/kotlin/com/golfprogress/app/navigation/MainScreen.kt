package com.golfprogress.app.navigation

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.List
import androidx.compose.material.icons.filled.AccountCircle
import androidx.compose.material.icons.filled.AddCircle
import androidx.compose.material.icons.filled.DateRange
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Star
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.golfprogress.app.R
import com.golfprogress.app.dashboard.DashboardScreen
import com.golfprogress.app.rounds.ReflectionScreen
import com.golfprogress.app.rounds.RoundDetailScreen
import com.golfprogress.app.rounds.RoundFormScreen
import com.golfprogress.app.rounds.RoundFormViewModel
import com.golfprogress.app.rounds.RoundsListScreen
import com.golfprogress.app.sections.ProfileContent
import com.golfprogress.app.sections.SectionContent
import com.golfprogress.app.ui.common.OfflineBanner
import java.util.UUID

/**
 * Destinations (docs/clients/ux-design.md → "Navigation"). Four are bottom tabs; Profile
 * opens from the avatar in the top bar, and Log round from the centre button.
 */
enum class Destination(
    val route: String,
    @StringRes val title: Int,
    val tab: Boolean,
) {
    Home("home", R.string.section_home, tab = true),
    Rounds("rounds", R.string.section_rounds, tab = true),
    LogRound("rounds/new", R.string.log_round, tab = false),
    RoundDetail("rounds/{roundId}", R.string.round_title, tab = false),
    EditRound("rounds/{roundId}/edit", R.string.edit_round, tab = false),
    AddReflection("rounds/{roundId}/reflect", R.string.add_reflection, tab = false),
    Practice("practice", R.string.section_practice, tab = true),
    Progress("progress", R.string.section_progress, tab = true),
    Profile("profile", R.string.section_profile, tab = false),
}

/** The bottom bar, in order: the centre item is Log round. */
private val bottomBar =
    listOf(
        Destination.Home to Icons.Filled.Home,
        Destination.Rounds to Icons.AutoMirrored.Filled.List,
        Destination.LogRound to Icons.Filled.AddCircle,
        Destination.Practice to Icons.Filled.Star,
        Destination.Progress to Icons.Filled.DateRange,
    )

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScreen(
    navController: NavHostController = rememberNavController(),
    onSignOut: () -> Unit = {},
) {
    val backStack by navController.currentBackStackEntryAsState()
    val current = Destination.entries.firstOrNull { it.route == backStack?.destination?.route } ?: Destination.Home

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(stringResource(current.title), modifier = Modifier.semantics { heading() }) },
                navigationIcon = {
                    if (!current.tab) {
                        IconButton(onClick = { navController.popBackStack() }) {
                            Icon(
                                Icons.AutoMirrored.Filled.ArrowBack,
                                contentDescription = stringResource(R.string.navigate_back),
                            )
                        }
                    }
                },
                actions = {
                    if (current != Destination.Profile) {
                        IconButton(onClick = { navController.navigate(Destination.Profile.route) }) {
                            Icon(Icons.Filled.AccountCircle, contentDescription = stringResource(R.string.open_profile))
                        }
                    }
                },
            )
        },
        bottomBar = {
            NavigationBar {
                bottomBar.forEach { (destination, icon) ->
                    BottomItem(destination, icon, selected = destination == current) {
                        navController.navigateTo(destination)
                    }
                }
            }
        },
    ) { padding ->
        val logRound = { navController.navigateTo(Destination.LogRound) }
        Column(Modifier.padding(padding)) {
            OfflineBanner()
            NavHost(navController, startDestination = Destination.Home.route, modifier = Modifier.weight(1f)) {
                val openRound = { id: UUID -> navController.navigate("rounds/$id") }
                composable(Destination.Home.route) { DashboardScreen(onLogRound = logRound, onOpenRound = openRound) }
                composable(Destination.Rounds.route) { RoundsListScreen(onOpenRound = openRound, onLogRound = logRound) }
                // After saving, the new round's detail replaces the form: that score is the reward.
                composable(Destination.LogRound.route) {
                    RoundFormScreen(onSaved = { id ->
                        navController.navigate("rounds/$id") { popUpTo(Destination.LogRound.route) { inclusive = true } }
                    })
                }
                composable(Destination.RoundDetail.route, arguments = listOf(roundIdArgument)) {
                    RoundDetailScreen(
                        onEdit = { id -> navController.navigate("rounds/$id/edit") },
                        onDeleted = { navController.popBackStack() },
                        onAddReflection = { id -> navController.navigate("rounds/$id/reflect") },
                    )
                }
                composable(Destination.EditRound.route, arguments = listOf(roundIdArgument)) {
                    RoundFormScreen(onSaved = { navController.popBackStack() })
                }
                composable(Destination.AddReflection.route, arguments = listOf(roundIdArgument)) {
                    ReflectionScreen(onSaved = { navController.popBackStack() })
                }
                composable(Destination.Practice.route) { SectionContent(R.string.practice_empty) }
                composable(Destination.Progress.route) { SectionContent(R.string.progress_empty) }
                composable(Destination.Profile.route) { ProfileContent(onSignOut) }
            }
        }
    }
}

@Composable
private fun RowScope.BottomItem(
    destination: Destination,
    icon: ImageVector,
    selected: Boolean,
    onClick: () -> Unit,
) {
    NavigationBarItem(
        selected = selected,
        onClick = onClick,
        icon = { Icon(icon, contentDescription = null) },
        label = { Text(stringResource(destination.title)) },
    )
}

private val roundIdArgument = navArgument(RoundFormViewModel.ROUND_ID) { type = NavType.StringType }

/** Tabs replace each other instead of stacking; Log round and Profile stack on top. */
private fun NavHostController.navigateTo(destination: Destination) {
    navigate(destination.route) {
        if (destination.tab) {
            popUpTo(graph.findStartDestination().id) { saveState = true }
            restoreState = true
        }
        launchSingleTop = true
    }
}

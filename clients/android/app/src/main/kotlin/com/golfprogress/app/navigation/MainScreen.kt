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
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.golfprogress.app.R
import com.golfprogress.app.dashboard.DashboardScreen
import com.golfprogress.app.rounds.LogRoundScreen
import com.golfprogress.app.sections.ProfileContent
import com.golfprogress.app.sections.SectionContent
import com.golfprogress.app.ui.common.OfflineBanner

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
                // The round detail arrives with #97; until then the last round opens Rounds.
                composable(Destination.Home.route) {
                    DashboardScreen(onLogRound = logRound, onOpenRound = { navController.navigateTo(Destination.Rounds) })
                }
                composable(Destination.Rounds.route) { SectionContent(R.string.rounds_empty, onLogRound = logRound) }
                // The round detail arrives with #97; until then a saved round shows on Home.
                composable(Destination.LogRound.route) { LogRoundScreen(onSaved = { navController.navigateTo(Destination.Home) }) }
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

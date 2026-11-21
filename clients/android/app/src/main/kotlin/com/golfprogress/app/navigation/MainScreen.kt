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
import androidx.compose.material.icons.filled.Face
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
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Density
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavBackStackEntry
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.golfprogress.app.R
import com.golfprogress.app.coaching.CoachesScreen
import com.golfprogress.app.coaching.GOLFER_ID
import com.golfprogress.app.coaching.StudentOverviewActions
import com.golfprogress.app.coaching.StudentOverviewScreen
import com.golfprogress.app.coaching.StudentOverviewViewModel
import com.golfprogress.app.coaching.StudentsScreen
import com.golfprogress.app.dashboard.DashboardScreen
import com.golfprogress.app.feedback.CoachFeedbackSection
import com.golfprogress.app.feedback.FeedbackSection
import com.golfprogress.app.feedback.FeedbackSlot
import com.golfprogress.app.feedback.NoFeedback
import com.golfprogress.app.goals.GoalDetailScreen
import com.golfprogress.app.goals.GoalDetailViewModel
import com.golfprogress.app.goals.GoalFormScreen
import com.golfprogress.app.practice.LogPracticeScreen
import com.golfprogress.app.practice.PracticeDetailScreen
import com.golfprogress.app.practice.PracticeDetailViewModel
import com.golfprogress.app.practice.PracticeListScreen
import com.golfprogress.app.profile.ProfileScreen
import com.golfprogress.app.progress.ProgressScreen
import com.golfprogress.app.rounds.ReflectionScreen
import com.golfprogress.app.rounds.RoundDetailScreen
import com.golfprogress.app.rounds.RoundFormScreen
import com.golfprogress.app.rounds.RoundFormViewModel
import com.golfprogress.app.rounds.RoundsListScreen
import com.golfprogress.app.ui.common.Load
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
    LogPractice("practice/new", R.string.log_practice, tab = false),
    PracticeDetail("practice/{sessionId}", R.string.practice_session, tab = false),
    GoalDetail("practice/goals/{goalId}", R.string.practice_goal, tab = false),
    Progress("progress", R.string.section_progress, tab = true),
    Profile("profile", R.string.section_profile, tab = false),
    Coaches("profile/coaches", R.string.coaches_title, tab = false),

    // The coach's workspace (#208): a student's screens are the golfer's own, read-only.
    Students("students", R.string.students, tab = false),
    StudentOverview("students/{golferId}", R.string.student, tab = false),
    StudentRounds("students/{golferId}/rounds", R.string.section_rounds, tab = false),
    StudentRound("students/{golferId}/rounds/{roundId}", R.string.round_title, tab = false),
    StudentPractice("students/{golferId}/practice", R.string.section_practice, tab = false),
    StudentSession("students/{golferId}/practice/{sessionId}", R.string.practice_session, tab = false),

    // Not under goals/: "goals/new" would also match goals/{goalId}.
    SetGoal("students/{golferId}/set-goal", R.string.set_practice_goal, tab = false),
    StudentGoal("students/{golferId}/goals/{goalId}", R.string.practice_goal, tab = false),
    ChangeGoal("students/{golferId}/goals/{goalId}/edit", R.string.change_goal, tab = false),
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
    /** After onboarding (#113): open Log round straight away. */
    openLogRound: Boolean = false,
    /** The signed-in golfer, whose feedback threads the detail screens show (#217). */
    userId: UUID? = null,
) {
    val feedback: FeedbackSlot =
        userId?.let { golferId -> { target, targetId -> FeedbackSection(golferId, target, targetId) } } ?: NoFeedback
    LaunchedEffect(Unit) { if (openLogRound) navController.navigateTo(Destination.LogRound) }
    val backStack by navController.currentBackStackEntryAsState()
    val current = Destination.entries.firstOrNull { it.route == backStack?.destination?.route } ?: Destination.Home
    val coachEntry: CoachEntryViewModel = hiltViewModel()
    val hasStudents by coachEntry.hasStudents.collectAsStateWithLifecycle()
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { coachEntry.refresh() }

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
                    if (hasStudents && current != Destination.Students) {
                        IconButton(onClick = { navController.navigate(Destination.Students.route) { launchSingleTop = true } }) {
                            Icon(Icons.Filled.Face, contentDescription = stringResource(R.string.open_students))
                        }
                    }
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
                composable(Destination.Home.route) {
                    DashboardScreen(
                        onLogRound = logRound,
                        onOpenRound = openRound,
                        onOpenPractice = { id -> navController.navigate("practice/$id") },
                    )
                }
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
                        feedback = feedback,
                    )
                }
                composable(Destination.EditRound.route, arguments = listOf(roundIdArgument)) {
                    RoundFormScreen(onSaved = { navController.popBackStack() })
                }
                composable(Destination.AddReflection.route, arguments = listOf(roundIdArgument)) {
                    ReflectionScreen(onSaved = { navController.popBackStack() })
                }
                composable(Destination.Practice.route) {
                    PracticeListScreen(
                        onOpen = { id -> navController.navigate("practice/$id") },
                        onLogPractice = { navController.navigate(Destination.LogPractice.route) },
                        onOpenGoal = { id -> navController.navigate("practice/goals/$id") },
                    )
                }
                composable(Destination.LogPractice.route) {
                    LogPracticeScreen(onSaved = { id ->
                        navController.navigate("practice/$id") { popUpTo(Destination.LogPractice.route) { inclusive = true } }
                    })
                }
                composable(
                    Destination.PracticeDetail.route,
                    arguments = listOf(navArgument(PracticeDetailViewModel.SESSION_ID) { type = NavType.StringType }),
                ) { PracticeDetailScreen(onDeleted = { navController.popBackStack() }, feedback = feedback) }
                composable(
                    Destination.GoalDetail.route,
                    arguments = listOf(navArgument(GoalDetailViewModel.GOAL_ID) { type = NavType.StringType }),
                ) { GoalDetailScreen(onOpenSession = { id -> navController.navigate("practice/$id") }) }
                composable(Destination.Progress.route) { ProgressScreen(onLogRound = logRound) }
                composable(Destination.Profile.route) {
                    ProfileScreen(onSignOut = onSignOut, onOpenCoaches = { navController.navigate(Destination.Coaches.route) })
                }
                composable(Destination.Coaches.route) { CoachesScreen() }
                studentScreens(navController, coachId = userId)
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
    // Log round's tab is shorter than its screen title, so it fits in Dutch too (#293).
    val label = stringResource(if (destination == Destination.LogRound) R.string.tab_log_round else destination.title)
    NavigationBarItem(
        selected = selected,
        onClick = onClick,
        icon = { Icon(icon, contentDescription = null) },
        label = {
            // Five tabs share the width, so their labels grow to 1.3× at most and stay on one
            // line rather than breaking mid-word at large font sizes (#120). TalkBack reads them
            // in full, and every screen's own text still scales all the way.
            val density = LocalDensity.current
            val fontScale = density.fontScale.coerceAtMost(MAX_TAB_LABEL_SCALE)
            CompositionLocalProvider(LocalDensity provides Density(density.density, fontScale)) {
                Text(label, maxLines = 1, overflow = TextOverflow.Ellipsis)
            }
        },
    )
}

private const val MAX_TAB_LABEL_SCALE = 1.3f

/**
 * The coach's workspace (#208): Students, a student's overview, and their rounds, practice and
 * goals on the golfer's own screens, which read the coach's routes when the route names a
 * student, and leave out what changes the golfer's data. Feedback is the coach's own thread.
 */
private fun NavGraphBuilder.studentScreens(
    navController: NavHostController,
    coachId: UUID?,
) {
    val golfer = navArgument(GOLFER_ID) { type = NavType.StringType }

    fun root(entry: NavBackStackEntry) = "students/${entry.arguments?.getString(GOLFER_ID)}"

    composable(Destination.Students.route) {
        StudentsScreen(onOpenStudent = { id -> navController.navigate("students/$id") })
    }
    composable(Destination.StudentOverview.route, arguments = listOf(golfer)) { entry ->
        val root = root(entry)
        StudentOverviewScreen(
            StudentOverviewActions(
                onOpenRound = { id -> navController.navigate("$root/rounds/$id") },
                onOpenRounds = { navController.navigate("$root/rounds") },
                onOpenPractice = { navController.navigate("$root/practice") },
                onOpenGoal = { id -> navController.navigate("$root/goals/$id") },
                onSetGoal = { navController.navigate("$root/set-goal") },
                onStopped = { navController.popBackStack(Destination.Students.route, inclusive = false) },
            ),
        )
    }
    composable(Destination.StudentRounds.route, arguments = listOf(golfer)) { entry ->
        RoundsListScreen(onOpenRound = { id -> navController.navigate("${root(entry)}/rounds/$id") }, onLogRound = {})
    }
    composable(Destination.StudentRound.route, arguments = listOf(golfer, roundIdArgument)) { entry ->
        RoundDetailScreen(onEdit = {}, onDeleted = {}, onAddReflection = {}, feedback = studentFeedback(navController, entry, coachId))
    }
    composable(Destination.StudentPractice.route, arguments = listOf(golfer)) { entry ->
        val root = root(entry)
        PracticeListScreen(
            onOpen = { id -> navController.navigate("$root/practice/$id") },
            onLogPractice = {},
            onOpenGoal = { id -> navController.navigate("$root/goals/$id") },
            onSetGoal = { navController.navigate("$root/set-goal") },
        )
    }
    composable(
        Destination.StudentSession.route,
        arguments = listOf(golfer, navArgument(PracticeDetailViewModel.SESSION_ID) { type = NavType.StringType }),
    ) { entry -> PracticeDetailScreen(onDeleted = {}, feedback = studentFeedback(navController, entry, coachId)) }
    composable(Destination.SetGoal.route, arguments = listOf(golfer)) { entry ->
        // The new goal's detail replaces the form.
        GoalFormScreen(onSaved = { id ->
            navController.navigate("${root(entry)}/goals/$id") { popUpTo(Destination.SetGoal.route) { inclusive = true } }
        })
    }
    val goal = navArgument(GoalDetailViewModel.GOAL_ID) { type = NavType.StringType }
    composable(Destination.StudentGoal.route, arguments = listOf(golfer, goal)) { entry ->
        val root = root(entry)
        GoalDetailScreen(
            onOpenSession = { id -> navController.navigate("$root/practice/$id") },
            onChange = { id -> navController.navigate("$root/goals/$id/edit") },
            onRemoved = { navController.popBackStack() },
        )
    }
    composable(Destination.ChangeGoal.route, arguments = listOf(golfer, goal)) {
        GoalFormScreen(onSaved = { navController.popBackStack() })
    }
}

/**
 * The coach's own feedback thread under a student's round, reflection or session. The
 * student's name comes from their overview, which is always below these screens.
 */
@Composable
private fun studentFeedback(
    navController: NavHostController,
    entry: NavBackStackEntry,
    coachId: UUID?,
): FeedbackSlot {
    val golferId = entry.arguments?.getString(GOLFER_ID)?.let(UUID::fromString)
    if (golferId == null || coachId == null) return NoFeedback
    val overviewEntry = remember(entry) { runCatching { navController.getBackStackEntry(Destination.StudentOverview.route) }.getOrNull() }
    val name =
        overviewEntry?.let {
            val overview: StudentOverviewViewModel = hiltViewModel(it)
            val state by overview.state.collectAsStateWithLifecycle()
            (state.overview as? Load.Loaded)?.value?.displayName
        } ?: stringResource(R.string.student)
    return { target, targetId -> CoachFeedbackSection(golferId, name, coachId, target, targetId) }
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

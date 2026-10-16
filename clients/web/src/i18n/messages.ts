import { defineMessage, defineMessages } from "react-intl";

/** Messages without values. English is the default and, for now, the only locale. */
const plainMessages = defineMessages({
  appName: { id: "app.name", defaultMessage: "GolfProgress" },
  skipToContent: { id: "app.skipToContent", defaultMessage: "Skip to content" },
  mainNavigation: { id: "nav.label", defaultMessage: "Main" },
  openMenu: { id: "nav.openMenu", defaultMessage: "Open menu" },
  closeMenu: { id: "nav.closeMenu", defaultMessage: "Close menu" },
  logRound: { id: "nav.logRound", defaultMessage: "Log round" },

  home: { id: "section.home", defaultMessage: "Home" },
  rounds: { id: "section.rounds", defaultMessage: "Rounds" },
  practice: { id: "section.practice", defaultMessage: "Practice" },
  progress: { id: "section.progress", defaultMessage: "Progress" },
  profile: { id: "section.profile", defaultMessage: "Profile" },

  homeEmpty: { id: "home.empty", defaultMessage: "Log your first round to see your fitness score." },
  roundsEmpty: { id: "rounds.empty", defaultMessage: "No rounds logged yet." },
  practiceEmpty: {
    id: "practice.empty",
    defaultMessage: "No practice logged yet. Practice counts towards your Habits score.",
  },
  progressEmpty: { id: "progress.empty", defaultMessage: "Log a few rounds to see your trends." },
  profileIntro: { id: "profile.intro", defaultMessage: "Your profile and preferences will appear here." },

  notFoundTitle: { id: "notFound.title", defaultMessage: "Page not found" },
  notFoundBody: { id: "notFound.body", defaultMessage: "This page doesn't exist." },
  backHome: { id: "notFound.backHome", defaultMessage: "Back to Home" },

  updateRequiredTitle: { id: "update.title", defaultMessage: "Update required" },
  updateRequiredBody: {
    id: "update.body",
    defaultMessage: "This version of GolfProgress is no longer supported. Reload the page to get the latest version.",
  },
  reload: { id: "update.reload", defaultMessage: "Reload" },

  restoringSession: { id: "session.restoring", defaultMessage: "Signing you in…" },
  welcomeTitle: { id: "welcome.title", defaultMessage: "GolfProgress" },
  welcomeBody: {
    id: "welcome.body",
    defaultMessage:
      "See your progress beyond the score: every round scored on execution, damage control, consistency and habits.",
  },
  createAccount: { id: "auth.createAccount", defaultMessage: "Create account" },
  signIn: { id: "auth.signIn", defaultMessage: "Sign in" },
  signOut: { id: "auth.signOut", defaultMessage: "Sign out" },
  name: { id: "auth.name", defaultMessage: "Name" },
  email: { id: "auth.email", defaultMessage: "Email" },
  password: { id: "auth.password", defaultMessage: "Password" },
  back: { id: "auth.back", defaultMessage: "Back" },
  working: { id: "auth.working", defaultMessage: "Please wait…" },
  haveAccount: { id: "auth.haveAccount", defaultMessage: "Already have an account? Sign in" },
  needAccount: { id: "auth.needAccount", defaultMessage: "New here? Create an account" },
  invalidEmail: { id: "auth.invalidEmail", defaultMessage: "Enter a valid email address." },
  wrongCredentials: { id: "auth.wrongCredentials", defaultMessage: "Email or password is wrong." },
  emailTaken: { id: "auth.emailTaken", defaultMessage: "An account with this email already exists. Sign in instead." },
  tooManyAttemptsLater: { id: "auth.tooManyAttemptsLater", defaultMessage: "Too many attempts. Try again later." },
  somethingWentWrong: { id: "auth.error", defaultMessage: "Something went wrong. Please try again." },
  cantConnect: {
    id: "auth.offline",
    defaultMessage: "Couldn't reach GolfProgress. Check your connection and try again.",
  },

  // Shared states (docs/clients/ux-design.md → "States every screen handles")
  offline: {
    id: "common.offline",
    defaultMessage: "You're offline. You can look around, but changes need a connection.",
  },
  loading: { id: "common.loading", defaultMessage: "Loading…" },
  tryAgain: { id: "common.tryAgain", defaultMessage: "Try again" },
  close: { id: "common.close", defaultMessage: "Close" },
  save: { id: "common.save", defaultMessage: "Save" },
  saving: { id: "common.saving", defaultMessage: "Saving…" },

  // Log round (#92)
  date: { id: "round.date", defaultMessage: "Date" },
  holes: { id: "round.holes", defaultMessage: "Holes" },
  course: { id: "round.course", defaultMessage: "Course" },
  tee: { id: "round.tee", defaultMessage: "Tee" },
  strokes: { id: "round.strokes", defaultMessage: "Strokes" },
  putts: { id: "round.putts", defaultMessage: "Putts" },
  penalties: { id: "round.penalties", defaultMessage: "Penalties" },
  fairways: { id: "round.fairways", defaultMessage: "Fairways hit" },
  greens: { id: "round.greens", defaultMessage: "Greens in regulation" },
  threePutts: { id: "round.threePutts", defaultMessage: "Three-putts" },
  blowUpHoles: { id: "round.blowUpHoles", defaultMessage: "Blow-up holes" },
  notes: { id: "round.notes", defaultMessage: "Notes" },
  moreStatsHint: { id: "logRound.moreStats", defaultMessage: "More stats? Change your tracking mode in Profile." },
  offlineCantSave: { id: "logRound.offline", defaultMessage: "You're offline. Connect to save this round." },
  preferencesLoadError: { id: "logRound.loadError", defaultMessage: "Couldn't load your preferences." },
  dateRequired: { id: "logRound.error.date", defaultMessage: "Choose the date you played." },
  strokesRequired: { id: "logRound.error.strokesRequired", defaultMessage: "Enter your strokes." },
  wholeNumber: { id: "logRound.error.wholeNumber", defaultMessage: "Enter a whole number." },
  strokesPositive: { id: "logRound.error.strokesPositive", defaultMessage: "Strokes must be more than 0." },
  notNegative: { id: "logRound.error.notNegative", defaultMessage: "Can't be less than 0." },
  roundSaveFailed: { id: "logRound.error.save", defaultMessage: "Couldn't save the round. Please try again." },

  // Round detail
  backToRounds: { id: "round.backToRounds", defaultMessage: "Back to rounds" },
  roundLoadError: { id: "round.loadError", defaultMessage: "Couldn't load this round." },
  roundNotFound: { id: "round.notFound", defaultMessage: "This round doesn't exist, or was deleted." },
  fitnessScore: { id: "score.title", defaultMessage: "Fitness score" },
  notScoredYet: { id: "score.notYet", defaultMessage: "This round hasn't been scored yet." },
  execution: { id: "pillar.execution", defaultMessage: "Execution" },
  damageControl: { id: "pillar.damageControl", defaultMessage: "Damage control" },
  consistency: { id: "pillar.consistency", defaultMessage: "Consistency" },
  habits: { id: "pillar.habits", defaultMessage: "Habits" },
  microWins: { id: "score.microWins", defaultMessage: "Micro-wins" },
  confidenceLow: { id: "confidence.low", defaultMessage: "Low" },
  confidenceMedium: { id: "confidence.medium", defaultMessage: "Medium" },
  confidenceHigh: { id: "confidence.high", defaultMessage: "High" },
  confidencePrefix: { id: "confidence.prefix", defaultMessage: "Confidence:" },

  // Dashboard (#88)
  dashboardLoadError: { id: "dashboard.loadError", defaultMessage: "Couldn't load your dashboard." },
  logFirstRound: { id: "dashboard.logFirstRound", defaultMessage: "Log your first round" },
  lastRound: { id: "dashboard.lastRound", defaultMessage: "Last round" },
  nextFocus: { id: "dashboard.nextFocus", defaultMessage: "Your next focus" },
  baseline: { id: "dashboard.baseline", defaultMessage: "Baseline" },
  baselineAverage: { id: "dashboard.baselineAverage", defaultMessage: "Average score" },
  thisMonth: { id: "dashboard.thisMonth", defaultMessage: "This month" },
  monthAverage: { id: "dashboard.monthAverage", defaultMessage: "Average fitness score" },
  sameAsLastMonth: { id: "dashboard.sameAsLastMonth", defaultMessage: "Same as last month" },
  lowConfidenceExplanation: {
    id: "confidence.lowExplanation",
    defaultMessage: "Based on only a few rounds so far: early days.",
  },
});

/** Any message that takes no values, for components that are handed one. */
export type PlainMessage = (typeof plainMessages)[keyof typeof plainMessages];

/**
 * Messages with values. react-intl types each message's values, so these declare theirs
 * and every use is checked by TypeScript.
 */
const messagesWithValues = {
  required: defineMessage<{ field: string }>({ id: "auth.required", defaultMessage: "{field} is required." }),
  tooManyAttempts: defineMessage<{ seconds: number }>({
    id: "auth.tooManyAttempts",
    defaultMessage: "Too many attempts. Try again in {seconds, plural, one {# second} other {# seconds}}.",
  }),

  holesOption: defineMessage<{ count: number }>({ id: "round.holesOption", defaultMessage: "{count} holes" }),
  atMostHoles: defineMessage<{ max: number }>({
    id: "logRound.error.atMostHoles",
    defaultMessage: "Can't be more than {max}, the number of holes.",
  }),
  atMostStrokes: defineMessage<{ max: number }>({
    id: "logRound.error.atMostStrokes",
    defaultMessage: "Can't be more than your strokes ({max}).",
  }),
  outOf: defineMessage<{ max: number }>({ id: "logRound.outOf", defaultMessage: "out of {max}" }),
  increase: defineMessage<{ field: string }>({ id: "logRound.increase", defaultMessage: "Increase {field}" }),
  decrease: defineMessage<{ field: string }>({ id: "logRound.decrease", defaultMessage: "Decrease {field}" }),
  pillarScore: defineMessage<{ score: number; max: number }>({
    id: "pillar.score",
    defaultMessage: "{score} / {max}",
  }),
  roundSummary: defineMessage<{ holes: number; strokes: number }>({
    id: "round.summary",
    defaultMessage: "{holes} holes · {strokes, plural, one {# stroke} other {# strokes}}",
  }),
  upOnLastMonth: defineMessage<{ change: string }>({
    id: "dashboard.upOnLastMonth",
    defaultMessage: "Up {change} on last month",
  }),
  downOnLastMonth: defineMessage<{ change: string }>({
    id: "dashboard.downOnLastMonth",
    defaultMessage: "Down {change} on last month",
  }),
  lastRoundLink: defineMessage<{ date: string; strokes: number }>({
    id: "dashboard.lastRoundLink",
    defaultMessage: "{date} · {strokes, plural, one {# stroke} other {# strokes}}",
  }),
  roundSummaryWithCourse: defineMessage<{ course: string; holes: number; strokes: number }>({
    id: "round.summaryWithCourse",
    defaultMessage: "{course} · {holes} holes · {strokes, plural, one {# stroke} other {# strokes}}",
  }),
};

/**
 * Every user-facing string in the app (#78). Components never contain literal text:
 * they use these through react-intl, so adding a language only needs translations.
 */
export const messages = { ...plainMessages, ...messagesWithValues };

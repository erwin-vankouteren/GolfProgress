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
  logRoundIntro: { id: "logRound.intro", defaultMessage: "The round form comes next." },

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
};

/**
 * Every user-facing string in the app (#78). Components never contain literal text:
 * they use these through react-intl, so adding a language only needs translations.
 */
export const messages = { ...plainMessages, ...messagesWithValues };

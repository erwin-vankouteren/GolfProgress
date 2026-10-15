import { defineMessages } from "react-intl";

/**
 * Every user-facing string in the app (#78). Components never contain literal text:
 * they use these through react-intl, so adding a language only needs translations.
 * English is the default and, for now, the only locale.
 */
export const messages = defineMessages({
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
});

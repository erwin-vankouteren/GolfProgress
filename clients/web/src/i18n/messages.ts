import { defineMessage, defineMessages } from "react-intl";

/** Messages without values. English is each defaultMessage; nl.ts has the Dutch. */
export const plainMessages = defineMessages({
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

  students: { id: "section.students", defaultMessage: "Students" },
  studentsLoadError: { id: "students.loadError", defaultMessage: "Couldn't load your students." },
  studentsEmpty: {
    id: "students.empty",
    defaultMessage: "No students yet. When you accept a golfer's invitation, they appear here.",
  },
  studentNoActivity: { id: "students.noActivity", defaultMessage: "No activity yet" },
  showMoreStudents: { id: "students.showMore", defaultMessage: "Show more students" },
  backToStudents: { id: "students.back", defaultMessage: "Back to students" },
  studentOverviewLoadError: {
    id: "students.overviewLoadError",
    defaultMessage: "Couldn't load this student's overview.",
  },
  studentGone: { id: "students.gone", defaultMessage: "You don't coach this golfer any more." },
  theirNextFocus: { id: "students.nextFocus", defaultMessage: "Their next focus" },
  recentRounds: { id: "students.recentRounds", defaultMessage: "Recent rounds" },
  latestReflections: { id: "students.latestReflections", defaultMessage: "Latest reflections" },
  noReflectionsYet: { id: "students.noReflections", defaultMessage: "No reflections yet." },
  reflectionsNotSharedPlain: {
    id: "students.reflectionsNotSharedPlain",
    defaultMessage: "Reflections aren't shared with you.",
  },
  practiceNotShared: {
    id: "students.practiceNotShared",
    defaultMessage: "This golfer doesn't share practice with you.",
  },
  studentPracticeEmpty: { id: "students.practiceEmpty", defaultMessage: "No practice logged yet." },
  allRounds: { id: "students.allRounds", defaultMessage: "All rounds" },
  practiceSessions: { id: "students.practiceSessions", defaultMessage: "Practice sessions" },
  breadcrumb: { id: "nav.breadcrumb", defaultMessage: "Breadcrumb" },
  feedback: { id: "feedback.title", defaultMessage: "Feedback" },
  feedbackFromYourCoach: { id: "feedback.fromYourCoach", defaultMessage: "Feedback from your coach" },
  feedbackLoadError: { id: "feedback.loadError", defaultMessage: "Couldn't load the feedback." },
  feedbackEmptyCoach: { id: "feedback.emptyCoach", defaultMessage: "No feedback yet. What did you notice?" },
  writeFeedback: { id: "feedback.write", defaultMessage: "Write feedback" },
  formerCoach: { id: "feedback.formerCoach", defaultMessage: "A former coach" },
  you: { id: "feedback.you", defaultMessage: "You" },
  yourStudent: { id: "feedback.yourStudent", defaultMessage: "Your student" },
  edited: { id: "feedback.edited", defaultMessage: "Edited" },
  editMessage: { id: "feedback.editMessage", defaultMessage: "Edit your message" },
  send: { id: "feedback.send", defaultMessage: "Send" },
  sending: { id: "feedback.sending", defaultMessage: "Sending…" },
  feedbackSendFailed: { id: "feedback.sendFailed", defaultMessage: "Couldn't send your message. Please try again." },
  feedbackSaveFailed: { id: "feedback.saveFailed", defaultMessage: "Couldn't save your message. Please try again." },
  feedbackDeleteFailed: {
    id: "feedback.deleteFailed",
    defaultMessage: "Couldn't delete your message. Please try again.",
  },
  deleteMessageTitle: { id: "feedback.deleteTitle", defaultMessage: "Delete this message?" },
  deleteMessageBody: {
    id: "feedback.deleteBody",
    defaultMessage: "It's removed for both of you. This can't be undone.",
  },
  tooManyMessages: { id: "feedback.tooMany", defaultMessage: "Too many messages this hour. Try again later." },
  writeSomethingFirst: { id: "feedback.empty", defaultMessage: "Write something first." },
  offlineCantSendMessage: { id: "feedback.offline", defaultMessage: "You're offline. Connect to send a message." },
  newFromYourCoach: { id: "feedback.newFromYourCoach", defaultMessage: "New from your coach" },
  goalsLoadError: { id: "goals.loadError", defaultMessage: "Couldn't load the practice goals." },
  yourGoalsForStudent: { id: "goals.yours", defaultMessage: "Your practice goals" },
  goalsFromYourCoach: { id: "goals.fromYourCoach", defaultMessage: "Goals from your coach" },
  setPracticeGoal: { id: "goals.set", defaultMessage: "Set a practice goal" },
  noGoalsYet: { id: "goals.none", defaultMessage: "No goals yet. Set one for their next practice sessions." },
  goalEnded: { id: "goals.ended", defaultMessage: "Ended" },
  goalReached: { id: "goals.reached", defaultMessage: "Target reached" },
  goalActive: { id: "goals.active", defaultMessage: "Active" },
  goalDone: { id: "goals.done", defaultMessage: "Done" },
  goalDismissed: { id: "goals.dismissed", defaultMessage: "Dismissed" },
  goalGone: { id: "goals.gone", defaultMessage: "This goal is no longer there." },
  goalLoadError: { id: "goals.goalLoadError", defaultMessage: "Couldn't load this goal." },
  goalFromFormerCoach: { id: "goals.fromFormerCoach", defaultMessage: "From a former coach" },
  goalWhat: { id: "goals.what", defaultMessage: "The goal" },
  goalTarget: { id: "goals.target", defaultMessage: "Sessions" },
  focusArea: { id: "goals.focusArea", defaultMessage: "Focus area" },
  goalSessions: { id: "goals.sessions", defaultMessage: "Sessions towards it" },
  goalNoSessions: {
    id: "goals.noSessions",
    defaultMessage: "No sessions yet. When you log one, choose this goal under Counts towards.",
  },
  goalNoSessionsCoach: { id: "goals.noSessionsCoach", defaultMessage: "No sessions towards it yet." },
  goalChangeFailed: { id: "goals.changeFailed", defaultMessage: "Couldn't change this goal. Please try again." },
  markGoalDone: { id: "goals.markDone", defaultMessage: "Mark done" },
  dismissGoal: { id: "goals.dismiss", defaultMessage: "Dismiss" },
  dismissGoalTitle: { id: "goals.dismissTitle", defaultMessage: "Dismiss this goal?" },
  dismissGoalBody: {
    id: "goals.dismissBody",
    defaultMessage: "It's no longer shown on Practice. Your coach sees that you dismissed it.",
  },
  goalHasSessions: {
    id: "goals.hasSessions",
    defaultMessage: "Sessions count towards this goal, so it can't be removed. Change its end date to end it sooner.",
  },
  changeGoal: { id: "goals.change", defaultMessage: "Change goal" },
  removeGoal: { id: "goals.remove", defaultMessage: "Remove goal" },
  removeGoalTitle: { id: "goals.removeTitle", defaultMessage: "Remove this goal?" },
  removeGoalBody: { id: "goals.removeBody", defaultMessage: "Your student no longer sees it. This can't be undone." },
  statPuttsPer18: { id: "goals.statPutts", defaultMessage: "Putts per 18 holes" },
  statPenaltiesPer18: { id: "goals.statPenalties", defaultMessage: "Penalties per 18 holes" },
  statFairwaysPer18: { id: "goals.statFairways", defaultMessage: "Fairways hit per 18 holes" },
  statScoreSpread: { id: "goals.statSpread", defaultMessage: "Spread of scores" },
  verdictCantTell: {
    id: "goals.verdictCantTell",
    defaultMessage:
      "Can't tell yet: this needs at least 2 rounds before the goal and 2 during it that track this stat.",
  },
  verdictNotMeasured: {
    id: "goals.verdictNotMeasured",
    defaultMessage: "This goal has no focus area, so there's no stat to compare.",
  },
  didItWork: { id: "goals.didItWork", defaultMessage: "Did it work?" },
  outcomeLoadError: { id: "goals.outcomeLoadError", defaultMessage: "Couldn't load whether it worked." },
  outcomeBefore: { id: "goals.before", defaultMessage: "Before the goal" },
  outcomeDuring: { id: "goals.during", defaultMessage: "During it, and two weeks after" },
  goalTitleRequired: { id: "goals.titleRequired", defaultMessage: "Give the goal a title." },
  goalEndBeforeStart: { id: "goals.endBeforeStart", defaultMessage: "The end can't be before the start." },
  goalEndInPast: { id: "goals.endInPast", defaultMessage: "The end can't be in the past." },
  goalTitle: { id: "goals.title", defaultMessage: "Title" },
  goalDetails: { id: "goals.details", defaultMessage: "Details" },
  goalFocusAreaHint: {
    id: "goals.focusAreaHint",
    defaultMessage:
      "Did it work? compares this stat before and during the goal, and it's their next focus while the goal lasts.",
  },
  noFocusArea: { id: "goals.noFocusArea", defaultMessage: "None" },
  anySessionType: { id: "goals.anySessionType", defaultMessage: "Any" },
  goalStarts: { id: "goals.starts", defaultMessage: "Starts" },
  goalEnds: { id: "goals.ends", defaultMessage: "Ends" },
  goalEndsHint: { id: "goals.endsHint", defaultMessage: "At most 8 weeks after the start." },
  goalSaveFailed: { id: "goals.saveFailed", defaultMessage: "Couldn't save the goal. Please try again." },
  offlineCantSaveGoal: { id: "goals.offline", defaultMessage: "You're offline. Connect to save the goal." },
  optional: { id: "common.optional", defaultMessage: "(optional)" },
  countsTowards: { id: "goals.countsTowards", defaultMessage: "Counts towards" },
  noGoal: { id: "goals.noGoal", defaultMessage: "No goal" },
  goalSuggested: { id: "goals.suggested", defaultMessage: "Suggested because it's for this type of session." },
  coachAlwaysSees: { id: "coaches.alwaysSees", defaultMessage: "Always sees your rounds and progress." },
  changeSharingFailed: {
    id: "coaches.changeFailed",
    defaultMessage: "Couldn't change what your coach sees. Please try again.",
  },
  removeCoach: { id: "coaches.remove", defaultMessage: "Remove" },
  removeCoachTitle: { id: "coaches.removeTitle", defaultMessage: "Remove this coach?" },
  removeCoachBody: {
    id: "coaches.removeBody",
    defaultMessage: "They lose access straight away. The feedback they gave you stays.",
  },
  removeCoachFailed: { id: "coaches.removeFailed", defaultMessage: "Couldn't remove this coach. Please try again." },
  stopCoaching: { id: "students.stop", defaultMessage: "Stop coaching" },
  stopCoachingTitle: { id: "students.stopTitle", defaultMessage: "Stop coaching this golfer?" },
  stopCoachingBody: {
    id: "students.stopBody",
    defaultMessage: "You lose access to what they share straight away. What you wrote for them stays with them.",
  },
  stopCoachingFailed: {
    id: "students.stopFailed",
    defaultMessage: "Couldn't stop coaching this golfer. Please try again.",
  },
  coachesTitle: { id: "coaches.title", defaultMessage: "Coaches" },
  coachesLoadError: { id: "coaches.loadError", defaultMessage: "Couldn't load your coaches." },
  coachesEmpty: { id: "coaches.empty", defaultMessage: "No coaches yet. Invite your coach to follow your progress." },
  coachesPending: { id: "coaches.pending", defaultMessage: "Pending invitations" },
  inviteCoach: { id: "coaches.invite", defaultMessage: "Invite a coach" },
  coachEmail: { id: "coaches.email", defaultMessage: "Your coach's email address" },
  whatCoachSees: { id: "coaches.whatTheySee", defaultMessage: "What your coach sees" },
  shareRounds: { id: "coaches.shareRounds", defaultMessage: "Rounds and progress" },
  shareRoundsHint: {
    id: "coaches.shareRoundsHint",
    defaultMessage: "Always shared: your rounds, fitness scores, next focus and monthly reviews.",
  },
  shareReflections: { id: "coaches.shareReflections", defaultMessage: "Reflections and notes" },
  shareReflectionsHint: {
    id: "coaches.shareReflectionsHint",
    defaultMessage: "Your round reflections, and the notes on your rounds and practice sessions.",
  },
  sharePractice: { id: "coaches.sharePractice", defaultMessage: "Practice" },
  sharePracticeHint: { id: "coaches.sharePracticeHint", defaultMessage: "Your practice sessions and practice goals." },
  neverShared: {
    id: "coaches.neverShared",
    defaultMessage:
      "Coaches never see your email address, preferences, handicap or typical score, or your other coaches.",
  },
  sendInvitation: { id: "coaches.send", defaultMessage: "Send invitation" },
  inviteFailed: { id: "coaches.inviteFailed", defaultMessage: "Couldn't send the invitation. Please try again." },
  tooManyInvitations: { id: "coaches.tooMany", defaultMessage: "Too many invitations today. Try again tomorrow." },
  cancelInvitationFailed: {
    id: "coaches.cancelFailed",
    defaultMessage: "Couldn't cancel the invitation. Please try again.",
  },
  offlineCantInvite: { id: "coaches.offline", defaultMessage: "You're offline. Connect to send an invitation." },
  invitationTitle: { id: "invitation.title", defaultMessage: "Coach invitation" },
  invitationSignedOut: {
    id: "invitation.signedOut",
    defaultMessage:
      "A golfer invited you to be their coach on GolfProgress. Sign in or create an account with the email address this invitation was sent to.",
  },
  invitationShares: { id: "invitation.shares", defaultMessage: "You'll see their:" },
  acceptInvitation: { id: "invitation.accept", defaultMessage: "Accept" },
  declineInvitation: { id: "invitation.decline", defaultMessage: "Decline" },
  invitationGone: {
    id: "invitation.gone",
    defaultMessage: "This invitation has expired, or was already used or cancelled. Ask the golfer to send a new one.",
  },
  invitationWrongAccount: {
    id: "invitation.wrongAccount",
    defaultMessage: "This invitation is for another email address. Sign in with the address it was sent to.",
  },
  signInAnotherAccount: { id: "invitation.otherAccount", defaultMessage: "Sign in with another account" },
  invitationFull: { id: "invitation.full", defaultMessage: "This golfer already has the most coaches they can have." },
  invitationLoadError: { id: "invitation.loadError", defaultMessage: "Couldn't load this invitation." },
  invitationDeclined: { id: "invitation.declined", defaultMessage: "You declined the invitation." },
  offlineCantRespond: { id: "invitation.offline", defaultMessage: "You're offline. Connect to accept or decline." },

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
  serverWaking: {
    id: "session.serverWaking",
    defaultMessage: "Waking up the server. This can take up to a minute.",
  },
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
  forgotPassword: { id: "auth.forgotPassword", defaultMessage: "Forgot password?" },
  resetPasswordTitle: { id: "reset.title", defaultMessage: "Reset your password" },
  resetPasswordIntro: {
    id: "reset.intro",
    defaultMessage: "Enter the email address you signed up with, and we'll send you a link to choose a new password.",
  },
  sendResetLink: { id: "reset.send", defaultMessage: "Send reset link" },
  backToSignIn: { id: "reset.backToSignIn", defaultMessage: "Back to sign in" },
  chooseNewPassword: { id: "reset.choose", defaultMessage: "Choose a new password" },
  setNewPassword: { id: "reset.save", defaultMessage: "Save new password" },
  passwordResetDone: {
    id: "reset.done",
    defaultMessage: "Your password is changed, and you're signed out on every device. Sign in with your new password.",
  },
  resetLinkInvalid: {
    id: "reset.invalid",
    defaultMessage: "This reset link is invalid or has expired: each link works once, for a limited time.",
  },
  askForNewLink: { id: "reset.askAgain", defaultMessage: "Ask for a new link" },
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

  // Round list, detail and editing (#95)
  roundsLoadError: { id: "rounds.loadError", defaultMessage: "Couldn't load your rounds." },
  showMoreRounds: { id: "rounds.showMore", defaultMessage: "Show more rounds" },
  selectRound: { id: "rounds.select", defaultMessage: "Choose a round to see its score." },
  strokesUnit: { id: "rounds.strokesUnit", defaultMessage: "strokes" },
  scoreUnit: { id: "rounds.scoreUnit", defaultMessage: "score" },
  notScored: { id: "rounds.notScored", defaultMessage: "Not scored" },
  roundNoLongerExists: {
    id: "round.noLongerExists",
    defaultMessage: "This round no longer exists. It may have been deleted on another device.",
  },
  edit: { id: "round.edit", defaultMessage: "Edit" },
  editRound: { id: "round.editTitle", defaultMessage: "Edit round" },
  stats: { id: "round.stats", defaultMessage: "Stats" },
  noStatsTracked: { id: "round.noStats", defaultMessage: "Only strokes were tracked for this round." },
  reflection: { id: "round.reflection", defaultMessage: "Reflection" },
  noReflection: {
    id: "round.noReflection",
    defaultMessage: "No reflection yet. Reflecting on a round counts towards your Habits score.",
  },
  focus: { id: "reflection.focus", defaultMessage: "Focus" },
  mood: { id: "reflection.mood", defaultMessage: "Mood" },
  frustrationCarriedOver: { id: "reflection.frustration", defaultMessage: "Frustration carried over" },
  yes: { id: "common.yes", defaultMessage: "Yes" },
  no: { id: "common.no", defaultMessage: "No" },
  whatWorked: { id: "reflection.whatWorked", defaultMessage: "What worked" },
  whatToRepeat: { id: "reflection.whatToRepeat", defaultMessage: "What to repeat" },
  oneChangeNext: { id: "reflection.oneChange", defaultMessage: "One change for next time" },
  addReflection: { id: "reflection.add", defaultMessage: "Add reflection" },
  addAnotherReflection: { id: "reflection.addAnother", defaultMessage: "Add another reflection" },
  saveReflection: { id: "reflection.save", defaultMessage: "Save reflection" },
  reflectionSaved: { id: "reflection.saved", defaultMessage: "Reflection saved." },
  reflectionHabitsHint: {
    id: "reflection.habitsHint",
    defaultMessage:
      "Reflecting counts towards the Habits part of your fitness score. This round's score includes it the next time the round is scored, for example when you edit it.",
  },
  reflectionNeedsSomething: {
    id: "reflection.needsSomething",
    defaultMessage: "Fill in at least one thing to save a reflection.",
  },
  frustrationQuestion: {
    id: "reflection.frustrationQuestion",
    defaultMessage: "Did frustration carry over between holes?",
  },
  clearRating: { id: "reflection.clear", defaultMessage: "Clear" },
  reflectionSaveFailed: {
    id: "reflection.saveFailed",
    defaultMessage: "Couldn't save the reflection. Please try again.",
  },
  reflectionLoadError: { id: "reflection.loadError", defaultMessage: "Couldn't load the reflection." },
  offlineCantSaveReflection: {
    id: "reflection.offline",
    defaultMessage: "You're offline. Connect to save this reflection.",
  },
  deleteRound: { id: "round.delete", defaultMessage: "Delete round" },
  deleteRoundTitle: { id: "round.deleteTitle", defaultMessage: "Delete this round?" },
  deleteRoundBody: {
    id: "round.deleteBody",
    defaultMessage:
      "Its fitness score, micro-wins and reflection are deleted with it, and your other scores are updated. This can't be undone.",
  },
  delete: { id: "common.delete", defaultMessage: "Delete" },
  deleting: { id: "common.deleting", defaultMessage: "Deleting…" },
  cancel: { id: "common.cancel", defaultMessage: "Cancel" },
  deleteFailed: { id: "round.deleteFailed", defaultMessage: "Couldn't delete the round. Please try again." },
  firstRound: {
    id: "explain.firstRound",
    defaultMessage: "There are no earlier rounds to compare with yet, so this starts in the middle.",
  },
  damageNotTracked: {
    id: "explain.damageNotTracked",
    defaultMessage: "Penalties and blow-up holes weren't tracked, so this is a middle score rather than full marks.",
  },
  consistencyNeedsRounds: {
    id: "explain.consistencyNeedsRounds",
    defaultMessage: "Judging consistency needs a few earlier rounds, so this starts in the middle.",
  },
  consistencyAtOrBetter: {
    id: "explain.consistencyAtOrBetter",
    defaultMessage: "At or better than your recent average: full marks.",
  },
  habitsUnknown: {
    id: "explain.habitsUnknown",
    defaultMessage: "Habits reward playing or practising about once a week, and reflecting on your rounds.",
  },
  reflected: { id: "explain.reflected", defaultMessage: "You reflected on this round." },
  notReflected: { id: "explain.notReflected", defaultMessage: "Adding a reflection would add to this." },

  // Practice (#103)
  logPractice: { id: "practice.log", defaultMessage: "Log practice" },
  practiceHabitsHint: { id: "practice.habitsHint", defaultMessage: "Practice counts towards your Habits score." },
  practiceLoadError: { id: "practice.loadError", defaultMessage: "Couldn't load your practice." },
  practiceSessionLoadError: { id: "practice.sessionLoadError", defaultMessage: "Couldn't load this session." },
  practiceNoLongerExists: {
    id: "practice.noLongerExists",
    defaultMessage: "This session no longer exists. It may have been deleted on another device.",
  },
  showMorePractice: { id: "practice.showMore", defaultMessage: "Show more sessions" },
  backToPractice: { id: "practice.back", defaultMessage: "Back to practice" },
  sessionType: { id: "practice.type", defaultMessage: "Type" },
  duration: { id: "practice.duration", defaultMessage: "Duration" },
  minutesUnit: { id: "practice.minutesUnit", defaultMessage: "minutes" },
  venue: { id: "practice.venue", defaultMessage: "Venue" },
  goal: { id: "practice.goal", defaultMessage: "Goal" },
  tags: { id: "practice.tags", defaultMessage: "Tags" },
  addTag: { id: "practice.addTag", defaultMessage: "Add tag" },
  tagsHint: { id: "practice.tagsHint", defaultMessage: "Press Enter or comma to add a tag." },
  tagDuplicate: { id: "practice.tagDuplicate", defaultMessage: "That tag is already there." },
  sessionRange: { id: "sessionType.range", defaultMessage: "Range" },
  sessionPutting: { id: "sessionType.putting", defaultMessage: "Putting" },
  sessionShortGame: { id: "sessionType.shortGame", defaultMessage: "Short game" },
  sessionSimulator: { id: "sessionType.simulator", defaultMessage: "Simulator" },
  sessionLesson: { id: "sessionType.lesson", defaultMessage: "Lesson" },
  sessionOther: { id: "sessionType.other", defaultMessage: "Other" },
  sessionUnknown: { id: "sessionType.unknown", defaultMessage: "Practice" },
  practiceSaveFailed: { id: "practice.saveFailed", defaultMessage: "Couldn't save the session. Please try again." },
  offlineCantSavePractice: {
    id: "practice.offline",
    defaultMessage: "You're offline. Connect to save this session.",
  },
  deletePractice: { id: "practice.delete", defaultMessage: "Delete session" },
  deletePracticeTitle: { id: "practice.deleteTitle", defaultMessage: "Delete this practice session?" },
  deletePracticeBody: {
    id: "practice.deleteBody",
    defaultMessage: "It no longer counts towards your Habits score. This can't be undone.",
  },
  deletePracticeFailed: {
    id: "practice.deleteFailed",
    defaultMessage: "Couldn't delete the session. Please try again.",
  },

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

  // Progress (#107)
  timelineTitle: { id: "progress.timeline", defaultMessage: "Rounds over time" },
  monthlyTrendTitle: { id: "progress.monthly", defaultMessage: "Monthly trend" },
  monthlyReviewTitle: { id: "progress.review", defaultMessage: "Monthly review" },
  progressLoadError: { id: "progress.loadError", defaultMessage: "Couldn't load your progress." },
  timelineLoadError: { id: "progress.timelineLoadError", defaultMessage: "Couldn't load your rounds over time." },
  monthlyLoadError: { id: "progress.monthlyLoadError", defaultMessage: "Couldn't load your monthly trend." },
  baselineLoadError: { id: "progress.baselineLoadError", defaultMessage: "Couldn't load your baseline." },
  reviewLoadError: { id: "progress.reviewLoadError", defaultMessage: "Couldn't load this review." },
  period: { id: "progress.period", defaultMessage: "Period" },
  range3m: { id: "progress.range3m", defaultMessage: "Last 3 months" },
  range6m: { id: "progress.range6m", defaultMessage: "Last 6 months" },
  range12m: { id: "progress.range12m", defaultMessage: "Last 12 months" },
  rangeAll: { id: "progress.rangeAll", defaultMessage: "All time" },
  rangeCustom: { id: "progress.rangeCustom", defaultMessage: "Choose dates" },
  rangeFrom: { id: "progress.rangeFrom", defaultMessage: "From" },
  rangeTo: { id: "progress.rangeTo", defaultMessage: "To" },
  rangeOrder: { id: "progress.rangeOrder", defaultMessage: "The start date must be on or before the end date." },
  showAsTable: { id: "progress.showAsTable", defaultMessage: "Show as table" },
  showAsChart: { id: "progress.showAsChart", defaultMessage: "Show as chart" },
  noRoundsInPeriod: {
    id: "progress.noRoundsInPeriod",
    defaultMessage: "No rounds in this period. Choose a longer one to see more.",
  },
  confidenceColumn: { id: "progress.confidenceColumn", defaultMessage: "Confidence" },
  monthColumn: { id: "progress.monthColumn", defaultMessage: "Month" },
  roundsColumn: { id: "progress.roundsColumn", defaultMessage: "Rounds" },
  changeColumn: { id: "progress.changeColumn", defaultMessage: "Change on previous month" },
  noScoredRounds: { id: "progress.noScoredRounds", defaultMessage: "No scored rounds" },
  monthlyEmpty: { id: "progress.monthlyEmpty", defaultMessage: "No scored rounds in the last 12 months." },
  monthlyNeedsAnotherMonth: {
    id: "progress.monthlyNeedsAnotherMonth",
    defaultMessage: "Play in another month to see a monthly trend.",
  },
  baselineEmpty: { id: "progress.baselineEmpty", defaultMessage: "Your baseline appears once you've logged a round." },
  baselineIntro: {
    id: "progress.baselineIntro",
    defaultMessage: "What your next round is compared with: your recent averages, per 18 holes.",
  },
  averageStrokes: { id: "progress.averageStrokes", defaultMessage: "Strokes" },
  spread: { id: "progress.spread", defaultMessage: "Spread" },
  statsTracked: { id: "progress.statsTracked", defaultMessage: "Stats tracked" },
  reviewMonth: { id: "progress.reviewMonth", defaultMessage: "Month" },
  focusNextMonth: { id: "progress.focusNextMonth", defaultMessage: "Your focus for next month" },
  focusHint: { id: "progress.focusHint", defaultMessage: "Optional. For example: fewer three-putts." },
  createReview: { id: "progress.createReview", defaultMessage: "Create review" },
  updateReview: { id: "progress.updateReview", defaultMessage: "Update review" },
  creatingReview: { id: "progress.creatingReview", defaultMessage: "Creating review…" },
  reviewReady: { id: "progress.reviewReady", defaultMessage: "Review ready." },
  reviewFailed: { id: "progress.reviewFailed", defaultMessage: "Couldn't create the review. Please try again." },
  offlineCantReview: { id: "progress.offlineReview", defaultMessage: "You're offline. Connect to create a review." },
  reviewFocus: { id: "progress.reviewFocus", defaultMessage: "Focus for next month" },
  reviewTheme: { id: "progress.reviewTheme", defaultMessage: "Most of your micro-wins" },
  themeStatDelta: { id: "theme.statDelta", defaultMessage: "Better stats" },
  themeBehavior: { id: "theme.behavior", defaultMessage: "Reflecting on your rounds" },
  themeNarrative: { id: "theme.narrative", defaultMessage: "Seeing the bigger picture" },
  themeHabit: { id: "theme.habit", defaultMessage: "Logging consistently" },
  themeRecovery: { id: "theme.recovery", defaultMessage: "Recovering from mistakes" },
  themeFocusArea: { id: "theme.focusArea", defaultMessage: "Your focus areas" },
  sameAsPreviousMonth: { id: "progress.sameAsPreviousMonth", defaultMessage: "Same as the month before" },

  // Profile (#111)
  yourDetails: { id: "profile.details", defaultMessage: "Your details" },
  preferences: { id: "profile.preferences", defaultMessage: "Preferences" },
  account: { id: "profile.account", defaultMessage: "Account" },
  about: { id: "profile.about", defaultMessage: "About" },
  profileLoadError: { id: "profile.loadError", defaultMessage: "Couldn't load your profile." },
  emailHint: { id: "profile.emailHint", defaultMessage: "You sign in with this. It can't be changed here." },
  handicapIndex: { id: "profile.handicapIndex", defaultMessage: "Handicap index" },
  typical18Score: { id: "profile.typical18Score", defaultMessage: "Typical 18-hole score" },
  optionalHint: { id: "profile.optional", defaultMessage: "Optional." },
  enterANumber: { id: "profile.enterANumber", defaultMessage: "Enter a number." },
  changesSaved: { id: "profile.saved", defaultMessage: "Changes saved." },
  profileSaveFailed: { id: "profile.saveFailed", defaultMessage: "Couldn't save your changes. Please try again." },
  offlineCantSaveProfile: { id: "profile.offline", defaultMessage: "You're offline. Connect to save changes." },
  units: { id: "profile.units", defaultMessage: "Units" },
  unitsMetric: { id: "profile.unitsMetric", defaultMessage: "Metric" },
  unitsImperial: { id: "profile.unitsImperial", defaultMessage: "Imperial" },
  trackingMode: { id: "profile.trackingMode", defaultMessage: "Tracking mode" },
  trackingMinimal: { id: "profile.trackingMinimal", defaultMessage: "Minimal" },
  trackingMinimalHint: { id: "profile.trackingMinimalHint", defaultMessage: "Just your score" },
  trackingBalanced: { id: "profile.trackingBalanced", defaultMessage: "Balanced" },
  trackingBalancedHint: { id: "profile.trackingBalancedHint", defaultMessage: "Adds putts and penalties" },
  trackingDetailed: { id: "profile.trackingDetailed", defaultMessage: "Detailed" },
  trackingDetailedHint: {
    id: "profile.trackingDetailedHint",
    defaultMessage: "Also fairways, greens in regulation, three-putts and blow-up holes",
  },
  canChangeAnyTime: { id: "profile.canChangeAnyTime", defaultMessage: "You can change this any time." },
  goalFocus: { id: "profile.goalFocus", defaultMessage: "Goal focus" },
  goalPutting: { id: "goal.putting", defaultMessage: "Putting" },
  goalPenalties: { id: "goal.penalties", defaultMessage: "Penalties" },
  goalTeeShots: { id: "goal.teeShots", defaultMessage: "Tee shots" },
  goalConsistency: { id: "goal.consistency", defaultMessage: "Consistency" },
  language: { id: "profile.language", defaultMessage: "Language" },
  languageDevice: { id: "profile.languageDevice", defaultMessage: "Same as this device" },
  languageHint: {
    id: "profile.languageHint",
    defaultMessage: "The app, your micro-wins and your next focus are shown in this language.",
  },
  emails: { id: "profile.emails", defaultMessage: "Emails" },
  emailFeedbackDigest: {
    id: "profile.emailFeedbackDigest",
    defaultMessage: "A daily email when your coach has sent you new feedback",
  },
  emailCoachDigest: {
    id: "profile.emailCoachDigest",
    defaultMessage: "A weekly email about what your students logged and replied",
  },
  changePassword: { id: "profile.changePassword", defaultMessage: "Change password" },
  currentPassword: { id: "profile.currentPassword", defaultMessage: "Current password" },
  newPassword: { id: "profile.newPassword", defaultMessage: "New password" },
  newPasswordHint: { id: "profile.newPasswordHint", defaultMessage: "At least 8 characters." },
  passwordChanged: { id: "profile.passwordChanged", defaultMessage: "Password changed." },
  passwordChangeFailed: {
    id: "profile.passwordChangeFailed",
    defaultMessage: "Couldn't change your password. Please try again.",
  },
  passwordOnlySpaces: { id: "profile.passwordOnlySpaces", defaultMessage: "The password can't be only spaces." },
  passwordIsEmail: { id: "profile.passwordIsEmail", defaultMessage: "The password can't be your email address." },
  passwordUnchanged: {
    id: "profile.passwordUnchanged",
    defaultMessage: "Choose a password different from the current one.",
  },
  signOutHint: { id: "profile.signOutHint", defaultMessage: "Signs you out on this device." },
  deleteAccount: { id: "profile.deleteAccount", defaultMessage: "Delete account" },
  deleteAccountHint: {
    id: "profile.deleteAccountHint",
    defaultMessage: "Permanently deletes your account and everything in it.",
  },
  deleteAccountTitle: { id: "profile.deleteAccountTitle", defaultMessage: "Delete your account?" },
  deleteAccountBody: {
    id: "profile.deleteAccountBody",
    defaultMessage:
      "This permanently deletes your account and all your rounds, scores, reflections and practice sessions, and signs you out everywhere. It can't be undone.",
  },
  deleteMyAccount: { id: "profile.deleteMyAccount", defaultMessage: "Delete my account" },
  deletingAccount: { id: "profile.deletingAccount", defaultMessage: "Deleting…" },
  deleteAccountFailed: {
    id: "profile.deleteAccountFailed",
    defaultMessage: "Couldn't delete your account. Please try again.",
  },
  privacyPolicy: { id: "profile.privacyPolicy", defaultMessage: "Privacy policy" },
  termsOfUse: { id: "profile.terms", defaultMessage: "Terms of use" },

  // Onboarding (#111)
  onboardingUnitsTitle: { id: "onboarding.units", defaultMessage: "Which units do you use?" },
  onboardingTrackingTitle: { id: "onboarding.tracking", defaultMessage: "How much do you want to track?" },
  onboardingGoalsTitle: { id: "onboarding.goals", defaultMessage: "What do you want to improve?" },
  next: { id: "onboarding.next", defaultMessage: "Next" },
  startLogging: { id: "onboarding.finish", defaultMessage: "Log your first round" },
  skipForNow: { id: "onboarding.skip", defaultMessage: "Skip for now" },

  // Subscriptions (#313, docs/payments.md)
  subscription: { id: "subscription.title", defaultMessage: "Subscription" },
  planLaunch: { id: "subscription.launch", defaultMessage: "GolfProgress is free during its launch period." },
  planReadOnly: { id: "subscription.readOnly", defaultMessage: "Your free trial has ended." },
  subscribedMonthly: { id: "subscription.monthly", defaultMessage: "Monthly subscription" },
  subscribedYearly: { id: "subscription.yearly", defaultMessage: "Yearly subscription" },
  coachingIncluded: { id: "subscription.coachingIncluded", defaultMessage: "Coaching included" },
  billingPeriod: { id: "subscription.period", defaultMessage: "Pay" },
  addCoaching: { id: "subscription.addCoaching", defaultMessage: "Add Coaching" },
  coachingHint: {
    id: "subscription.coachingHint",
    defaultMessage: "Invite your coach to follow your rounds and practice, give feedback and set practice goals.",
  },
  subscribe: { id: "subscription.subscribe", defaultMessage: "Subscribe" },
  manageSubscription: { id: "subscription.manage", defaultMessage: "Manage subscription" },
  managedInGooglePlay: {
    id: "subscription.googlePlay",
    defaultMessage: "You subscribed in the Android app. Manage it in Google Play.",
  },
  subscriptionUnavailable: { id: "subscription.unavailable", defaultMessage: "Subscribing isn't available yet." },
  subscriptionPricesError: { id: "subscription.pricesError", defaultMessage: "Couldn't load the prices." },
  subscribeFailed: { id: "subscription.failed", defaultMessage: "Couldn't open the payment page. Please try again." },
  subscriptionDone: {
    id: "subscription.done",
    defaultMessage: "Thanks for subscribing! It can take a moment to show here.",
  },
  offlineCantSubscribe: { id: "subscription.offline", defaultMessage: "You're offline. Connect to subscribe." },
  coachingNeeded: {
    id: "subscription.coachingNeeded",
    defaultMessage: "Add Coaching to your subscription to invite a coach and let your coaches follow your golf.",
  },
  addCoachingLink: { id: "subscription.addCoachingLink", defaultMessage: "Add Coaching" },
  studentPaused: {
    id: "students.paused",
    defaultMessage: "Paused: they don't have Coaching at the moment. You'll see their golf again when they add it.",
  },
  readOnlyBanner: {
    id: "subscription.readOnlyBanner",
    defaultMessage:
      "Your free trial has ended. Everything you've logged is still here; subscribe to log new rounds and practice.",
  },
});

/** Any message that takes no values, for components that are handed one. */
export type PlainMessage = (typeof plainMessages)[keyof typeof plainMessages];

/**
 * Messages with values. react-intl types each message's values, so these declare theirs
 * and every use is checked by TypeScript.
 */
export const messagesWithValues = {
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
  betterThanAverage: defineMessage<{ strokes: number; average: string; amount: string }>({
    id: "explain.betterThanAverage",
    defaultMessage: "{strokes} strokes, {amount} better than your recent average of {average}.",
  }),
  worseThanAverage: defineMessage<{ strokes: number; average: string; amount: string }>({
    id: "explain.worseThanAverage",
    defaultMessage: "{strokes} strokes, {amount} worse than your recent average of {average}.",
  }),
  levelWithAverage: defineMessage<{ strokes: number; average: string }>({
    id: "explain.levelWithAverage",
    defaultMessage: "{strokes} strokes, level with your recent average of {average}.",
  }),
  nineHoleEquivalent: defineMessage<{ strokes: number; per18: number }>({
    id: "explain.nineHoles",
    defaultMessage: "Over 9 holes, your {strokes} counts as {per18} over 18.",
  }),
  damage: defineMessage<{ penalties: string; blowUps: string }>({
    id: "explain.damage",
    defaultMessage: "{penalties}, {blowUps}. Fewer of either scores higher.",
  }),
  penaltiesCount: defineMessage<{ count: number }>({
    id: "explain.penalties",
    defaultMessage: "{count, plural, =0 {No penalties} one {# penalty} other {# penalties}}",
  }),
  blowUpsCount: defineMessage<{ count: number }>({
    id: "explain.blowUps",
    defaultMessage: "{count, plural, =0 {no blow-up holes} one {# blow-up hole} other {# blow-up holes}}",
  }),
  penaltiesNotTracked: defineMessage({ id: "explain.penaltiesNotTracked", defaultMessage: "Penalties not tracked" }),
  blowUpsNotTracked: defineMessage({ id: "explain.blowUpsNotTracked", defaultMessage: "blow-up holes not tracked" }),
  consistencyWorse: defineMessage<{ worseBy: string; spread: string }>({
    id: "explain.consistencyWorse",
    defaultMessage:
      "{worseBy} strokes worse than your recent average, where your rounds usually vary by about {spread}. Twice that scores nothing.",
  }),
  habitsExplanation: defineMessage<{ rounds: number; practice: number }>({
    id: "explain.habits",
    defaultMessage:
      "{rounds, plural, one {# round} other {# rounds}} and {practice, plural, one {# practice session} other {# practice sessions}} in the 28 days up to this round; four or more earns full points.",
  }),
  outOfFive: defineMessage<{ value: number }>({ id: "reflection.outOfFive", defaultMessage: "{value} of 5" }),
  clearNamedRating: defineMessage<{ field: string }>({ id: "reflection.clearNamed", defaultMessage: "Clear {field}" }),
  atMostCharacters: defineMessage<{ max: number }>({
    id: "reflection.atMostCharacters",
    defaultMessage: "Use at most {max, number} characters.",
  }),
  durationMinutes: defineMessage<{ minutes: number }>({
    id: "practice.durationMinutes",
    defaultMessage: "{minutes, number} min",
  }),
  durationRange: defineMessage<{ max: number }>({
    id: "practice.error.durationRange",
    defaultMessage: "Enter between 1 and {max, number} minutes.",
  }),
  tooManyTags: defineMessage<{ max: number }>({
    id: "practice.error.tooManyTags",
    defaultMessage: "Use at most {max} tags.",
  }),
  tagTooLong: defineMessage<{ max: number }>({
    id: "practice.error.tagTooLong",
    defaultMessage: "A tag can be at most {max} characters.",
  }),
  removeTag: defineMessage<{ tag: string }>({ id: "practice.removeTag", defaultMessage: "Remove tag {tag}" }),
  reflectedOn: defineMessage<{ date: string }>({ id: "reflection.reflectedOn", defaultMessage: "Added {date}" }),
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

  // Progress (#107)
  moreRoundsForTrend: defineMessage<{ count: number }>({
    id: "progress.moreRoundsForTrend",
    defaultMessage: "Log {count, plural, one {# more round} other {# more rounds}} to see a trend.",
  }),
  fewRoundsInPeriod: defineMessage<{ count: number }>({
    id: "progress.fewRoundsInPeriod",
    defaultMessage:
      "{count, plural, one {Only # scored round} other {Only # scored rounds}} in this period: not enough for a trend. Choose a longer period.",
  }),
  timelineSummary: defineMessage<{ rounds: number; from: string; to: string }>({
    id: "progress.timelineSummary",
    defaultMessage: "{rounds, plural, one {# round} other {# rounds}} from {from} to {to}.",
  }),
  timelineScoreSummary: defineMessage<{ first: number; last: number; average: number }>({
    id: "progress.timelineScoreSummary",
    defaultMessage: "Fitness score went from {first} to {last}, averaging {average}.",
  }),
  timelineCapped: defineMessage<{ count: number }>({
    id: "progress.timelineCapped",
    defaultMessage: "Showing your latest {count} rounds in this period.",
  }),
  strokesLine: defineMessage<{ holes: number }>({
    id: "progress.strokesLine",
    defaultMessage: "Strokes ({holes} holes)",
  }),
  strokesLeaveOut: defineMessage<{ holes: number }>({
    id: "progress.strokesLeaveOut",
    defaultMessage: "The strokes line shows {holes}-hole rounds only; the table has every round.",
  }),
  monthlySummaryOne: defineMessage<{ month: string; average: number }>({
    id: "progress.monthlySummaryOne",
    defaultMessage: "Average fitness score in {month}: {average}.",
  }),
  monthlySummary: defineMessage<{
    months: number;
    firstMonth: string;
    first: number;
    lastMonth: string;
    last: number;
  }>({
    id: "progress.monthlySummary",
    defaultMessage:
      "Scored rounds in {months, plural, one {# month} other {# months}}. Average fitness score went from {first} in {firstMonth} to {last} in {lastMonth}.",
  }),
  monthReviewed: defineMessage<{ month: string }>({
    id: "progress.monthReviewed",
    defaultMessage: "{month} (reviewed)",
  }),
  reviewNoRounds: defineMessage<{ month: string }>({
    id: "progress.reviewNoRounds",
    defaultMessage: "You didn't log any rounds in {month}, so there's nothing to review.",
  }),
  reviewNone: defineMessage<{ month: string }>({
    id: "progress.reviewNone",
    defaultMessage: "There's no review of {month} yet. Create one to see how the month went.",
  }),
  roundsPlayed: defineMessage<{ count: number }>({
    id: "progress.roundsPlayed",
    defaultMessage: "{count, plural, one {# round} other {# rounds}} played",
  }),
  reviewCreatedOn: defineMessage<{ date: string }>({
    id: "progress.reviewCreatedOn",
    defaultMessage: "Created {date}",
  }),
  baselineBasedOn: defineMessage<{ count: number }>({
    id: "progress.baselineBasedOn",
    defaultMessage: "Based on {count, plural, one {your last round} other {your last # rounds}}.",
  }),
  spreadValue: defineMessage<{ value: string }>({ id: "progress.spreadValue", defaultMessage: "± {value}" }),
  upOnPreviousMonth: defineMessage<{ change: string }>({
    id: "progress.upOnPreviousMonth",
    defaultMessage: "Up {change} on the month before",
  }),
  downOnPreviousMonth: defineMessage<{ change: string }>({
    id: "progress.downOnPreviousMonth",
    defaultMessage: "Down {change} on the month before",
  }),
  numberRange: defineMessage<{ min: number; max: number }>({
    id: "profile.numberRange",
    defaultMessage: "Enter a number from {min} to {max}.",
  }),
  passwordTooShort: defineMessage<{ min: number }>({
    id: "profile.passwordTooShort",
    defaultMessage: "Use at least {min} characters.",
  }),
  goalFocusHint: defineMessage<{ max: number }>({
    id: "profile.goalFocusHint",
    defaultMessage: "Choose up to {max}. Your micro-wins look for progress here first.",
  }),
  appVersion: defineMessage<{ version: string }>({ id: "profile.version", defaultMessage: "Version {version}" }),
  resetLinkSent: defineMessage<{ email: string }>({
    id: "reset.sent",
    defaultMessage:
      "If {email} has a GolfProgress account, a link to reset its password is on its way. It works for a limited time.",
  }),
  onboardingStep: defineMessage<{ step: number; total: number }>({
    id: "onboarding.step",
    defaultMessage: "Step {step} of {total}",
  }),
  studentLastActivity: defineMessage<{ date: string }>({
    id: "students.lastActivity",
    defaultMessage: "Last activity: {date}",
  }),
  studentUnreadReplies: defineMessage<{ count: number }>({
    id: "students.unreadReplies",
    defaultMessage: "{count, plural, one {# unread reply} other {# unread replies}}",
  }),
  studentShares: defineMessage<{ items: string }>({
    id: "students.shares",
    defaultMessage: "You see: {items}",
  }),
  removeCoachOf: defineMessage<{ name: string }>({
    id: "coaches.removeOf",
    defaultMessage: "Remove {name}",
  }),
  goalProgress: defineMessage<{ linked: number; target: number }>({
    id: "goals.progress",
    defaultMessage: "{linked} of {target, plural, one {# session} other {# sessions}}",
  }),
  goalFrom: defineMessage<{ coach: string }>({
    id: "goals.from",
    defaultMessage: "From {coach}",
  }),
  goalPeriod: defineMessage<{ from: string; to: string }>({
    id: "goals.period",
    defaultMessage: "{from} – {to}",
  }),
  verdictImproved: defineMessage<{ stat: string }>({
    id: "goals.verdictImproved",
    defaultMessage: "It's working: {stat} improved on before the goal.",
  }),
  verdictWorse: defineMessage<{ stat: string }>({
    id: "goals.verdictWorse",
    defaultMessage: "Not yet: {stat} isn't better than before the goal so far.",
  }),
  verdictSame: defineMessage<{ stat: string }>({
    id: "goals.verdictSame",
    defaultMessage: "About the same: {stat} is close to before the goal.",
  }),
  outcomeWindow: defineMessage<{ from: string; to: string }>({
    id: "goals.window",
    defaultMessage: "Rounds from {from} to {to} count as during.",
  }),
  outcomeRounds: defineMessage<{ count: number }>({
    id: "goals.rounds",
    defaultMessage: "({count, plural, one {# round} other {# rounds}})",
  }),
  goalTargetRange: defineMessage<{ max: number }>({
    id: "goals.targetRange",
    defaultMessage: "Choose 1 to {max} sessions.",
  }),
  goalTooLong: defineMessage<{ weeks: number }>({
    id: "goals.tooLong",
    defaultMessage: "A goal can last at most {weeks} weeks.",
  }),
  goalForStudent: defineMessage<{ name: string }>({
    id: "goals.forStudent",
    defaultMessage: "For {name}",
  }),
  unreadOnRound: defineMessage<{ coach: string; date: string }>({
    id: "feedback.unreadOnRound",
    defaultMessage: "{coach} on your round of {date}",
  }),
  unreadOnReflection: defineMessage<{ coach: string; date: string }>({
    id: "feedback.unreadOnReflection",
    defaultMessage: "{coach} on your reflection on the round of {date}",
  }),
  unreadOnReflectionNoDate: defineMessage<{ coach: string }>({
    id: "feedback.unreadOnReflectionNoDate",
    defaultMessage: "{coach} on your reflection",
  }),
  unreadOnSession: defineMessage<{ coach: string; date: string }>({
    id: "feedback.unreadOnSession",
    defaultMessage: "{coach} on your practice session of {date}",
  }),
  newMessages: defineMessage<{ count: number }>({
    id: "feedback.newMessages",
    defaultMessage: "{count, plural, one {# new message} other {# new messages}}",
  }),
  moreUnreadFeedback: defineMessage<{ count: number }>({
    id: "feedback.moreUnread",
    defaultMessage:
      "{count, plural, one {And # more new message on other items.} other {And # more new messages on other items.}}",
  }),
  feedbackEmptyGolfer: defineMessage<{ name: string }>({
    id: "feedback.emptyGolfer",
    defaultMessage: "No messages yet. You can ask {name} about this.",
  }),
  messageToCoach: defineMessage<{ name: string }>({
    id: "feedback.messageTo",
    defaultMessage: "Message to {name}",
  }),
  editMessageOf: defineMessage<{ when: string }>({
    id: "feedback.editOf",
    defaultMessage: "Edit your message of {when}",
  }),
  deleteMessageOf: defineMessage<{ when: string }>({
    id: "feedback.deleteOf",
    defaultMessage: "Delete your message of {when}",
  }),
  studentNoRounds: defineMessage<{ name: string }>({
    id: "students.noRounds",
    defaultMessage: "{name} hasn't logged a round yet. Their scores and next focus appear here after the first.",
  }),
  studentBaselineBasedOn: defineMessage<{ count: number }>({
    id: "students.baselineBasedOn",
    defaultMessage: "Based on {count, plural, one {their last round} other {their last # rounds}}.",
  }),
  studentTrend: defineMessage<{ first: number; last: number; count: number }>({
    id: "students.trend",
    defaultMessage: "Fitness score from {first} to {last} over their last {count} scored rounds.",
  }),
  reflectionsNotShared: defineMessage<{ name: string }>({
    id: "students.reflectionsNotShared",
    defaultMessage: "{name} doesn't share reflections with you.",
  }),
  reflectionOnRound: defineMessage<{ date: string }>({
    id: "students.reflectionOnRound",
    defaultMessage: "Round of {date}",
  }),
  stopCoachingOf: defineMessage<{ name: string }>({
    id: "students.stopOf",
    defaultMessage: "Stop coaching {name}",
  }),
  coachSees: defineMessage<{ items: string }>({
    id: "coaches.sees",
    defaultMessage: "Sees: {items}",
  }),
  invitationExpires: defineMessage<{ date: string }>({
    id: "coaches.expires",
    defaultMessage: "Pending, expires {date}",
  }),
  cancelInvitationOf: defineMessage<{ email: string }>({
    id: "coaches.cancelOf",
    defaultMessage: "Cancel the invitation to {email}",
  }),
  invitationSent: defineMessage<{ email: string }>({
    id: "coaches.sent",
    defaultMessage: "Invitation sent to {email}.",
  }),
  invitationResent: defineMessage<{ email: string }>({
    id: "coaches.resent",
    defaultMessage: "Invitation sent to {email} again. Only the newest link works.",
  }),
  invitationFrom: defineMessage<{ name: string }>({
    id: "invitation.from",
    defaultMessage: "{name} invited you to be their coach.",
  }),
  focusTooLong: defineMessage<{ max: number }>({
    id: "progress.focusTooLong",
    defaultMessage: "Keep it to {max} characters or fewer.",
  }),
  trialEndsOn: defineMessage<{ date: string }>({
    id: "subscription.trialEnds",
    defaultMessage: "Your free trial runs until {date}.",
  }),
  trialEndingBanner: defineMessage<{ date: string }>({
    id: "subscription.trialEndingBanner",
    defaultMessage: "Your free trial ends on {date}. Subscribe to keep logging your golf.",
  }),
  pricePerMonth: defineMessage<{ price: string }>({ id: "subscription.perMonth", defaultMessage: "{price} a month" }),
  pricePerYear: defineMessage<{ price: string }>({ id: "subscription.perYear", defaultMessage: "{price} a year" }),
  coachingFor: defineMessage<{ price: string }>({
    id: "subscription.coachingFor",
    defaultMessage: "Add Coaching, {price}",
  }),
  subscriptionRenews: defineMessage<{ date: string }>({
    id: "subscription.renews",
    defaultMessage: "Renews on {date}.",
  }),
  subscriptionCancels: defineMessage<{ date: string }>({
    id: "subscription.cancels",
    defaultMessage: "Ends on {date}: it won't renew.",
  }),
};

/**
 * Every user-facing string in the app (#78). Components never contain literal text:
 * they use these through react-intl, so adding a language only needs translations.
 */
export const messages = { ...plainMessages, ...messagesWithValues };

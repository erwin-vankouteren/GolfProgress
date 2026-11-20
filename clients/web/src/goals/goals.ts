import { knownSessionTypes, type Schemas } from "../api";
import { localIsoDate, parseWhole } from "../rounds/roundForm";

/**
 * Practice goals' rules (#227, #230, #235), without React: what to show, which goal a
 * session counts towards, and the coach's goal form, with the API's rules
 * (PracticeGoalRequest, #226).
 */

export type PracticeGoal = Schemas["PracticeGoalResponse"];
export type GoalOutcome = Schemas["GoalOutcomeResponse"];
export type FocusArea = NonNullable<PracticeGoal["focusArea"]>;

export const focusAreas: FocusArea[] = ["Putting", "Penalties", "TeeShots", "Consistency"];
export const maxTitle = 100;
export const maxDetails = 1000;
export const maxTargetSessions = 50;
export const maxGoalDays = 56;

/** Still worth showing on Practice: active, or ended without being marked done or dismissed. */
export function goalsToShow(goals: PracticeGoal[]): PracticeGoal[] {
  return goals.filter((goal) => goal.status === "Active" || goal.status === "Ended");
}

/** Whether the golfer can still mark it done or dismiss it. */
export function isOpen(goal: PracticeGoal): boolean {
  return goal.status === "Active" || goal.status === "Ended";
}

/** The goals a session on [practicedAt] can count towards: active, and the date within their period. */
export function linkableGoals(goals: PracticeGoal[], practicedAt: string): PracticeGoal[] {
  return goals.filter((goal) => goal.status === "Active" && goal.startsOn <= practicedAt && practicedAt <= goal.endsOn);
}

/** The goal to suggest for a session: the first linkable one set for that type of session. */
export function suggestedGoal(
  goals: PracticeGoal[],
  practicedAt: string,
  sessionType: string,
): PracticeGoal | undefined {
  return linkableGoals(goals, practicedAt).find((goal) => goal.sessionType === sessionType);
}

/** "3 of 6 sessions", and whether the target is reached. */
export function progressOf(goal: PracticeGoal) {
  return {
    linked: goal.linkedSessions,
    target: goal.targetSessions,
    reached: goal.linkedSessions >= goal.targetSessions,
  };
}

// ---------------------------------------------------------------- the coach's form

export interface GoalFormValues {
  title: string;
  details: string;
  focusArea: FocusArea | "";
  sessionType: (typeof knownSessionTypes)[number] | "";
  targetSessions: string;
  startsOn: string;
  endsOn: string;
}

export type GoalField = keyof GoalFormValues;

export type GoalError =
  | { code: "required" }
  | { code: "tooLong"; max: number }
  | { code: "targetRange"; max: number }
  | { code: "dateRequired" }
  | { code: "endBeforeStart" }
  | { code: "tooLongPeriod"; weeks: number }
  | { code: "endInPast" };

export type GoalErrors = Partial<Record<GoalField, GoalError>>;

const addDays = (iso: string, days: number) => {
  const date = new Date(`${iso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
};

const daysBetween = (from: string, to: string) =>
  Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86_400_000);

/** A new goal: from today, for four weeks, six sessions. */
export function emptyGoal(today: Date = new Date()): GoalFormValues {
  const start = localIsoDate(today);
  return {
    title: "",
    details: "",
    focusArea: "",
    sessionType: "",
    targetSessions: "6",
    startsOn: start,
    endsOn: addDays(start, 28),
  };
}

/** An existing goal in the form, to change it. */
export function goalToForm(goal: PracticeGoal): GoalFormValues {
  const type = knownSessionTypes.find((known) => known === goal.sessionType);
  return {
    title: goal.title,
    details: goal.details ?? "",
    focusArea: goal.focusArea ?? "",
    sessionType: type ?? "",
    targetSessions: String(goal.targetSessions),
    startsOn: goal.startsOn,
    endsOn: goal.endsOn,
  };
}

const length = (text: string) => [...text.trim()].length;
const isDate = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value);

export function validateGoal(values: GoalFormValues, today: Date = new Date()): GoalErrors {
  const errors: GoalErrors = {};

  if (length(values.title) === 0) errors.title = { code: "required" };
  else if (length(values.title) > maxTitle) errors.title = { code: "tooLong", max: maxTitle };
  if (length(values.details) > maxDetails) errors.details = { code: "tooLong", max: maxDetails };

  const target = parseWhole(values.targetSessions);
  if (target === undefined || target < 1 || target > maxTargetSessions)
    errors.targetSessions = { code: "targetRange", max: maxTargetSessions };

  if (!isDate(values.startsOn)) errors.startsOn = { code: "dateRequired" };
  if (!isDate(values.endsOn)) errors.endsOn = { code: "dateRequired" };
  else if (isDate(values.startsOn)) {
    if (values.endsOn < values.startsOn) errors.endsOn = { code: "endBeforeStart" };
    else if (daysBetween(values.startsOn, values.endsOn) > maxGoalDays)
      errors.endsOn = { code: "tooLongPeriod", weeks: maxGoalDays / 7 };
    else if (values.endsOn < localIsoDate(today)) errors.endsOn = { code: "endInPast" };
  }

  return errors;
}

/** The POST or PUT body. Optional choices left empty are sent as null. */
export function toGoalRequest(values: GoalFormValues): Schemas["PracticeGoalRequest"] {
  return {
    title: values.title.trim(),
    details: values.details.trim() || null,
    focusArea: values.focusArea || null,
    sessionType: values.sessionType || null,
    targetSessions: parseWhole(values.targetSessions) ?? 0,
    startsOn: values.startsOn,
    endsOn: values.endsOn,
  };
}

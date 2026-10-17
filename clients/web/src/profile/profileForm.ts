import type { Schemas } from "../api";

/**
 * Profile and preferences (#111): the rules the API applies to PATCH /me and
 * POST /me/password, checked before sending so mistakes show next to their fields.
 */

export type Me = Schemas["UserResponse"];
export type Units = Me["units"];
export type TrackingMode = Me["trackingMode"];

export const unitOptions: readonly Units[] = ["Metric", "Imperial"];
export const trackingModes: readonly TrackingMode[] = ["Minimal", "Balanced", "Detailed"];
/** GoalFocusArea in the API. */
export const goalFocusAreas = ["Putting", "Penalties", "TeeShots", "Consistency"] as const;
export type GoalFocusArea = (typeof goalFocusAreas)[number];

/** UpdateMeRequest.MaxGoalFocusAreas. */
export const maxGoalFocus = 3;
/** User.DisplayNameMaxLength. */
export const maxDisplayName = 100;
export const handicapRange = { min: -10, max: 54 } as const;
export const typical18Range = { min: 18, max: 200 } as const;
/** PasswordPolicy: counted in characters as people see them. */
export const passwordLength = { min: 8, max: 128 } as const;

const characters = (text: string) => [...text].length;

// ---------------------------------------------------------------- profile

export type ProfileField = "displayName" | "handicapIndex" | "typical18Score";
export type ProfileValues = Record<ProfileField, string>;
export type ProfileError =
  | { kind: "required" }
  | { kind: "tooLong"; max: number }
  | { kind: "notANumber" }
  | { kind: "notWhole" }
  | { kind: "outOfRange"; min: number; max: number };

export const profileValuesOf = (me: Me): ProfileValues => ({
  displayName: me.displayName ?? "",
  handicapIndex: me.handicapIndex === null ? "" : String(me.handicapIndex),
  typical18Score: me.typical18Score === null ? "" : String(me.typical18Score),
});

/** A decimal as typed, with a point or a comma; undefined when it isn't one. */
export function parseDecimal(text: string): number | undefined {
  const normalised = text.trim().replace(",", ".");
  if (!/^[+-]?(\d+\.?\d*|\.\d+)$/.test(normalised)) return undefined;
  return Number(normalised);
}

export function validateProfile(values: ProfileValues): Partial<Record<ProfileField, ProfileError>> {
  const errors: Partial<Record<ProfileField, ProfileError>> = {};

  const name = values.displayName.trim();
  if (name === "") errors.displayName = { kind: "required" };
  else if (characters(name) > maxDisplayName) errors.displayName = { kind: "tooLong", max: maxDisplayName };

  if (values.handicapIndex.trim() !== "") {
    const handicap = parseDecimal(values.handicapIndex);
    if (handicap === undefined) errors.handicapIndex = { kind: "notANumber" };
    else if (handicap < handicapRange.min || handicap > handicapRange.max)
      errors.handicapIndex = { kind: "outOfRange", ...handicapRange };
  }

  if (values.typical18Score.trim() !== "") {
    const score = parseDecimal(values.typical18Score);
    if (score === undefined) errors.typical18Score = { kind: "notANumber" };
    else if (!Number.isInteger(score)) errors.typical18Score = { kind: "notWhole" };
    else if (score < typical18Range.min || score > typical18Range.max)
      errors.typical18Score = { kind: "outOfRange", ...typical18Range };
  }

  return errors;
}

/** PATCH /me for the profile: an emptied handicap or typical score is cleared. */
export function toProfileRequest(values: ProfileValues): Schemas["UpdateMeRequest"] {
  const handicap = parseDecimal(values.handicapIndex);
  const typical = parseDecimal(values.typical18Score);
  return {
    displayName: values.displayName.trim(),
    handicapIndex: handicap === undefined ? null : handicap,
    typical18Score: typical === undefined ? null : typical,
  };
}

// ---------------------------------------------------------------- preferences

export type Preferences = { units: Units; trackingMode: TrackingMode; goalFocus: GoalFocusArea[] };

/** The user's goal focus, keeping only areas this build knows, in the API's order. */
export function preferencesOf(me: Me): Preferences {
  return {
    units: me.units,
    trackingMode: me.trackingMode,
    goalFocus: goalFocusAreas.filter((area) => me.goalFocus.includes(area)),
  };
}

/** Ticks or unticks an area; no more than three can be ticked. */
export function toggleGoalFocus(current: GoalFocusArea[], area: GoalFocusArea): GoalFocusArea[] {
  if (current.includes(area)) return current.filter((a) => a !== area);
  if (current.length >= maxGoalFocus) return current;
  return goalFocusAreas.filter((a) => a === area || current.includes(a));
}

// ---------------------------------------------------------------- password

export type PasswordError =
  | { kind: "required" }
  | { kind: "tooShort"; min: number }
  | { kind: "tooLong"; max: number }
  | { kind: "onlySpaces" }
  | { kind: "isEmail" }
  | { kind: "unchanged" };

/** PasswordPolicy.Check, plus "different from the current one". */
export function checkNewPassword(password: string, current: string, email: string | null): PasswordError | undefined {
  const length = characters(password);
  if (length === 0) return { kind: "required" };
  if (length < passwordLength.min) return { kind: "tooShort", min: passwordLength.min };
  if (length > passwordLength.max) return { kind: "tooLong", max: passwordLength.max };
  if (password.trim() === "") return { kind: "onlySpaces" };
  if (email && password.trim().toLowerCase() === email.trim().toLowerCase()) return { kind: "isEmail" };
  if (password === current) return { kind: "unchanged" };
  return undefined;
}

/** The browser's IANA time zone, sent at onboarding so the API can tell days apart. */
export function browserTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || undefined;
  } catch {
    return undefined;
  }
}

import { knownSessionTypes, type Schemas } from "../api";
import { localIsoDate, parseWhole } from "../rounds/roundForm";

/**
 * The Log practice form's rules (#103), without React: the API's rules
 * (CreatePracticeSessionRequest, #19) and the tags as chips.
 */

export type KnownSessionType = (typeof knownSessionTypes)[number];

export const maxDurationMinutes = 1440;
export const maxTags = 20;
export const maxTagLength = 50;
export const maxShortText = 200;
export const maxNotes = 2000;

export interface PracticeFormValues {
  practicedAt: string;
  sessionType: KnownSessionType;
  durationMinutes: string;
  venueName: string;
  primaryGoal: string;
  tags: string[];
  notes: string;
}

export type PracticeField = Exclude<keyof PracticeFormValues, "tags"> | "tags";

export type PracticeError =
  | { code: "dateRequired" }
  | { code: "wholeNumber" }
  | { code: "durationRange"; max: number }
  | { code: "tooLong"; max: number }
  | { code: "tooManyTags"; max: number };

export type PracticeErrors = Partial<Record<PracticeField, PracticeError>>;

/** Today, a range session, nothing else filled in. */
export function emptyPractice(today: Date = new Date()): PracticeFormValues {
  return {
    practicedAt: localIsoDate(today),
    sessionType: "Range",
    durationMinutes: "",
    venueName: "",
    primaryGoal: "",
    tags: [],
    notes: "",
  };
}

const length = (text: string) => [...text.trim()].length;

export function validatePractice(values: PracticeFormValues): PracticeErrors {
  const errors: PracticeErrors = {};

  if (!/^\d{4}-\d{2}-\d{2}$/.test(values.practicedAt)) errors.practicedAt = { code: "dateRequired" };

  if (values.durationMinutes.trim() !== "") {
    const minutes = parseWhole(values.durationMinutes);
    if (minutes === undefined) errors.durationMinutes = { code: "wholeNumber" };
    else if (minutes < 1 || minutes > maxDurationMinutes)
      errors.durationMinutes = { code: "durationRange", max: maxDurationMinutes };
  }

  if (length(values.venueName) > maxShortText) errors.venueName = { code: "tooLong", max: maxShortText };
  if (length(values.primaryGoal) > maxShortText) errors.primaryGoal = { code: "tooLong", max: maxShortText };
  if (length(values.notes) > maxNotes) errors.notes = { code: "tooLong", max: maxNotes };
  if (values.tags.length > maxTags) errors.tags = { code: "tooManyTags", max: maxTags };

  return errors;
}

export type AddTagResult =
  { ok: true; tags: string[] } | { ok: false; reason: "empty" | "duplicate" | "tooLong" | "tooMany" };

/**
 * Adds a tag from the tag input: trimmed, at most 50 characters, at most 20 tags, and not
 * one already there in any case.
 */
export function addTag(tags: string[], input: string): AddTagResult {
  const tag = input.trim();
  if (tag === "") return { ok: false, reason: "empty" };
  if ([...tag].length > maxTagLength) return { ok: false, reason: "tooLong" };
  if (tags.some((t) => t.toLowerCase() === tag.toLowerCase())) return { ok: false, reason: "duplicate" };
  if (tags.length >= maxTags) return { ok: false, reason: "tooMany" };
  return { ok: true, tags: [...tags, tag] };
}

/** The POST body. Optional text left empty is sent as null. */
export function toPracticeRequest(values: PracticeFormValues): Schemas["CreatePracticeSessionRequest"] {
  const text = (value: string) => value.trim() || null;
  return {
    practicedAt: values.practicedAt,
    sessionType: values.sessionType,
    durationMinutes: parseWhole(values.durationMinutes) ?? null,
    venueName: text(values.venueName),
    primaryGoal: text(values.primaryGoal),
    tags: values.tags,
    notes: text(values.notes),
  };
}

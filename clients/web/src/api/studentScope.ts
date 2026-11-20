import { createContext, useContext } from "react";

/**
 * Whose rounds and practice a screen shows (#206): the signed-in golfer's own (undefined),
 * or a student's, read-only, for their coach. The round and practice screens are the same
 * either way; their queries read the coach's routes and they leave out every action.
 */
export const StudentScope = createContext<string | undefined>(undefined);

/** The student's id inside a student's screens, undefined in the golfer's own. */
export function useStudentScope(): string | undefined {
  return useContext(StudentScope);
}

/** Where the rounds and practice sections are, and whether they can be changed. */
export function useSectionPaths() {
  const golferId = useStudentScope();
  return golferId
    ? { rounds: `/students/${golferId}/rounds`, practice: `/students/${golferId}/practice`, readOnly: true }
    : { rounds: "/rounds", practice: "/practice", readOnly: false };
}

/**
 * A golfer query key, under the student for a coach: ["students", id, ...key]. So
 * invalidating ["students"] covers a student's data, and the golfer's own keys are untouched.
 */
export function scopedKey<K extends readonly unknown[]>(golferId: string | undefined, key: K) {
  return golferId ? (["students", golferId, ...key] as const) : key;
}

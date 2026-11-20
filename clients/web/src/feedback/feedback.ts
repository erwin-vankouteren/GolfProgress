import type { Schemas } from "../api";

/** Feedback threads' logic (#214, #216), apart from the screens so it can be tested on its own. */

export type FeedbackMessage = Schemas["FeedbackMessageResponse"];
export type FeedbackTargetType = FeedbackMessage["targetType"];
export type Coach = Schemas["CoachResponse"];

/** The API's limit on a message, after trimming. */
export const feedbackTextMax = 2000;

export type TextError = "empty" | "tooLong";

export function checkFeedbackText(text: string): TextError | undefined {
  const trimmed = text.trim();
  if (trimmed === "") return "empty";
  return trimmed.length > feedbackTextMax ? "tooLong" : undefined;
}

/** Whether a coach can see an item of this type, so the golfer can write to them about it. */
export function coachCanSee(coach: Pick<Coach, "shareReflections" | "sharePractice">, type: FeedbackTargetType) {
  if (type === "Reflection") return coach.shareReflections;
  if (type === "PracticeSession") return coach.sharePractice;
  return true;
}

export type GolferThread = {
  /** The coach's user id, or null for a coach whose account is gone. */
  coachId: string | null;
  /** Their name, or null for "a former coach". */
  coachName: string | null;
  messages: FeedbackMessage[];
  /** Whether the golfer can write in it: the coach is theirs and can see the item. */
  canWrite: boolean;
};

/**
 * The golfer's threads on an item, one per coach: first each current coach who can see it,
 * in the order they became coaches, even before anyone has written; then any others with
 * messages (a coach who stopped, or no longer sees this), which can be read but not answered.
 */
export function golferThreads(messages: FeedbackMessage[], coaches: Coach[], type: FeedbackTargetType): GolferThread[] {
  const byCoach = new Map<string | null, FeedbackMessage[]>();
  for (const message of messages) {
    const list = byCoach.get(message.coachId) ?? [];
    list.push(message);
    byCoach.set(message.coachId, list);
  }

  const current = coaches
    .filter((coach) => coachCanSee(coach, type))
    .map((coach): GolferThread => ({
      coachId: coach.coachId,
      coachName: coach.displayName,
      messages: byCoach.get(coach.coachId) ?? [],
      canWrite: true,
    }));
  const currentIds = new Set(current.map((thread) => thread.coachId));
  const past = [...byCoach.entries()]
    .filter(([coachId]) => !currentIds.has(coachId))
    .map(([coachId, list]): GolferThread => ({
      coachId,
      coachName: list[0]?.coachName ?? null,
      messages: list,
      canWrite: false,
    }));

  return [...current, ...past];
}

/** The newest message's time, to mark the thread read up to; undefined for an empty one. */
export function newestMessageAt(messages: FeedbackMessage[]): string | undefined {
  return messages.reduce<string | undefined>(
    (newest, message) => (newest === undefined || message.createdAt > newest ? message.createdAt : newest),
    undefined,
  );
}

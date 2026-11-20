import type { Schemas } from "../api";

/** "New from your coach" (#221): the dashboard card's logic, apart from the screen. */

export type UnreadFeedback = Schemas["UnreadFeedbackResponse"];
export type UnreadThread = UnreadFeedback["threads"][number];

/** Where a thread is: its round (also for a reflection), or its practice session; none for a reflection without a round. */
export function unreadThreadPath(thread: UnreadThread): string | undefined {
  if (thread.targetType === "PracticeSession") return `/practice/${thread.targetId}`;
  return thread.roundId ? `/rounds/${thread.roundId}` : undefined;
}

/** Unread messages in threads the card doesn't list, which shows at most five. */
export function unreadNotListed(unread: UnreadFeedback): number {
  return Math.max(0, unread.count - unread.threads.reduce((sum, thread) => sum + thread.unread, 0));
}

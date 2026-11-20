import { describe, expect, it } from "vitest";
import {
  checkFeedbackText,
  coachCanSee,
  golferThreads,
  newestMessageAt,
  type Coach,
  type FeedbackMessage,
} from "./feedback";

const coach = (coachId: string, overrides: Partial<Coach> = {}): Coach => ({
  coachLinkId: `link-${coachId}`,
  coachId,
  displayName: coachId.toUpperCase(),
  shareReflections: true,
  sharePractice: true,
  createdAt: "",
  ...overrides,
});

const message = (
  coachId: string | null,
  createdAt: string,
  overrides: Partial<FeedbackMessage> = {},
): FeedbackMessage => ({
  feedbackMessageId: `${coachId}-${createdAt}`,
  golferId: "g",
  coachId,
  coachName: coachId ? coachId.toUpperCase() : null,
  fromCoach: true,
  mine: false,
  targetType: "Round",
  targetId: "t",
  text: "Hi",
  createdAt,
  editedAt: null,
  ...overrides,
});

describe("checkFeedbackText (#214, #216)", () => {
  it("needs 1 to 2000 characters after trimming", () => {
    expect(checkFeedbackText("   ")).toBe("empty");
    expect(checkFeedbackText(` ${"a".repeat(2000)} `)).toBeUndefined();
    expect(checkFeedbackText("a".repeat(2001))).toBe("tooLong");
  });
});

describe("coachCanSee", () => {
  it("follows what the golfer shares", () => {
    const limited = { shareReflections: false, sharePractice: false };
    expect(coachCanSee(limited, "Round")).toBe(true);
    expect(coachCanSee(limited, "Reflection")).toBe(false);
    expect(coachCanSee(limited, "PracticeSession")).toBe(false);
  });
});

describe("golferThreads (#216)", () => {
  it("gives each current coach who can see the item a thread, even an empty one, then past ones read-only", () => {
    const threads = golferThreads(
      [message("a", "2026-11-02"), message(null, "2026-10-01"), message("gone", "2026-10-05")],
      [coach("a"), coach("b"), coach("c", { shareReflections: false })],
      "Round",
    );

    expect(threads.map((t) => [t.coachId, t.canWrite, t.messages.length])).toEqual([
      ["a", true, 1],
      ["b", true, 0],
      ["c", true, 0],
      [null, false, 1],
      ["gone", false, 1],
    ]);
  });

  it("leaves out a coach who can't see the item, but keeps what they wrote, read-only", () => {
    const threads = golferThreads(
      [message("c", "2026-11-02")],
      [coach("c", { shareReflections: false })],
      "Reflection",
    );

    expect(threads).toEqual([expect.objectContaining({ coachId: "c", coachName: "C", canWrite: false })]);
  });

  it("is empty without coaches or messages", () => {
    expect(golferThreads([], [], "Round")).toEqual([]);
  });
});

describe("newestMessageAt", () => {
  it("finds the latest time, whatever the order", () => {
    expect(newestMessageAt([message("a", "2026-11-02T10:00:00Z"), message("a", "2026-11-03T09:00:00Z")])).toBe(
      "2026-11-03T09:00:00Z",
    );
    expect(newestMessageAt([])).toBeUndefined();
  });
});

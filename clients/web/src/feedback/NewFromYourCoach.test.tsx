import { screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { json, renderSignedIn, testUserId as userId } from "../test/fakeApi";
import { unreadNotListed, unreadThreadPath, type UnreadThread } from "./unread";

/** Issue #221: "New from your coach" on Home. */

const roundId = "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a";
const sessionId = "00000000-0000-0000-0000-0000000000s1";
const coachId = "00000000-0000-0000-0000-00000000000d";

const thread = (overrides: Partial<UnreadThread> = {}): UnreadThread => ({
  targetType: "Round",
  targetId: roundId,
  roundId,
  onDate: "2026-11-01",
  coachId,
  coachName: "Pro",
  unread: 2,
  newestAt: "2026-11-03T10:00:00Z",
  preview: "Lag putts to a metre.",
  ...overrides,
});

describe("the card's rules (#221)", () => {
  it("links to the round, a reflection's round, or the session", () => {
    expect(unreadThreadPath(thread())).toBe(`/rounds/${roundId}`);
    expect(unreadThreadPath(thread({ targetType: "Reflection", targetId: "r" }))).toBe(`/rounds/${roundId}`);
    expect(unreadThreadPath(thread({ targetType: "Reflection", roundId: null }))).toBeUndefined();
    expect(unreadThreadPath(thread({ targetType: "PracticeSession", targetId: sessionId, roundId: null }))).toBe(
      `/practice/${sessionId}`,
    );
  });

  it("counts what's on threads it doesn't list", () => {
    expect(unreadNotListed({ count: 5, threads: [thread({ unread: 2 }), thread({ unread: 1 })] })).toBe(2);
    expect(unreadNotListed({ count: 2, threads: [thread({ unread: 2 })] })).toBe(0);
  });
});

const round = {
  roundId,
  userId,
  playedAt: "2026-11-01",
  holes: 18,
  courseName: null,
  teeName: null,
  totalStrokes: 88,
  totalPutts: null,
  penalties: null,
  fairwaysHit: null,
  gir: null,
  threePutts: null,
  blowUpHoles: null,
  source: "Manual",
  notes: null,
  createdAt: "",
  updatedAt: "",
  fitnessScore: null,
  fitnessConfidence: null,
};

const dashboard = {
  userId,
  lastRound: null,
  microWins: [],
  nextLever: null,
  baselineAvgScore: null,
  baselineConfidence: null,
  monthlyFitnessAvg: null,
  monthlyFitnessChange: null,
  generatedAt: "",
};

describe("New from your coach (#221)", () => {
  it("lists what's new, opens the round, and is gone once it's read", async () => {
    let read = false;
    const { user } = renderSignedIn("/", {
      "GET /me/dashboard": () => json(200, dashboard),
      "GET /me/feedback/unread": () =>
        json(
          200,
          read
            ? { count: 0, threads: [] }
            : {
                count: 4,
                threads: [
                  thread(),
                  thread({
                    targetType: "PracticeSession",
                    targetId: sessionId,
                    roundId: null,
                    unread: 1,
                    coachName: null,
                  }),
                ],
              },
        ),
      "GET /me/rounds": () => json(200, [round]),
      [`GET /rounds/${roundId}`]: () => json(200, round),
      [`GET /rounds/${roundId}/micro-wins`]: () => json(200, []),
      [`GET /rounds/${roundId}/reflections`]: () => json(200, []),
      "GET /me/coaches": () =>
        json(200, {
          coaches: [
            {
              coachLinkId: "l",
              coachId,
              displayName: "Pro",
              shareReflections: true,
              sharePractice: true,
              createdAt: "",
            },
          ],
          invitations: [],
        }),
      "GET /feedback": () =>
        json(200, [
          {
            feedbackMessageId: "m",
            golferId: userId,
            coachId,
            coachName: "Pro",
            fromCoach: true,
            mine: false,
            targetType: "Round",
            targetId: roundId,
            text: "Lag putts to a metre.",
            createdAt: "2026-11-03T10:00:00Z",
            editedAt: null,
          },
        ]),
      "POST /feedback/read": () => {
        read = true;
        return new Response(null, { status: 204 });
      },
    });

    const card = await screen.findByRole("region", { name: "New from your coach" });
    expect(within(card).getByText("2 new messages")).toBeInTheDocument();
    expect(within(card).getByText("A former coach on your practice session of", { exact: false })).toBeInTheDocument();
    expect(within(card).getByText("And 1 more new message on other items.")).toBeInTheDocument();

    await user.click(within(card).getByRole("link", { name: /Pro on your round of Nov 1, 2026/ }));
    await screen.findByRole("region", { name: "Feedback from your coach" });
    await waitFor(() => expect(read).toBe(true));

    await user.click(screen.getByRole("link", { name: "Home" }));
    await screen.findByRole("heading", { level: 1, name: "Home" });
    await waitFor(() => expect(screen.queryByRole("region", { name: "New from your coach" })).not.toBeInTheDocument());
  });

  it("isn't there when nothing is new, or when it can't be checked", async () => {
    renderSignedIn("/", {
      "GET /me/dashboard": () => json(200, dashboard),
      "GET /me/feedback/unread": () => json(500, { status: 500 }),
    });

    await screen.findByText("Log your first round to see your fitness score.");
    expect(screen.queryByRole("region", { name: "New from your coach" })).not.toBeInTheDocument();
  });
});

import { screen, within } from "@testing-library/react";
import { beforeAll, describe, expect, it } from "vitest";
import { json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";

/** Issue #206: a student's rounds and practice, on the golfer's screens, read-only. */

const golferId = "00000000-0000-0000-0000-00000000000c";
const root = `/coach/students/${golferId}`;
const roundId = "00000000-0000-0000-0000-000000000001";
const sessionId = "00000000-0000-0000-0000-000000000002";

const me = {
  userId,
  email: "pro@example.test",
  displayName: "Pro",
  timezone: null,
  units: "Metric",
  trackingMode: "Balanced",
  language: null,
  handicapIndex: null,
  typical18Score: null,
  goalFocus: [],
  createdAt: "",
  updatedAt: "",
  hasStudents: true,
};

const round = {
  roundId,
  userId: golferId,
  playedAt: "2026-11-01",
  holes: 18,
  courseName: "Hilversum",
  teeName: null,
  totalStrokes: 88,
  totalPutts: 33,
  penalties: 1,
  fairwaysHit: null,
  gir: null,
  threePutts: null,
  blowUpHoles: 1,
  source: "Manual",
  notes: "Windy back nine",
  createdAt: "",
  updatedAt: "",
  fitnessScore: 66,
  fitnessConfidence: "Medium",
};

const session = {
  practiceSessionId: sessionId,
  userId: golferId,
  practicedAt: "2026-11-03",
  durationMinutes: 45,
  sessionType: "Putting",
  venueName: null,
  tags: [],
  primaryGoal: "Lag putts",
  notes: "Short putts",
  createdAt: "",
};

const overview = {
  golferId,
  displayName: "Sam",
  shareReflections: true,
  sharePractice: true,
  lastRound: { roundId, playedAt: "2026-11-01", totalStrokes: 88, fitnessScore: 66, fitnessConfidence: "Medium" },
  recentRounds: [],
  monthlyFitnessAvg: null,
  monthlyFitnessChange: null,
  nextFocus: null,
  baseline: null,
  latestReflections: [],
};

const paged = (body: unknown[]) =>
  new Response(JSON.stringify(body), {
    headers: { "Content-Type": "application/json", "X-Total-Count": String(body.length) },
  });

function handlers(overrides: Record<string, Handler> = {}): Record<string, Handler> {
  return {
    "GET /me": () => json(200, me),
    [`GET ${root}/overview`]: () => json(200, overview),
    [`GET ${root}/rounds`]: () => paged([round]),
    [`GET ${root}/rounds/${roundId}`]: () => json(200, round),
    [`GET ${root}/rounds/${roundId}/fitness-score`]: () =>
      json(200, {
        fitnessScoreId: "s",
        userId: golferId,
        roundId,
        scoreTotal: 66,
        execution: 18,
        damageControl: 21,
        consistency: 17,
        habits: 10,
        confidenceLevel: "Medium",
        computedAt: "",
        explanation: {
          baselineAvgScore: null,
          baselineStdDev: null,
          roundsInHabitWindow: null,
          practiceSessionsInHabitWindow: null,
          reflected: null,
        },
      }),
    [`GET ${root}/rounds/${roundId}/micro-wins`]: () => json(200, []),
    [`GET ${root}/rounds/${roundId}/next-lever`]: () =>
      json(200, {
        roundId,
        userId: golferId,
        leverKey: "penalties",
        title: "Fewer penalties",
        recommendation: "Pick safer targets off the tee.",
        rationale: "Penalties cost the most.",
        confidenceLevel: "Medium",
        createdAt: "",
      }),
    [`GET ${root}/rounds/${roundId}/reflections`]: () =>
      json(200, [
        {
          reflectionId: "f",
          userId: golferId,
          roundId,
          focusRating: 4,
          moodRating: null,
          frustrationSpillover: null,
          whatWorked: "Tempo on the tee",
          whatToRepeat: null,
          oneChangeNext: null,
          createdAt: "2026-11-01T18:00:00Z",
        },
      ]),
    [`GET ${root}/practice-sessions`]: () => paged([session]),
    [`GET ${root}/practice-sessions/${sessionId}`]: () => json(200, session),
    ...overrides,
  };
}

beforeAll(async () => {
  await import("./StudentOverviewPage");
});

describe("a student's rounds (#206)", () => {
  it("opens from the overview, read-only, with the round's score, focus, notes and reflection", async () => {
    const { router, user } = renderSignedIn(`/students/${golferId}`, handlers());

    await user.click(await screen.findByRole("link", { name: "All rounds" }));
    expect(await screen.findByRole("heading", { level: 1, name: "Rounds" })).toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: /Nov 1, 2026/ }));

    expect(router.state.location.pathname).toBe(`/students/${golferId}/rounds/${roundId}`);
    expect(await screen.findByText("Windy back nine")).toBeInTheDocument();
    expect(await screen.findByRole("region", { name: "Their next focus" })).toHaveTextContent("Fewer penalties");
    expect(await screen.findByText("Tempo on the tee")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Edit" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Add .*reflection/ })).not.toBeInTheDocument();

    const breadcrumb = screen.getByRole("navigation", { name: "Breadcrumb" });
    await user.click(within(breadcrumb).getByRole("link", { name: "Sam" }));
    expect(router.state.location.pathname).toBe(`/students/${golferId}`);
  });

  it("says when reflections aren't shared", async () => {
    renderSignedIn(
      `/students/${golferId}/rounds/${roundId}`,
      handlers({ [`GET ${root}/rounds/${roundId}/reflections`]: () => json(404, { status: 404 }) }),
    );

    expect(await screen.findByText("Reflections aren't shared with you.")).toBeInTheDocument();
  });

  it("offers no Log round when the student has none", async () => {
    renderSignedIn(`/students/${golferId}/rounds`, handlers({ [`GET ${root}/rounds`]: () => paged([]) }));

    const empty = (await screen.findByText("No rounds logged yet.")).closest(".empty-state")!;
    expect(within(empty as HTMLElement).queryByRole("link")).not.toBeInTheDocument();
  });

  it("says when they're no longer a student", async () => {
    renderSignedIn(
      `/students/${golferId}/rounds`,
      handlers({ [`GET ${root}/rounds`]: () => json(404, { status: 404 }) }),
    );

    expect(await screen.findByText("You don't coach this golfer any more.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Try again" })).not.toBeInTheDocument();
  });
});

describe("a student's practice (#206)", () => {
  it("opens from the overview, read-only, with the session's notes", async () => {
    const { user } = renderSignedIn(`/students/${golferId}`, handlers());

    await user.click(await screen.findByRole("link", { name: "Practice sessions" }));
    expect(await screen.findByRole("heading", { level: 1, name: "Practice" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Log practice" })).not.toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: /Nov 3, 2026/ }));

    expect(await screen.findByText("Short putts")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Delete/ })).not.toBeInTheDocument();
  });

  it("isn't offered when practice isn't shared, and says so if opened", async () => {
    renderSignedIn(
      `/students/${golferId}/practice`,
      handlers({ [`GET ${root}/practice-sessions`]: () => json(404, { status: 404 }) }),
    );

    expect(await screen.findByText("This golfer doesn't share practice with you.")).toBeInTheDocument();
  });

  it("has no Practice link on the overview when it isn't shared", async () => {
    renderSignedIn(
      `/students/${golferId}`,
      handlers({ [`GET ${root}/overview`]: () => json(200, { ...overview, sharePractice: false }) }),
    );

    await screen.findByRole("heading", { level: 1, name: "Sam" });
    expect(screen.queryByRole("link", { name: "Practice sessions" })).not.toBeInTheDocument();
  });
});

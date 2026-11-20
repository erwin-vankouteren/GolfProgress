import { screen, waitFor, within } from "@testing-library/react";
import { beforeAll, describe, expect, it } from "vitest";
import { localIsoDate } from "../rounds/roundForm";
import { json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";

/** Issues #227, #230 and #235: practice goals on the web, for the golfer and the coach. */

const day = (offset: number) => {
  const date = new Date();
  date.setDate(date.getDate() + offset);
  return localIsoDate(date);
};

const goalId = "00000000-0000-0000-0000-0000000000a1";
const golferId = "00000000-0000-0000-0000-00000000000c";
const sessionId = "00000000-0000-0000-0000-0000000000b1";

const goal = (overrides: Record<string, unknown> = {}) => ({
  practiceGoalId: goalId,
  golferId: userId,
  coachId: "pro",
  coachName: "Pro",
  title: "Lag putts inside 1 metre",
  details: "From 10 metres, 20 balls.",
  focusArea: "Putting",
  sessionType: "Putting",
  targetSessions: 6,
  linkedSessions: 2,
  startsOn: day(-7),
  endsOn: day(21),
  status: "Active",
  createdAt: "",
  updatedAt: null,
  ...overrides,
});

const session = {
  practiceSessionId: sessionId,
  userId,
  practicedAt: day(-2),
  durationMinutes: 30,
  sessionType: "Putting",
  venueName: null,
  tags: [],
  primaryGoal: null,
  notes: null,
  practiceGoalId: goalId,
  createdAt: "",
};

const outcome = (overrides: Record<string, unknown> = {}) => ({
  focusArea: "Putting",
  stat: "PuttsPer18",
  higherIsBetter: false,
  before: 35,
  during: 32,
  change: -3,
  roundsBefore: 5,
  roundsDuring: 4,
  duringFrom: day(-7),
  duringTo: day(35),
  confidence: "High",
  verdict: "Improved",
  ...overrides,
});

const paged = (body: unknown[]) =>
  new Response(JSON.stringify(body), {
    headers: { "Content-Type": "application/json", "X-Total-Count": String(body.length) },
  });

beforeAll(async () => {
  await import("../coaching/StudentOverviewPage");
});

describe("goals from your coach (#230, #235)", () => {
  function renderGolfer(path: string, handlers: Record<string, Handler> = {}) {
    let current = goal();
    const patched: unknown[] = [];
    const result = renderSignedIn(path, {
      "GET /me": () => json(200, { userId, hasStudents: false }),
      "GET /me/practice-sessions": () => paged([session]),
      "GET /me/practice-goals": () => json(200, [current]),
      [`GET /me/practice-goals/${goalId}`]: () => json(200, { goal: current, sessions: [session] }),
      [`GET /me/practice-goals/${goalId}/outcome`]: () => json(200, outcome()),
      [`PATCH /me/practice-goals/${goalId}`]: async (request) => {
        const body = (await request.json()) as { status: string };
        patched.push(body);
        current = goal({ status: body.status });
        return json(200, current);
      },
      ...handlers,
    });
    return { ...result, patched };
  }

  it("shows the goal on Practice, opens it with whether it worked, and marks it done", async () => {
    const { user, patched } = renderGolfer("/practice");

    const goals = await screen.findByRole("region", { name: "Goals from your coach" });
    expect(within(goals).getByText("2 of 6 sessions")).toBeInTheDocument();
    await user.click(within(goals).getByRole("link", { name: /Lag putts inside 1 metre/ }));

    expect(await screen.findByRole("heading", { level: 1, name: "Lag putts inside 1 metre" })).toBeInTheDocument();
    expect(screen.getByText(/From Pro/)).toBeInTheDocument();
    const outcomeCard = await screen.findByRole("region", { name: "Did it work?" });
    expect(
      await within(outcomeCard).findByText("It's working: Putts per 18 holes improved on before the goal."),
    ).toBeInTheDocument();
    expect(within(outcomeCard).getByText("35")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Sessions towards it" })).toHaveTextContent("Putting");

    await user.click(screen.getByRole("button", { name: "Mark done" }));
    await waitFor(() => expect(patched).toEqual([{ status: "Done" }]));
    await waitFor(() => expect(screen.queryByRole("button", { name: "Mark done" })).not.toBeInTheDocument());
  });

  it("says it can't tell yet with too few rounds, and asks before dismissing", async () => {
    const { user, patched } = renderGolfer(`/practice/goals/${goalId}`, {
      [`GET /me/practice-goals/${goalId}/outcome`]: () =>
        json(200, outcome({ during: 30, roundsDuring: 1, confidence: "Low", verdict: "CantTellYet", change: -5 })),
    });

    expect(await screen.findByText(/Can't tell yet/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Dismiss" }));
    const dialog = await screen.findByRole("dialog", { name: "Dismiss this goal?" });
    await user.click(within(dialog).getByRole("button", { name: "Dismiss" }));
    await waitFor(() => expect(patched).toEqual([{ status: "Dismissed" }]));
  });

  it("suggests the goal for a putting session when logging one, and sends it", async () => {
    const posted: Record<string, unknown>[] = [];
    const { user } = renderGolfer("/practice/new", {
      "POST /me/practice-sessions": async (request) => {
        const body = (await request.json()) as Record<string, unknown>;
        posted.push(body);
        return json(201, { ...session, ...body, practiceSessionId: "new" });
      },
      "GET /me/practice-sessions/new": () => json(200, { ...session, practiceSessionId: "new" }),
    });

    const goalField = await screen.findByLabelText("Counts towards");
    expect(goalField).toHaveValue("");
    await user.selectOptions(screen.getByLabelText("Type"), "Putting");
    expect(goalField).toHaveValue(goalId);
    expect(screen.getByText("Suggested because it's for this type of session.")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(posted[0]).toMatchObject({ sessionType: "Putting", practiceGoalId: goalId }));
  });

  it("isn't on Practice without goals", async () => {
    renderGolfer("/practice", { "GET /me/practice-goals": () => json(200, []) });

    await screen.findByRole("heading", { level: 1, name: "Practice" });
    expect(screen.queryByRole("region", { name: "Goals from your coach" })).not.toBeInTheDocument();
  });
});

describe("a coach's practice goals (#227)", () => {
  const root = `/coach/students/${golferId}`;
  const overview = {
    golferId,
    displayName: "Sam",
    shareReflections: true,
    sharePractice: true,
    lastRound: null,
    recentRounds: [],
    monthlyFitnessAvg: null,
    monthlyFitnessChange: null,
    nextFocus: null,
    baseline: null,
    latestReflections: [],
    activeGoals: [] as unknown[],
  };

  it("sets a goal from the overview, after checking it, and lands on it", async () => {
    const posted: Record<string, unknown>[] = [];
    let goals: unknown[] = [];
    const saved = goal({ golferId, linkedSessions: 0, title: "Lag putts" });
    const { user, router } = renderSignedIn(`/students/${golferId}`, {
      "GET /me": () => json(200, { userId, hasStudents: true }),
      [`GET ${root}/overview`]: () => json(200, { ...overview, activeGoals: goals }),
      [`POST ${root}/practice-goals`]: async (request) => {
        posted.push((await request.json()) as Record<string, unknown>);
        goals = [saved];
        return json(200, saved);
      },
      [`GET ${root}/practice-goals/${goalId}`]: () => json(200, { goal: saved, sessions: [] }),
      [`GET ${root}/practice-goals/${goalId}/outcome`]: () =>
        json(200, outcome({ roundsDuring: 0, during: null, change: null, confidence: "Low", verdict: "CantTellYet" })),
    });

    await user.click(await screen.findByRole("link", { name: "Set a practice goal" }));
    expect(await screen.findByRole("heading", { level: 1, name: "Set a practice goal" })).toBeInTheDocument();
    expect(screen.getByText("For Sam")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(screen.getByText("Give the goal a title.")).toBeInTheDocument();
    expect(screen.getByLabelText("Title")).toHaveFocus();
    expect(posted).toEqual([]);

    await user.type(screen.getByLabelText("Title"), "Lag putts");
    await user.selectOptions(screen.getByLabelText(/Focus area/), "Putting");
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/students/${golferId}/practice/goals/${goalId}`));
    expect(posted[0]).toMatchObject({ title: "Lag putts", focusArea: "Putting", sessionType: null, targetSessions: 6 });
    expect(await screen.findByRole("heading", { level: 1, name: "Lag putts" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Change goal" })).toBeInTheDocument();
  });

  it("explains why a goal with sessions can't be removed", async () => {
    const withSessions = goal({ golferId });
    const { user } = renderSignedIn(`/students/${golferId}/practice/goals/${goalId}`, {
      "GET /me": () => json(200, { userId, hasStudents: true }),
      [`GET ${root}/overview`]: () => json(200, overview),
      [`GET ${root}/practice-goals/${goalId}`]: () => json(200, { goal: withSessions, sessions: [session] }),
      [`GET ${root}/practice-goals/${goalId}/outcome`]: () => json(200, outcome()),
      [`DELETE ${root}/practice-goals/${goalId}`]: () => json(409, { status: 409 }),
    });

    await user.click(await screen.findByRole("button", { name: "Remove goal" }));
    const dialog = await screen.findByRole("dialog", { name: "Remove this goal?" });
    await user.click(within(dialog).getByRole("button", { name: "Remove goal" }));

    expect(await within(dialog).findByText(/can't be removed/)).toBeInTheDocument();
    // The coach doesn't get the golfer's buttons.
    expect(screen.queryByRole("button", { name: "Mark done" })).not.toBeInTheDocument();
  });
});

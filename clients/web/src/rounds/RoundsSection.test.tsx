import { screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";

type Round = Record<string, unknown> & { roundId: string; playedAt: string; totalStrokes: number };

const makeRound = (i: number, overrides: Partial<Round> = {}): Round => ({
  roundId: `00000000-0000-0000-0000-${String(i).padStart(12, "0")}`,
  userId,
  playedAt: `2026-05-${String(28 - (i % 28)).padStart(2, "0")}`,
  holes: 18,
  courseName: i === 0 ? "Hilversum" : `Course ${i}`,
  teeName: null,
  totalStrokes: 80 + (i % 15),
  totalPutts: i === 0 ? 33 : null,
  penalties: i === 0 ? 1 : null,
  fairwaysHit: null,
  gir: null,
  threePutts: null,
  blowUpHoles: i === 0 ? 1 : null,
  source: "Manual",
  notes: null,
  createdAt: "",
  updatedAt: "",
  fitnessScore: 60 + i,
  fitnessConfidence: "Medium",
  ...overrides,
});

/**
 * A fake API holding rounds in memory: GET /me/rounds pages them with X-Total-Count, and
 * PUT and DELETE change them, so the list, detail and dashboard can be seen to follow.
 */
function renderRounds(path: string, { count = 3, trackingMode = "Balanced" } = {}) {
  const rounds = Array.from({ length: count }, (_, i) => makeRound(i));
  const puts: Record<string, unknown>[] = [];

  const scoreFor = (round: Round) => ({
    fitnessScoreId: `s-${round.roundId}`,
    userId,
    roundId: round.roundId,
    scoreTotal: round.fitnessScore,
    execution: 18,
    damageControl: 21,
    consistency: 23,
    habits: 10,
    confidenceLevel: "Medium",
    computedAt: "",
    explanation: {
      baselineAvgScore: 91.4,
      baselineStdDev: 3,
      roundsInHabitWindow: 3,
      practiceSessionsInHabitWindow: 1,
      reflected: true,
    },
  });

  const handlers: Record<string, Handler> = {
    "GET /me": () =>
      json(200, {
        userId,
        email: "e@example.test",
        displayName: "E",
        timezone: null,
        units: "Metric",
        trackingMode,
        language: null,
        handicapIndex: null,
        typical18Score: null,
        goalFocus: [],
        createdAt: "",
        updatedAt: "",
      }),
    "GET /me/rounds": (request) => {
      const query = new URL(request.url).searchParams;
      const skip = Number(query.get("skip") ?? 0);
      const take = Number(query.get("take") ?? 20);
      return new Response(JSON.stringify(rounds.slice(skip, skip + take)), {
        headers: { "Content-Type": "application/json", "X-Total-Count": String(rounds.length) },
      });
    },
  };

  // The original ids, so a deleted round's routes answer 404 like the API.
  for (const id of rounds.map((r) => r.roundId)) {
    const find = () => rounds.find((r) => r.roundId === id);
    const orMissing = (answer: (round: Round) => Response) => () => {
      const round = find();
      return round ? answer(round) : json(404, { status: 404 });
    };

    handlers[`GET /rounds/${id}`] = orMissing((round) => json(200, round));
    handlers[`GET /rounds/${id}/fitness-score`] = orMissing((round) => json(200, scoreFor(round)));
    handlers[`GET /rounds/${id}/micro-wins`] = orMissing(() => json(200, []));
    handlers[`GET /rounds/${id}/next-lever`] = orMissing((round) =>
      json(200, {
        roundId: round.roundId,
        userId,
        leverKey: "penalties",
        title: "Reduce penalty strokes",
        recommendation: "Choose the safest target when trouble is possible.",
        rationale: "Penalties are a high-impact lever.",
        confidenceLevel: "Medium",
        createdAt: "",
      }),
    );
    handlers[`GET /rounds/${id}/reflections`] = orMissing(() =>
      json(200, [
        {
          reflectionId: "f1",
          userId,
          roundId: id,
          focusRating: 4,
          moodRating: 3,
          frustrationSpillover: false,
          whatWorked: "Kept the driver in play",
          whatToRepeat: null,
          oneChangeNext: null,
          createdAt: "",
        },
      ]),
    );
    handlers[`PUT /rounds/${id}`] = async (request) => {
      const body = (await request.json()) as Record<string, unknown>;
      puts.push(body);
      const index = rounds.findIndex((r) => r.roundId === id);
      rounds[index] = { ...rounds[index]!, ...body, fitnessScore: 75 } as Round;
      return json(200, rounds[index]);
    };
    handlers[`DELETE /rounds/${id}`] = () => {
      rounds.splice(
        rounds.findIndex((r) => r.roundId === id),
        1,
      );
      return new Response(null, { status: 204 });
    };
  }

  return { ...renderSignedIn(path, handlers), rounds, puts };
}

const firstId = makeRound(0).roundId;

describe("round list (#95)", () => {
  it("shows each round's date, course, strokes and score, newest first", async () => {
    renderRounds("/rounds");

    const rows = await screen.findAllByRole("link", { name: /strokes/ });
    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveTextContent("May 28, 2026");
    expect(rows[0]).toHaveTextContent("Hilversum");
    expect(rows[0]).toHaveTextContent("80 strokes");
    expect(rows[0]).toHaveTextContent("60 score");
    expect(rows[0]).toHaveAttribute("href", `/rounds/${firstId}`);
  });

  it("loads more rounds until it has them all", async () => {
    const { user } = renderRounds("/rounds", { count: 25 });

    expect(await screen.findAllByRole("link", { name: /strokes/ })).toHaveLength(20);
    await user.click(screen.getByRole("button", { name: "Show more rounds" }));

    await waitFor(() => expect(screen.getAllByRole("link", { name: /strokes/ })).toHaveLength(25));
    expect(screen.queryByRole("button", { name: "Show more rounds" })).not.toBeInTheDocument();
  });

  it("invites a user with no rounds to log one", async () => {
    renderRounds("/rounds", { count: 0 });

    expect(await screen.findByText("No rounds logged yet.")).toBeInTheDocument();
    expect(within(screen.getByRole("main")).getByRole("link", { name: "Log round" })).toBeInTheDocument();
  });
});

describe("round detail (#95)", () => {
  it("explains each pillar, and shows the stats, next focus and reflection", async () => {
    const { user } = renderRounds(`/rounds/${firstId}`);

    expect(await screen.findByRole("heading", { level: 1, name: "May 28, 2026" })).toBeInTheDocument();
    const score = await screen.findByRole("region", { name: "Fitness score" });

    // Each pillar is a disclosure: name and number, opening to why.
    const execution = within(score).getByText("Execution").closest("summary")!;
    expect(execution).toHaveTextContent("18 / 25");
    await user.click(execution);
    expect(
      within(score).getByText("80 strokes, 11.4 better than your recent average of 91.4.", { exact: false }),
    ).toBeVisible();
    await user.click(within(score).getByText("Damage control"));
    expect(within(score).getByText(/1 penalty, 1 blow-up hole/)).toBeVisible();

    const stats = screen.getByRole("region", { name: "Stats" });
    expect(within(stats).getByText("Putts").nextSibling).toHaveTextContent("33");

    expect(await screen.findByText("Reduce penalty strokes")).toBeInTheDocument();

    const reflection = await screen.findByRole("region", { name: "Reflection" });
    expect(within(reflection).getByText("4 of 5")).toBeInTheDocument();
    expect(within(reflection).getByText("Kept the driver in play")).toBeInTheDocument();
  });

  it("says when a round no longer exists", async () => {
    renderRounds("/rounds/00000000-0000-0000-0000-999999999999");

    expect(
      await screen.findByText("This round no longer exists. It may have been deleted on another device."),
    ).toBeInTheDocument();
  });
});

describe("editing and deleting (#95)", () => {
  it("edits a round in the prefilled form, then shows it rescored", async () => {
    const { user, puts, router } = renderRounds(`/rounds/${firstId}`);

    await user.click(await screen.findByRole("link", { name: "Edit" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Edit round" })).toBeInTheDocument();
    const strokes = screen.getByLabelText("Strokes");
    expect(strokes).toHaveValue("80");
    expect(screen.getByLabelText("Course")).toHaveValue("Hilversum");

    await user.clear(strokes);
    await user.type(strokes, "78");
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/rounds/${firstId}`));
    expect(puts[0]).toMatchObject({ totalStrokes: 78, courseName: "Hilversum", totalPutts: 33 });
    expect(await screen.findByText("Hilversum · 18 holes · 78 strokes")).toBeInTheDocument();
    expect(await screen.findByText("75")).toBeInTheDocument();
  });

  it("keeps stats the round tracked, even when the tracking mode no longer shows them", async () => {
    const { user, puts } = renderRounds(`/rounds/${firstId}/edit`, { trackingMode: "Minimal" });

    expect(await screen.findByLabelText("Putts")).toHaveValue("33");
    expect(screen.getByLabelText("Blow-up holes")).toHaveValue("1");
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(puts).toHaveLength(1));
    expect(puts[0]).toMatchObject({ totalPutts: 33, penalties: 1, blowUpHoles: 1 });
  });

  it("deletes a round after confirming, and the list no longer shows it", async () => {
    const { user, router, rounds } = renderRounds(`/rounds/${firstId}/edit`);

    await user.click(await screen.findByRole("button", { name: "Delete round" }));
    const dialog = screen.getByRole("dialog", { name: "Delete this round?" });
    // The safe choice has focus.
    expect(within(dialog).getByRole("button", { name: "Cancel" })).toHaveFocus();
    await user.click(within(dialog).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/rounds"));
    expect(rounds).toHaveLength(2);
    await waitFor(() => expect(screen.getAllByRole("link", { name: /strokes/ })).toHaveLength(2));
    expect(screen.queryByText("Hilversum")).not.toBeInTheDocument();
  });

  it("can back out of deleting", async () => {
    const { user, rounds } = renderRounds(`/rounds/${firstId}/edit`);

    await user.click(await screen.findByRole("button", { name: "Delete round" }));
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(rounds).toHaveLength(3);
  });
});

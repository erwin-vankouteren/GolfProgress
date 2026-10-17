import { screen, waitFor, within } from "@testing-library/react";
import { beforeAll, describe, expect, it } from "vitest";
import { json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";

const roundId = (i: number) => `00000000-0000-0000-0000-${String(i).padStart(12, "0")}`;

const point = (i: number, playedAt: string, score: number | null, holes = 18) => ({
  roundId: roundId(i),
  playedAt,
  holes,
  totalStrokes: holes === 18 ? 90 - i : 45,
  totalPutts: null,
  penalties: null,
  threePutts: null,
  blowUpHoles: null,
  fitnessScoreTotal: score,
  fitnessConfidence: score === null ? null : "Medium",
  microWinTitle: null,
});

const month = (monthStart: string, fitnessAvg: number | null, roundsLogged: number, hasReview = false) => ({
  monthStart,
  fitnessAvg,
  fitnessChange: null,
  roundsLogged,
  highlights: [],
  hasReview,
});

const baseline = {
  userId,
  windowRounds: 5,
  sampleSize: 4,
  avgScore: 88.25,
  scoreStdDev: 3.4,
  avgPutts: 33.5,
  avgPenalties: null,
  avgThreePutts: null,
  avgBlowUpHoles: null,
  avgFairwaysHit: null,
  avgGir: null,
  completenessRatio: 0.4,
  confidenceLevel: "Medium",
  computedAt: "2026-10-10T10:00:00Z",
};

type Setup = {
  roundCount?: number;
  timeline?: ReturnType<typeof point>[];
  months?: ReturnType<typeof month>[];
  baseline?: typeof baseline | null;
};

/** A fake API for Progress, keeping generated reviews in memory. */
function renderProgress({
  roundCount = 4,
  timeline = [
    point(1, "2026-08-02", 55),
    point(2, "2026-09-06", 60),
    point(3, "2026-10-04", 64, 9),
    point(4, "2026-10-11", 70),
  ],
  months = [month("2026-08-01", 55, 1), month("2026-09-01", 60, 1), month("2026-10-01", 67, 2)],
  baseline: base = baseline,
}: Setup = {}) {
  const reviews: Record<string, unknown> = {};
  const posted: Record<string, unknown>[] = [];

  const handlers: Record<string, Handler> = {
    "GET /me/rounds": () =>
      new Response(JSON.stringify(timeline.slice(0, 1)), {
        headers: { "Content-Type": "application/json", "X-Total-Count": String(roundCount) },
      }),
    "GET /me/timeline": () => json(200, { userId, count: timeline.length, items: timeline }),
    "GET /me/timeline/monthly": () =>
      json(200, {
        userId,
        months: months.length,
        items: months.map((m) => ({ ...m, hasReview: m.hasReview || m.monthStart in reviews })),
      }),
    "GET /me/baseline": () => (base ? json(200, base) : json(404, { status: 404 })),
    "POST /me/monthly-reviews/generate": async (request) => {
      const body = (await request.json()) as { monthStart: string; focusNextMonth?: string };
      posted.push(body);
      const review = {
        monthlyReviewId: "77777777-7777-7777-7777-777777777777",
        userId,
        monthStart: body.monthStart,
        fitnessAvg: 67,
        fitnessChange: 7,
        focusNextMonth: body.focusNextMonth ?? null,
        highlights: [],
        computedAt: "2026-10-17T09:00:00Z",
      };
      reviews[body.monthStart] = review;
      return json(200, review);
    },
  };
  for (const m of months)
    handlers[`GET /me/monthly-reviews/${m.monthStart}`] = () =>
      reviews[m.monthStart] ? json(200, reviews[m.monthStart]) : json(404, { status: 404 });

  return { ...renderSignedIn("/progress", handlers), posted };
}

// The charts load lazily; loading Recharts once up front keeps that out of every test's time.
beforeAll(async () => {
  await import("./ProgressCharts");
}, 60_000);

describe("Progress (#107)", () => {
  it("explains, before any rounds, and offers Log your first round", async () => {
    renderProgress({ roundCount: 0, timeline: [], months: [], baseline: null });

    expect(await screen.findByText("Log a few rounds to see your trends.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Log your first round" })).toBeInTheDocument();
  });

  it("summarises the rounds over time, with a table of every round", async () => {
    const { user } = renderProgress();

    const timeline = await screen.findByRole("region", { name: "Rounds over time" });
    expect(
      await within(timeline).findByText(
        "4 rounds from Aug 2, 2026 to Oct 11, 2026. Fitness score went from 55 to 70, averaging 62.",
      ),
    ).toBeInTheDocument();
    // The 9-hole round's strokes aren't on the 18-hole line.
    expect(
      within(timeline).getByText("The strokes line shows 18-hole rounds only; the table has every round."),
    ).toBeInTheDocument();

    await user.click(within(timeline).getByRole("button", { name: "Show as table" }));
    const rows = within(within(timeline).getByRole("table")).getAllByRole("row");
    // A header, then newest first.
    expect(rows).toHaveLength(5);
    expect(rows[1]).toHaveTextContent("Oct 11, 2026");
    expect(within(rows[1]!).getByRole("link")).toHaveAttribute("href", `/rounds/${roundId(4)}`);
    expect(rows[2]).toHaveTextContent("9");
    expect(within(timeline).getByRole("button", { name: "Show as chart" })).toBeInTheDocument();
  });

  it("says how many more rounds a trend needs", async () => {
    renderProgress({ roundCount: 1, timeline: [point(1, "2026-10-04", 52)] });

    const timeline = await screen.findByRole("region", { name: "Rounds over time" });
    expect(await within(timeline).findByText("Log 2 more rounds to see a trend.")).toBeInTheDocument();
    // No chart to swap for a table: the table is already there.
    expect(within(timeline).getByRole("table")).toBeInTheDocument();
    expect(within(timeline).queryByRole("button", { name: "Show as table" })).not.toBeInTheDocument();
  });

  it("asks the timeline for the chosen period, and refuses dates the wrong way round", async () => {
    const { user, requests } = renderProgress();
    const timeline = await screen.findByRole("region", { name: "Rounds over time" });
    const timelineQueries = () =>
      requests.filter((r) => new URL(r.url).pathname === "/me/timeline").map((r) => new URL(r.url).searchParams);

    await user.selectOptions(within(timeline).getByLabelText("Period"), "All time");
    await waitFor(() => expect(timelineQueries().at(-1)?.has("from")).toBe(false));

    await user.selectOptions(within(timeline).getByLabelText("Period"), "Choose dates");
    await user.type(within(timeline).getByLabelText("From"), "2026-09-01");
    await waitFor(() => expect(timelineQueries().at(-1)?.get("from")).toBe("2026-09-01"));

    const before = timelineQueries().length;
    await user.type(within(timeline).getByLabelText("To"), "2026-08-01");
    expect(within(timeline).getByRole("alert")).toHaveTextContent("The start date must be on or before the end date.");
    expect(within(timeline).getByLabelText("To")).toHaveAttribute("aria-invalid", "true");
    expect(timelineQueries()).toHaveLength(before);
  });

  it("summarises the monthly trend, with months without scored rounds in the table", async () => {
    const { user } = renderProgress({
      months: [month("2026-08-01", 55, 1), month("2026-09-01", null, 0), month("2026-10-01", 67, 2)],
    });

    const monthly = await screen.findByRole("region", { name: "Monthly trend" });
    expect(
      await within(monthly).findByText(
        "Scored rounds in 2 months. Average fitness score went from 55 in August 2026 to 67 in October 2026.",
      ),
    ).toBeInTheDocument();

    await user.click(within(monthly).getByRole("button", { name: "Show as table" }));
    const rows = within(within(monthly).getByRole("table")).getAllByRole("row");
    expect(rows[2]).toHaveTextContent("September 2026");
    expect(rows[2]).toHaveTextContent("No scored rounds");
  });

  it("shows the baseline per 18 holes, how many rounds it's based on, and its confidence", async () => {
    renderProgress();

    const base = await screen.findByRole("region", { name: "Baseline" });
    expect(await within(base).findByText("88.3")).toBeInTheDocument();
    expect(within(base).getByText("± 3.4")).toBeInTheDocument();
    expect(within(base).getByText("33.5")).toBeInTheDocument();
    expect(within(base).getByText("40%")).toBeInTheDocument();
    // Stats the user doesn't track aren't listed.
    expect(within(base).queryByText("Penalties")).not.toBeInTheDocument();
    expect(within(base).getByText("Based on your last 4 rounds.")).toBeInTheDocument();
    expect(within(base).getByText("Medium")).toBeInTheDocument();
  });

  it("creates the latest month's review with a focus, and says when it's ready", async () => {
    const { user, posted } = renderProgress();

    const review = await screen.findByRole("region", { name: "Monthly review" });
    expect(await within(review).findByText(/no review of October 2026 yet/)).toBeInTheDocument();
    expect(within(review).getByLabelText("Month")).toHaveValue("2026-10-01");

    await user.type(within(review).getByLabelText("Your focus for next month"), "  Fewer three-putts ");
    await user.click(within(review).getByRole("button", { name: "Create review" }));

    expect(await within(review).findByRole("status")).toHaveTextContent("Review ready.");
    expect(posted).toEqual([{ monthStart: "2026-10-01", focusNextMonth: "Fewer three-putts" }]);
    expect(within(review).getByText("Up 7 on the month before")).toBeInTheDocument();
    expect(within(review).getByText("2 rounds played")).toBeInTheDocument();
    expect(within(review).getByText("Fewer three-putts")).toBeInTheDocument();
    expect(within(review).getByRole("button", { name: "Update review" })).toBeInTheDocument();
    await waitFor(() =>
      expect(within(review).getByRole("option", { name: "October 2026 (reviewed)" })).toBeInTheDocument(),
    );
  });

  it("has nothing to review for a month without rounds", async () => {
    const { user } = renderProgress({
      months: [month("2026-09-01", null, 0), month("2026-10-01", 67, 2)],
    });

    const review = await screen.findByRole("region", { name: "Monthly review" });
    await user.selectOptions(await within(review).findByLabelText("Month"), "September 2026");

    expect(
      await within(review).findByText("You didn't log any rounds in September 2026, so there's nothing to review."),
    ).toBeInTheDocument();
    expect(within(review).queryByRole("button", { name: "Create review" })).not.toBeInTheDocument();
  });

  it("keeps the focus within 200 characters before sending", async () => {
    const { user, posted } = renderProgress();

    const review = await screen.findByRole("region", { name: "Monthly review" });
    const focus = await within(review).findByLabelText("Your focus for next month");
    await user.click(focus);
    await user.paste("x".repeat(201));
    await user.click(within(review).getByRole("button", { name: "Create review" }));

    expect(within(review).getByText("Keep it to 200 characters or fewer.")).toBeInTheDocument();
    expect(focus).toHaveAttribute("aria-invalid", "true");
    expect(focus).toHaveFocus();
    expect(posted).toEqual([]);
  });
});

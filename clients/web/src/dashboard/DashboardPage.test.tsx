import { focusManager } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import { IntlProvider } from "react-intl";
import { describe, expect, it } from "vitest";
import { countTo, json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";
import { MonthChange } from "./DashboardPage";

const roundId = "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a";

const dashboard = (overrides: Record<string, unknown> = {}) => ({
  userId,
  lastRound: { roundId, playedAt: "2026-05-03", totalStrokes: 88, fitnessScore: 72, fitnessConfidence: "High" },
  microWins: ["Fewer penalties", "Learning captured", "Consistency win"].map((title, i) => ({
    microWinId: `w${i}`,
    roundId,
    userId,
    category: "StatDelta",
    metric: null,
    direction: "Improved",
    deltaValue: null,
    confidenceLevel: "High",
    title,
    message: "",
    createdAt: "2026-05-03T10:00:00Z",
  })),
  nextLever: {
    roundId,
    userId,
    leverKey: "three_putts",
    title: "Cut down 3-putts",
    recommendation: "On long putts, aim to finish inside a 1–2 meter circle.",
    rationale: "Three-putts are a quick scoring lever.",
    confidenceLevel: "Medium",
    createdAt: "2026-05-03T10:00:00Z",
  },
  baselineAvgScore: 91.4,
  baselineConfidence: "Medium",
  monthlyFitnessAvg: 70,
  monthlyFitnessChange: 4,
  generatedAt: "2026-05-03T10:00:00Z",
  ...overrides,
});

const score = {
  fitnessScoreId: "s",
  userId,
  roundId,
  scoreTotal: 72,
  execution: 18,
  damageControl: 21,
  consistency: 23,
  habits: 10,
  confidenceLevel: "High",
  computedAt: "2026-05-03T10:00:00Z",
};

function renderHome(handlers: Record<string, Handler> = {}) {
  return renderSignedIn("/", {
    "GET /me/dashboard": () => json(200, dashboard()),
    [`GET /rounds/${roundId}/fitness-score`]: () => json(200, score),
    ...handlers,
  });
}

const card = (name: string) => screen.getByRole("region", { name });

describe("Home dashboard (#88)", () => {
  it("shows where the user stands after their last round", async () => {
    renderHome();

    const lastRound = await screen.findByRole("region", { name: "Last round" });
    expect(within(lastRound).getByRole("link", { name: "May 3, 2026 · 88 strokes" })).toBeInTheDocument();
    expect(within(lastRound).getByText("72")).toBeInTheDocument();
    expect(within(lastRound).getByText("High")).toBeInTheDocument();
    // The pillars come from the round's own score.
    expect(await within(lastRound).findByText("Damage control")).toBeInTheDocument();
    expect(within(lastRound).getByText("21 / 25")).toBeInTheDocument();

    expect(within(card("Your next focus")).getByText("Cut down 3-putts")).toBeInTheDocument();
    expect(within(card("Your next focus")).getByText("Three-putts are a quick scoring lever.")).toBeInTheDocument();

    expect(within(card("Baseline")).getByText("91.4")).toBeInTheDocument();
    expect(within(card("Baseline")).getByText("Medium")).toBeInTheDocument();

    expect(within(card("Micro-wins")).getAllByRole("listitem")).toHaveLength(3);

    expect(within(card("This month")).getByText("70")).toBeInTheDocument();
    expect(within(card("This month")).getByText("Up 4 on last month")).toBeInTheDocument();
  });

  it("opens the last round's detail", async () => {
    const { router, user } = renderHome({
      [`GET /rounds/${roundId}`]: () =>
        json(200, {
          roundId,
          userId,
          playedAt: "2026-05-03",
          holes: 18,
          courseName: "Hilversum",
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
        }),
      [`GET /rounds/${roundId}/micro-wins`]: () => json(200, []),
    });

    await user.click(await screen.findByRole("link", { name: /May 3, 2026/ }));

    expect(router.state.location.pathname).toBe(`/rounds/${roundId}`);
    expect(await screen.findByText("Hilversum · 18 holes · 88 strokes")).toBeInTheDocument();
  });

  it("explains a low-confidence score", async () => {
    renderHome({
      "GET /me/dashboard": () =>
        json(
          200,
          dashboard({
            lastRound: {
              roundId,
              playedAt: "2026-05-03",
              totalStrokes: 88,
              fitnessScore: 61,
              fitnessConfidence: "Low",
            },
          }),
        ),
    });

    const lastRound = await screen.findByRole("region", { name: "Last round" });
    expect(within(lastRound).getByText("Low")).toBeInTheDocument();
    expect(within(lastRound).getByText("Based on only a few rounds so far: early days.")).toBeInTheDocument();
  });

  it("invites a user with no rounds to log their first", async () => {
    renderHome({
      "GET /me/dashboard": () =>
        json(
          200,
          dashboard({
            lastRound: null,
            microWins: [],
            nextLever: null,
            baselineAvgScore: null,
            baselineConfidence: null,
            monthlyFitnessAvg: null,
            monthlyFitnessChange: null,
          }),
        ),
    });

    expect(await screen.findByText("Log your first round to see your fitness score.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Log your first round" })).toHaveAttribute("href", "/rounds/new");
    expect(screen.queryByRole("region", { name: "Last round" })).not.toBeInTheDocument();
  });

  it("leaves out cards it has nothing for", async () => {
    renderHome({
      "GET /me/dashboard": () => json(200, dashboard({ monthlyFitnessAvg: null, monthlyFitnessChange: null })),
    });

    await screen.findByRole("region", { name: "Last round" });
    expect(screen.queryByRole("region", { name: "This month" })).not.toBeInTheDocument();
  });

  it("shows skeletons while loading", async () => {
    let release!: () => void;
    const held = new Promise<void>((resolve) => (release = resolve));
    renderHome({
      "GET /me/dashboard": async () => {
        await held;
        return json(200, dashboard());
      },
    });

    expect(await screen.findByRole("status", { name: "Loading…" })).toBeInTheDocument();
    release();
    expect(await screen.findByRole("region", { name: "Last round" })).toBeInTheDocument();
  });

  it("offers Try again when the dashboard can't be loaded", async () => {
    let fail = true;
    const { user } = renderHome({
      "GET /me/dashboard": () => (fail ? json(500, { status: 500 }) : json(200, dashboard())),
    });

    expect(await screen.findByText("Couldn't load your dashboard.", {}, { timeout: 3000 })).toBeInTheDocument();
    fail = false;
    await user.click(screen.getByRole("button", { name: "Try again" }));

    expect(await screen.findByRole("region", { name: "Last round" })).toBeInTheDocument();
  });

  it("refreshes when the window regains focus", async () => {
    const { requests } = renderHome();
    await screen.findByRole("region", { name: "Last round" });
    const before = countTo(requests, "/me/dashboard");

    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    await waitFor(() => expect(countTo(requests, "/me/dashboard")).toBe(before + 1));
    focusManager.setFocused(undefined);
  });
});

describe("the month's change", () => {
  const renderChange = (change: number) =>
    render(
      <IntlProvider locale="en">
        <MonthChange change={change} />
      </IntlProvider>,
    );

  it("says the direction in words, not only the arrow", () => {
    renderChange(-3.4);
    expect(screen.getByText("Down 3 on last month")).toBeInTheDocument();
  });

  it("says when it's the same", () => {
    renderChange(0.3);
    expect(screen.getByText("Same as last month")).toBeInTheDocument();
  });
});

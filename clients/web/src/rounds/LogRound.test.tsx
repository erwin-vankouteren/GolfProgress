import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createApiClient } from "../api";
import { App } from "../app/App";
import { routes } from "../app/routes";
import { Session } from "../session/session";

type Handler = (request: Request) => Response | Promise<Response>;

const roundId = "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a";
const userId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f";

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json" },
  });

const me = (trackingMode: "Minimal" | "Balanced" | "Detailed", typical18Score: number | null = 92) => ({
  userId,
  email: "erwin@example.test",
  displayName: "Erwin",
  timezone: null,
  units: "Metric",
  trackingMode,
  handicapIndex: null,
  typical18Score,
  goalFocus: [],
  createdAt: "2026-05-01T00:00:00Z",
  updatedAt: "2026-05-01T00:00:00Z",
});

const savedRound = (body: Record<string, unknown>) => ({
  roundId,
  userId,
  playedAt: body.playedAt,
  holes: body.holes,
  courseName: body.courseName,
  teeName: body.teeName,
  totalStrokes: body.totalStrokes,
  totalPutts: body.totalPutts,
  penalties: body.penalties,
  fairwaysHit: null,
  gir: null,
  threePutts: null,
  blowUpHoles: null,
  source: "Manual",
  notes: body.notes,
  createdAt: "2026-05-03T10:00:00Z",
  updatedAt: "2026-05-03T10:00:00Z",
});

const score = {
  fitnessScoreId: "b1",
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

/** A fake API: a signed-in user with `trackingMode`, who can log a round that's scored at once. */
function renderLogRound({
  trackingMode = "Balanced" as "Minimal" | "Balanced" | "Detailed",
  handlers = {} as Record<string, Handler>,
} = {}) {
  const posted: Record<string, unknown>[] = [];
  let lastRound: Record<string, unknown> = {};
  const all: Record<string, Handler> = {
    "GET /client-config": () => json(200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "1.0.0" } }),
    "GET /me": () => json(200, me(trackingMode)),
    "POST /me/rounds": async (request) => {
      const body = (await request.json()) as Record<string, unknown>;
      posted.push(body);
      lastRound = savedRound(body);
      return json(201, lastRound);
    },
    [`GET /rounds/${roundId}`]: () => json(200, lastRound),
    [`GET /rounds/${roundId}/fitness-score`]: () => json(200, score),
    [`GET /rounds/${roundId}/micro-wins`]: () =>
      json(200, [
        {
          microWinId: "w1",
          roundId,
          userId,
          category: "StatDelta",
          metric: "penalties",
          direction: "Improved",
          deltaValue: -1,
          confidenceLevel: "High",
          title: "Fewer penalties",
          message: "You had fewer penalty strokes than your recent average.",
          createdAt: "2026-05-03T10:00:00Z",
        },
      ]),
    ...handlers,
  };

  const api = createApiClient({
    baseUrl: "https://api.example.test",
    tokens: { accessToken: () => "token", refreshAccessToken: async () => null },
    fetch: vi.fn(async (request: Request) => {
      const handler = all[`${request.method} ${new URL(request.url).pathname}`];
      return handler ? handler(request) : json(404, { status: 404 });
    }),
  });
  const session = new Session({ accessToken: "token", userId });
  session.attach(api);
  const router = createMemoryRouter(routes, { initialEntries: ["/rounds/new"] });

  render(
    <App api={api} session={session} appVersion="1.0.0">
      <RouterProvider router={router} />
    </App>,
  );
  return { router, posted, user: userEvent.setup() };
}

afterEach(() => vi.unstubAllGlobals());

describe("Log round (#92)", () => {
  it("logs a round and opens it with its new score", async () => {
    const { router, posted, user } = renderLogRound();

    await user.type(await screen.findByLabelText("Course"), "Hilversum");
    await user.type(screen.getByLabelText("Strokes"), "88");
    await user.type(screen.getByLabelText("Putts"), "33");
    await user.type(screen.getByLabelText("Penalties"), "1");
    // Enter submits, from any field.
    await user.keyboard("{Enter}");

    expect(await screen.findByText("72")).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/rounds/${roundId}`);
    expect(screen.getByText("Hilversum · 18 holes · 88 strokes")).toBeInTheDocument();
    expect(screen.getByText("High")).toBeInTheDocument();
    expect(screen.getByText("Execution")).toBeInTheDocument();
    expect(screen.getByText("18 / 25")).toBeInTheDocument();
    expect(screen.getByText("Fewer penalties")).toBeInTheDocument();

    expect(posted).toHaveLength(1);
    expect(posted[0]).toMatchObject({
      holes: 18,
      courseName: "Hilversum",
      totalStrokes: 88,
      totalPutts: 33,
      penalties: 1,
      fairwaysHit: null,
    });
  });

  it("shows only the stats for the tracking mode", async () => {
    renderLogRound({ trackingMode: "Minimal" });

    expect(await screen.findByLabelText("Strokes")).toBeInTheDocument();
    expect(screen.queryByLabelText("Putts")).not.toBeInTheDocument();
    expect(screen.getByText("More stats? Change your tracking mode in Profile.")).toBeInTheDocument();
  });

  it("shows every stat in Detailed, with the hole count as their limit", async () => {
    const { user } = renderLogRound({ trackingMode: "Detailed" });

    const fairways = await screen.findByLabelText("Fairways hit");
    expect(screen.getByLabelText("Greens in regulation")).toBeInTheDocument();
    expect(screen.getByLabelText("Blow-up holes")).toBeInTheDocument();

    await user.click(screen.getByLabelText("9 holes"));
    await user.type(fairways, "10");

    expect(screen.getByText("Can't be more than 9, the number of holes.")).toBeInTheDocument();
    expect(fairways).toHaveAttribute("aria-invalid", "true");
    expect(fairways).toHaveAccessibleDescription(/Can't be more than 9/);
  });

  it("checks the API's rules as the user types", async () => {
    const { user } = renderLogRound();

    await user.type(await screen.findByLabelText("Strokes"), "30");
    await user.type(screen.getByLabelText("Putts"), "31");

    expect(screen.getByText("Can't be more than your strokes (30).")).toBeInTheDocument();
  });

  it("on Save, shows what's missing and moves focus to it, without sending", async () => {
    const { posted, user } = renderLogRound();

    await user.click(await screen.findByRole("button", { name: "Save" }));

    expect(screen.getByText("Enter your strokes.")).toBeInTheDocument();
    expect(screen.getByLabelText("Strokes")).toHaveFocus();
    expect(posted).toHaveLength(0);
  });

  it("steps from the typical score", async () => {
    const { user } = renderLogRound();

    await user.click(await screen.findByRole("button", { name: "Increase Strokes" }));
    expect(screen.getByLabelText("Strokes")).toHaveValue("92");

    await user.click(screen.getByRole("button", { name: "Decrease Strokes" }));
    expect(screen.getByLabelText("Strokes")).toHaveValue("91");

    // The arrow keys step too.
    await user.type(screen.getByLabelText("Strokes"), "{ArrowUp}{ArrowUp}");
    expect(screen.getByLabelText("Strokes")).toHaveValue("93");
  });

  it("shows the API's validation errors next to their fields", async () => {
    const { user } = renderLogRound({
      handlers: {
        "POST /me/rounds": () =>
          json(400, { status: 400, errors: { PlayedAt: ["PlayedAt must be an ISO date, yyyy-MM-dd."] } }),
      },
    });

    await user.type(await screen.findByLabelText("Strokes"), "88");
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("PlayedAt must be an ISO date, yyyy-MM-dd.")).toBeInTheDocument();
    expect(screen.getByLabelText("Date")).toHaveAttribute("aria-invalid", "true");
  });

  it("sends a round once, however often Save is pressed", async () => {
    let release!: () => void;
    const held = new Promise<void>((resolve) => (release = resolve));
    const { posted, user } = renderLogRound({
      handlers: {
        "POST /me/rounds": async (request) => {
          posted.push((await request.json()) as Record<string, unknown>);
          await held;
          return json(500, { status: 500 });
        },
      },
    });

    await user.type(await screen.findByLabelText("Strokes"), "88");
    const save = screen.getByRole("button", { name: "Save" });
    await user.click(save);
    await user.keyboard("{Enter}");
    await user.click(save);

    expect(await screen.findByRole("button", { name: "Saving…" })).toBeDisabled();
    release();
    expect(await screen.findByText("Couldn't save the round. Please try again.")).toBeInTheDocument();
    expect(posted).toHaveLength(1);
  });

  it("can't save while offline", async () => {
    vi.stubGlobal("navigator", { ...navigator, onLine: false });
    renderLogRound();

    expect(await screen.findByRole("button", { name: "Save" })).toBeDisabled();
    expect(screen.getByText("You're offline. Connect to save this round.")).toBeInTheDocument();
    expect(
      within(screen.getByRole("main")).getByText("You're offline. You can look around, but changes need a connection."),
    ).toBeInTheDocument();
  });

  it("offers Try again when the preferences can't be loaded", async () => {
    let fail = true;
    const { user } = renderLogRound({
      handlers: { "GET /me": () => (fail ? json(500, { status: 500 }) : json(200, me("Balanced"))) },
    });

    expect(await screen.findByText("Couldn't load your preferences.", {}, { timeout: 3000 })).toBeInTheDocument();
    fail = false;
    await user.click(screen.getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(screen.getByLabelText("Strokes")).toBeInTheDocument());
  });
});

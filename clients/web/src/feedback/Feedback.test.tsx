import { screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";

/** Issues #214 and #216: feedback threads, for the coach and for the golfer, on the web. */

const golferId = "00000000-0000-0000-0000-00000000000c";
const coachId = "00000000-0000-0000-0000-00000000000d";
const roundId = "00000000-0000-0000-0000-000000000001";

const me = (id: string, hasStudents: boolean) => ({
  userId: id,
  email: "x@example.test",
  displayName: "X",
  timezone: null,
  units: "Metric",
  trackingMode: "Minimal",
  language: null,
  handicapIndex: null,
  typical18Score: null,
  goalFocus: [],
  createdAt: "",
  updatedAt: "",
  hasStudents,
});

const round = (owner: string) => ({
  roundId,
  userId: owner,
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
});

type Message = Record<string, unknown> & { feedbackMessageId: string; text: string };

/**
 * A fake feedback API holding messages in memory, answering as [callerId]: POST adds one
 * written by the caller, PATCH and DELETE change it, /feedback/read is recorded.
 */
function feedbackApi(callerId: string, initial: Message[] = [], callerIsGolfer = false) {
  const messages = [...initial];
  const posted: Record<string, unknown>[] = [];
  const reads: Record<string, unknown>[] = [];
  let next = 0;

  const handlers: Record<string, Handler> = {
    "GET /feedback": () => json(200, messages),
    "POST /feedback": async (request) => {
      const body = (await request.json()) as Record<string, unknown>;
      posted.push(body);
      const message: Message = {
        feedbackMessageId: `m${next++}`,
        golferId,
        coachId: callerIsGolfer ? body.coachId : callerId,
        coachName: "Pro",
        fromCoach: !callerIsGolfer,
        mine: true,
        targetType: body.targetType,
        targetId: body.targetId,
        text: body.text as string,
        createdAt: `2026-11-0${2 + next}T10:00:00Z`,
        editedAt: null,
      };
      messages.push(message);
      return json(200, message);
    },
    "POST /feedback/read": async (request) => {
      reads.push((await request.json()) as Record<string, unknown>);
      return new Response(null, { status: 204 });
    },
  };
  for (const id of ["m0", "m1", "s0", "s1"]) {
    handlers[`PATCH /feedback/${id}`] = async (request) => {
      const found = messages.find((m) => m.feedbackMessageId === id)!;
      Object.assign(found, {
        text: ((await request.json()) as { text: string }).text,
        editedAt: "2026-11-09T10:00:00Z",
      });
      return json(200, found);
    };
    handlers[`DELETE /feedback/${id}`] = () => {
      messages.splice(
        messages.findIndex((m) => m.feedbackMessageId === id),
        1,
      );
      return new Response(null, { status: 204 });
    };
  }
  return { handlers, posted, reads };
}

const fromGolfer = (id: string, text: string, createdAt = "2026-11-02T09:00:00Z"): Message => ({
  feedbackMessageId: id,
  golferId,
  coachId,
  coachName: "Pro",
  fromCoach: false,
  mine: false,
  targetType: "Round",
  targetId: roundId,
  text,
  createdAt,
  editedAt: null,
});

const root = `/coach/students/${golferId}`;

function renderAsCoach(initial: Message[] = [], overrides: Record<string, Handler> = {}) {
  const api = feedbackApi(coachId, initial);
  const result = renderSignedIn(`/students/${golferId}/rounds/${roundId}`, {
    "GET /me": () => json(200, me(coachId, true)),
    [`GET ${root}/overview`]: () =>
      json(200, {
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
      }),
    [`GET ${root}/rounds`]: () => json(200, [round(golferId)]),
    [`GET ${root}/rounds/${roundId}`]: () => json(200, round(golferId)),
    [`GET ${root}/rounds/${roundId}/micro-wins`]: () => json(200, []),
    [`GET ${root}/rounds/${roundId}/reflections`]: () => json(200, []),
    ...api.handlers,
    ...overrides,
  });
  return { ...result, ...api };
}

const feedbackCard = (name = "Feedback") => screen.findByRole("region", { name });

beforeAll(async () => {
  await import("../coaching/StudentOverviewPage");
});

afterEach(() => vi.unstubAllGlobals());

describe("a coach's feedback on a student's round (#214)", () => {
  it("writes feedback, shows the student's reply, and marks it read", async () => {
    const { user, posted, reads } = renderAsCoach([fromGolfer("s0", "Can we look at my putting?")]);
    const card = await feedbackCard();

    expect(await within(card).findByText("Can we look at my putting?")).toBeInTheDocument();
    expect(within(card).getByText("Sam")).toBeInTheDocument();
    await waitFor(() => expect(reads).toEqual([{ golferId, upTo: "2026-11-02T09:00:00Z" }]));

    await user.type(within(card).getByLabelText("Write feedback"), "  Lag putts to a metre.  ");
    await user.click(within(card).getByRole("button", { name: "Send" }));

    expect(await within(card).findByText("Lag putts to a metre.")).toBeInTheDocument();
    expect(posted).toEqual([
      { golferId, targetType: "Round", targetId: roundId, coachId: null, text: "Lag putts to a metre." },
    ]);
    expect(within(card).getByText("You")).toBeInTheDocument();
    expect(within(card).getByLabelText("Write feedback")).toHaveValue("");
  });

  it("edits and deletes their own message", async () => {
    const { user } = renderAsCoach();
    const card = await feedbackCard();
    expect(await within(card).findByText("No feedback yet. What did you notice?")).toBeInTheDocument();
    await user.type(within(card).getByLabelText("Write feedback"), "First try");
    await user.click(within(card).getByRole("button", { name: "Send" }));
    await within(card).findByText("First try");

    await user.click(within(card).getByRole("button", { name: /^Edit your message of/ }));
    const editor = within(card).getByLabelText("Edit your message");
    await user.clear(editor);
    await user.type(editor, "Second try");
    await user.click(within(card).getByRole("button", { name: "Save" }));
    expect(await within(card).findByText("Second try")).toBeInTheDocument();
    expect(within(card).getByText(/Edited/)).toBeInTheDocument();

    await user.click(within(card).getByRole("button", { name: /^Delete your message of/ }));
    const dialog = await screen.findByRole("dialog", { name: "Delete this message?" });
    await user.click(within(dialog).getByRole("button", { name: "Delete" }));
    expect(await within(card).findByText("No feedback yet. What did you notice?")).toBeInTheDocument();
  });

  it("asks for text, says when there are too many messages, and offers Try again when it can't load", async () => {
    let fail = true;
    const { user } = renderAsCoach([], {
      "GET /feedback": () => (fail ? json(500, { status: 500 }) : json(200, [])),
      "POST /feedback": () => json(429, { status: 429 }),
    });
    const card = await feedbackCard();

    const tryAgain = await within(card).findByRole("button", { name: "Try again" }, { timeout: 3000 });
    fail = false;
    await user.click(tryAgain);

    await user.click(await within(card).findByRole("button", { name: "Send" }));
    expect(within(card).getByRole("alert")).toHaveTextContent("Write something first.");
    await user.type(within(card).getByLabelText("Write feedback"), "Hello");
    await user.click(within(card).getByRole("button", { name: "Send" }));
    expect(await within(card).findByRole("alert")).toHaveTextContent("Too many messages this hour.");
  });

  it("can't send while offline", async () => {
    vi.stubGlobal("navigator", { ...navigator, onLine: false });
    renderAsCoach();
    const card = await feedbackCard();

    expect(await within(card).findByRole("button", { name: "Send" })).toBeDisabled();
    expect(within(card).getByText("You're offline. Connect to send a message.")).toBeInTheDocument();
  });
});

const coach = (id: string, name: string, overrides: Record<string, unknown> = {}) => ({
  coachLinkId: `link-${id}`,
  coachId: id,
  displayName: name,
  shareReflections: true,
  sharePractice: true,
  createdAt: "",
  ...overrides,
});

const fromCoach = (id: string, text: string, from = coachId, name: string | null = "Pro"): Message => ({
  ...fromGolfer(id, text),
  coachId: from,
  coachName: name,
  fromCoach: true,
});

function renderAsGolfer(coaches: unknown[], initial: Message[] = []) {
  // Signed in as the test user: the golfer whose round it is.
  const api = feedbackApi(userId, initial, true);
  const result = renderSignedIn(`/rounds/${roundId}`, {
    "GET /me": () => json(200, me(userId, false)),
    "GET /me/rounds": () => json(200, [round(userId)]),
    [`GET /rounds/${roundId}`]: () => json(200, round(userId)),
    [`GET /rounds/${roundId}/micro-wins`]: () => json(200, []),
    [`GET /rounds/${roundId}/reflections`]: () => json(200, []),
    "GET /me/coaches": () => json(200, { coaches, invitations: [] }),
    ...api.handlers,
  });
  return { ...result, ...api };
}

describe("feedback from your coach (#216)", () => {
  it("shows the coach's feedback, replies to them, and marks it read", async () => {
    const { user, posted, reads } = renderAsGolfer([coach(coachId, "Pro")], [fromCoach("s0", "Nice tempo.")]);
    const card = await feedbackCard("Feedback from your coach");

    expect(await within(card).findByText("Nice tempo.")).toBeInTheDocument();
    expect(within(card).getByText("Pro")).toBeInTheDocument();
    await waitFor(() => expect(reads).toEqual([{ golferId: userId, upTo: "2026-11-02T09:00:00Z" }]));

    await user.type(within(card).getByLabelText("Message to Pro"), "Thanks!");
    await user.click(within(card).getByRole("button", { name: "Send" }));

    expect(await within(card).findByText("Thanks!")).toBeInTheDocument();
    expect(posted).toEqual([{ golferId: userId, targetType: "Round", targetId: roundId, coachId, text: "Thanks!" }]);
  });

  it("shows a thread per coach, and a former coach's read-only", async () => {
    const otherId = "00000000-0000-0000-0000-00000000000e";
    renderAsGolfer(
      [coach(coachId, "Pro"), coach(otherId, "Kim")],
      [fromCoach("s0", "From Pro"), fromCoach("s1", "From the past", "gone", null)],
    );
    const card = await feedbackCard("Feedback from your coach");

    expect(await within(card).findByRole("heading", { name: "Pro" })).toBeInTheDocument();
    expect(within(card).getByRole("heading", { name: "Kim" })).toBeInTheDocument();
    expect(within(card).getByText("No messages yet. You can ask Kim about this.")).toBeInTheDocument();
    expect(within(card).getAllByRole("heading", { name: "A former coach" })).toHaveLength(1);
    expect(within(card).getByText("From the past")).toBeInTheDocument();
    // Pro and Kim can be written to; the former coach can't.
    expect(within(card).getAllByRole("button", { name: "Send" })).toHaveLength(2);
  });

  it("isn't shown without a coach or anything from one", async () => {
    renderAsGolfer([]);

    await screen.findByRole("region", { name: "Reflection" });
    expect(screen.queryByRole("region", { name: "Feedback from your coach" })).not.toBeInTheDocument();
  });
});

import { screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";

type Session = Record<string, unknown> & { practiceSessionId: string };

const session = (i: number, overrides: Partial<Session> = {}): Session => ({
  practiceSessionId: `00000000-0000-0000-0000-${String(i).padStart(12, "0")}`,
  userId,
  practicedAt: `2026-05-${String(20 - i).padStart(2, "0")}`,
  durationMinutes: 45,
  sessionType: "Putting",
  venueName: null,
  tags: [],
  primaryGoal: null,
  notes: null,
  createdAt: "",
  ...overrides,
});

/** A fake API holding practice sessions in memory, with create, list, get and delete. */
function renderPractice(path: string, initial: Session[] = []) {
  const sessions = [...initial];
  const posted: Record<string, unknown>[] = [];
  const newId = "99999999-9999-9999-9999-999999999999";

  const handlers: Record<string, Handler> = {
    "GET /me/practice-sessions": (request) => {
      const query = new URL(request.url).searchParams;
      const skip = Number(query.get("skip") ?? 0);
      const take = Number(query.get("take") ?? 20);
      return new Response(JSON.stringify(sessions.slice(skip, skip + take)), {
        headers: { "Content-Type": "application/json", "X-Total-Count": String(sessions.length) },
      });
    },
    "POST /me/practice-sessions": async (request) => {
      const body = (await request.json()) as Record<string, unknown>;
      posted.push(body);
      const saved = { ...body, practiceSessionId: newId, userId, createdAt: "" } as Session;
      sessions.unshift(saved);
      return json(201, saved);
    },
  };

  for (const id of [...initial.map((s) => s.practiceSessionId), newId]) {
    const find = () => sessions.find((s) => s.practiceSessionId === id);
    handlers[`GET /me/practice-sessions/${id}`] = () => {
      const found = find();
      return found ? json(200, found) : json(404, { status: 404 });
    };
    handlers[`DELETE /me/practice-sessions/${id}`] = () => {
      const found = find();
      if (!found) return json(404, { status: 404 });
      sessions.splice(sessions.indexOf(found), 1);
      return new Response(null, { status: 204 });
    };
  }

  return { ...renderSignedIn(path, handlers), sessions, posted };
}

describe("practice list (#103)", () => {
  it("shows date, type and duration, newest first, and that practice counts towards Habits", async () => {
    renderPractice("/practice", [
      session(0, { sessionType: "ShortGame", durationMinutes: 60 }),
      session(1, { sessionType: "Fitness", durationMinutes: null }),
    ]);

    const rows = await screen.findAllByRole("link", { name: /2026/ });
    expect(rows[0]).toHaveTextContent("May 20, 2026");
    expect(rows[0]).toHaveTextContent("Short game");
    expect(rows[0]).toHaveTextContent("60 min");
    // A type this build doesn't know (#134) reads as "Practice".
    expect(rows[1]).toHaveTextContent("Practice");
    expect(screen.getByText("Practice counts towards your Habits score.")).toBeInTheDocument();
  });

  it("explains, when empty, and offers Log practice", async () => {
    renderPractice("/practice");

    expect(
      await screen.findByText("No practice logged yet. Practice counts towards your Habits score."),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Log practice" })).toHaveAttribute("href", "/practice/new");
  });
});

describe("Log practice (#103)", () => {
  it("logs a session with tags, and opens it", async () => {
    const { user, posted, router } = renderPractice("/practice/new");

    await user.selectOptions(await screen.findByLabelText("Type"), "Short game");
    await user.type(screen.getByLabelText("Duration"), "45");
    await user.type(screen.getByLabelText("Venue"), "Hilversum");

    // Tags as chips: Enter and comma both add one.
    const tags = screen.getByLabelText("Tags");
    await user.type(tags, "lag putting{Enter}");
    await user.type(tags, "chipping,");
    expect(screen.getByRole("button", { name: "Remove tag lag putting" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Remove tag chipping" }));
    // A tag still being typed when saving counts too.
    await user.type(tags, "bunker");

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/practice\/9{8}-/));
    expect(posted[0]).toMatchObject({
      sessionType: "ShortGame",
      durationMinutes: 45,
      venueName: "Hilversum",
      tags: ["lag putting", "bunker"],
    });
    expect(await screen.findByRole("heading", { level: 1, name: "Short game" })).toBeInTheDocument();
    expect(screen.getByText("45 min")).toBeInTheDocument();
    expect(screen.getByText("bunker")).toBeInTheDocument();
  });

  it("checks the API's rules before sending", async () => {
    const { user, posted } = renderPractice("/practice/new");

    await user.type(await screen.findByLabelText("Duration"), "2000");
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(screen.getByText("Enter between 1 and 1,440 minutes.")).toBeInTheDocument();
    expect(screen.getByLabelText("Duration")).toHaveFocus();
    expect(posted).toHaveLength(0);
  });

  it("refuses a tag twice", async () => {
    const { user } = renderPractice("/practice/new");

    const tags = await screen.findByLabelText("Tags");
    await user.type(tags, "Range{Enter}range{Enter}");

    expect(screen.getByRole("alert")).toHaveTextContent("That tag is already there.");
    expect(screen.getAllByRole("button", { name: /^Remove tag/ })).toHaveLength(1);
  });
});

describe("practice session detail (#103)", () => {
  it("shows every field, and deletes after confirming", async () => {
    const one = session(0, {
      venueName: "Hilversum",
      primaryGoal: "Lag putts inside a metre",
      tags: ["lag putting"],
      notes: "Windy",
    });
    const { user, router, sessions } = renderPractice(`/practice/${one.practiceSessionId}`, [one, session(1)]);

    expect(await screen.findByRole("heading", { level: 1, name: "Putting" })).toBeInTheDocument();
    for (const text of ["45 min", "Hilversum", "Lag putts inside a metre", "lag putting", "Windy"]) {
      expect(screen.getByText(text)).toBeInTheDocument();
    }

    await user.click(screen.getByRole("button", { name: "Delete session" }));
    const dialog = screen.getByRole("dialog", { name: "Delete this practice session?" });
    await user.click(within(dialog).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/practice"));
    expect(sessions).toHaveLength(1);
    await waitFor(() => expect(screen.getAllByRole("link", { name: /2026/ })).toHaveLength(1));
  });

  it("says when a session no longer exists", async () => {
    renderPractice("/practice/00000000-0000-0000-0000-000000000000");

    expect(
      await screen.findByText("This session no longer exists. It may have been deleted on another device."),
    ).toBeInTheDocument();
  });
});

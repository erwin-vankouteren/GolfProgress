import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { createApiClient } from "../api";
import { App } from "../app/App";
import { routes } from "../app/routes";
import { Session } from "../session/session";
import { json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";

const me = {
  userId,
  email: "sam@example.test",
  displayName: "Sam",
  timezone: null,
  units: "Metric",
  trackingMode: "Minimal",
  language: null,
  handicapIndex: null,
  typical18Score: null,
  goalFocus: [],
  createdAt: "2026-05-01T10:00:00Z",
  updatedAt: "2026-05-01T10:00:00Z",
};

const pending = {
  coachInvitationId: "00000000-0000-0000-0000-00000000000a",
  email: "pro@example.test",
  shareReflections: false,
  sharePractice: true,
  createdAt: "2026-11-01T10:00:00Z",
  expiresAt: "2026-11-08T10:00:00Z",
  resent: false,
};

const noContent = () => new Response(null, { status: 204 });

// Both pages load lazily: loaded here, a test doesn't spend its wait on the import.
beforeAll(async () => {
  await Promise.all([import("../profile/ProfilePage"), import("./CoachInvitationPage"), import("./StudentsPage")]);
});
const coachesRegion = () => screen.findByRole("region", { name: "Coaches" });

describe("Profile → Coaches (#185)", () => {
  it("invites a coach with what they chose to share, and lists the invitation", async () => {
    const invitations: (typeof pending)[] = [];
    const bodies: unknown[] = [];
    const { user } = renderSignedIn("/profile", {
      "GET /me": () => json(200, me),
      "GET /me/coaches": () => json(200, { coaches: [], invitations }),
      "POST /me/coach-invitations": async (request) => {
        bodies.push(await request.json());
        invitations.push(pending);
        return json(200, pending);
      },
    });
    const coaches = await coachesRegion();

    expect(await within(coaches).findByText(/No coaches yet/)).toBeInTheDocument();
    const rounds = within(coaches).getByRole("checkbox", { name: /Rounds and progress/ });
    expect(rounds).toBeChecked();
    expect(rounds).toBeDisabled();

    await user.type(within(coaches).getByLabelText("Your coach's email address"), " pro@example.test ");
    await user.click(within(coaches).getByRole("checkbox", { name: /Reflections and notes/ }));
    await user.click(within(coaches).getByRole("button", { name: "Send invitation" }));

    expect(await within(coaches).findByRole("status")).toHaveTextContent("Invitation sent to pro@example.test.");
    expect(bodies).toEqual([{ email: "pro@example.test", shareReflections: false, sharePractice: true }]);
    expect(await within(coaches).findByText("Sees: Rounds and progress and Practice")).toBeInTheDocument();
    expect(within(coaches).getByLabelText("Your coach's email address")).toHaveValue("");
  });

  it("checks the address, and shows the API's refusal next to it", async () => {
    const { user, requests } = renderSignedIn("/profile", {
      "GET /me": () => json(200, me),
      "GET /me/coaches": () => json(200, { coaches: [], invitations: [] }),
      "POST /me/coach-invitations": () =>
        json(400, { status: 400, errors: { Email: ["This person is already your coach."] } }),
    });
    const coaches = await coachesRegion();
    const email = within(coaches).getByLabelText("Your coach's email address");

    await user.click(within(coaches).getByRole("button", { name: "Send invitation" }));
    expect(await within(coaches).findByText("Your coach's email address is required.")).toBeInTheDocument();
    expect(email).toHaveFocus();
    expect(requests.some((r) => r.method === "POST")).toBe(false);

    await user.type(email, "pro@example.test");
    await user.click(within(coaches).getByRole("button", { name: "Send invitation" }));
    expect(await within(coaches).findByText("This person is already your coach.")).toBeInTheDocument();
    expect(email).toHaveAttribute("aria-invalid", "true");
  });

  it("says so when today's invitations have run out", async () => {
    const { user } = renderSignedIn("/profile", {
      "GET /me": () => json(200, me),
      "GET /me/coaches": () => json(200, { coaches: [], invitations: [] }),
      "POST /me/coach-invitations": () => json(429, { status: 429 }),
    });
    const coaches = await coachesRegion();

    await user.type(within(coaches).getByLabelText("Your coach's email address"), "pro@example.test");
    await user.click(within(coaches).getByRole("button", { name: "Send invitation" }));

    expect(await within(coaches).findByRole("alert")).toHaveTextContent("Too many invitations today.");
  });

  it("shows active coaches, and cancels a pending invitation", async () => {
    let invitations = [pending];
    const { user } = renderSignedIn("/profile", {
      "GET /me": () => json(200, me),
      "GET /me/coaches": () =>
        json(200, {
          coaches: [
            {
              coachLinkId: "00000000-0000-0000-0000-00000000000b",
              displayName: "Mark",
              shareReflections: true,
              sharePractice: true,
              createdAt: "2026-10-01T10:00:00Z",
            },
          ],
          invitations,
        }),
      [`DELETE /me/coach-invitations/${pending.coachInvitationId}`]: () => {
        invitations = [];
        return noContent();
      },
    });
    const coaches = await coachesRegion();

    expect(await within(coaches).findByText("Mark")).toBeInTheDocument();
    expect(within(coaches).getByText("Always sees your rounds and progress.")).toBeVisible();

    await user.click(within(coaches).getByRole("button", { name: "Cancel the invitation to pro@example.test" }));

    await waitFor(() => expect(within(coaches).queryByText("pro@example.test")).not.toBeInTheDocument());
    expect(within(coaches).getByText("Mark")).toBeInTheDocument();
  });

  const coach = {
    coachLinkId: "00000000-0000-0000-0000-00000000000b",
    displayName: "Mark",
    shareReflections: true,
    sharePractice: true,
    createdAt: "2026-10-01T10:00:00Z",
  };

  it("changes what a coach sees as it's switched", async () => {
    let current = { ...coach };
    const bodies: unknown[] = [];
    const { user } = renderSignedIn("/profile", {
      "GET /me": () => json(200, me),
      "GET /me/coaches": () => json(200, { coaches: [current], invitations: [] }),
      [`PATCH /me/coaches/${coach.coachLinkId}`]: async (request) => {
        const body = (await request.json()) as Partial<typeof coach>;
        bodies.push(body);
        current = { ...current, ...body };
        return json(200, current);
      },
    });
    const mark = await screen.findByRole("group", { name: "Mark" });

    await user.click(within(mark).getByRole("checkbox", { name: /Practice/ }));

    await waitFor(() => expect(within(mark).getByRole("checkbox", { name: /Practice/ })).not.toBeChecked());
    expect(bodies).toEqual([{ shareReflections: true, sharePractice: false }]);
    await waitFor(() => expect(within(mark).getByRole("checkbox", { name: /Practice/ })).toBeEnabled());
    expect(within(mark).getByRole("checkbox", { name: /Reflections and notes/ })).toBeChecked();
  });

  it("goes back and says so when the change fails", async () => {
    const { user } = renderSignedIn("/profile", {
      "GET /me": () => json(200, me),
      "GET /me/coaches": () => json(200, { coaches: [coach], invitations: [] }),
      [`PATCH /me/coaches/${coach.coachLinkId}`]: () => json(500, { status: 500 }),
    });
    const mark = await screen.findByRole("group", { name: "Mark" });

    await user.click(within(mark).getByRole("checkbox", { name: /Practice/ }));

    expect(await screen.findByText(/Couldn't change what your coach sees/)).toBeInTheDocument();
    expect(within(mark).getByRole("checkbox", { name: /Practice/ })).toBeChecked();
  });

  it("removes a coach only after saying what that means", async () => {
    let coaches = [coach];
    let deletes = 0;
    const { user } = renderSignedIn("/profile", {
      "GET /me": () => json(200, me),
      "GET /me/coaches": () => json(200, { coaches, invitations: [] }),
      [`DELETE /me/coaches/${coach.coachLinkId}`]: () => {
        deletes++;
        coaches = [];
        return noContent();
      },
    });

    await user.click(await screen.findByRole("button", { name: "Remove Mark" }));
    const dialog = await screen.findByRole("dialog", { name: "Remove this coach?" });
    expect(within(dialog).getByText(/lose access straight away/)).toBeInTheDocument();
    expect(within(dialog).getByText(/feedback they gave you stays/)).toBeInTheDocument();

    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    expect(deletes).toBe(0);

    await user.click(screen.getByRole("button", { name: "Remove Mark" }));
    await user.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Remove" }));

    expect(await screen.findByText(/No coaches yet/)).toBeInTheDocument();
    expect(deletes).toBe(1);
  });

  it("offers Try again when the list can't load, and inviting still works", async () => {
    let fail = true;
    const { user } = renderSignedIn("/profile", {
      "GET /me": () => json(200, me),
      "GET /me/coaches": () => (fail ? json(404, { status: 404 }) : json(200, { coaches: [], invitations: [] })),
    });
    const coaches = await coachesRegion();

    expect(await within(coaches).findByText("Couldn't load your coaches.")).toBeInTheDocument();
    expect(within(coaches).getByRole("button", { name: "Send invitation" })).toBeEnabled();

    fail = false;
    await user.click(within(coaches).getByRole("button", { name: "Try again" }));
    expect(await within(coaches).findByText(/No coaches yet/)).toBeInTheDocument();
  });
});

const offer = {
  golferDisplayName: "Sam",
  shareReflections: false,
  sharePractice: true,
  expiresAt: "2026-11-08T10:00:00Z",
};
const invitationPath = "/coach-invitation?token=abc";
const student = {
  golferId: "00000000-0000-0000-0000-00000000000c",
  displayName: "Sam",
  lastActivityOn: "2026-11-01",
  unreadReplies: 0,
  shareReflections: false,
  sharePractice: true,
};

const paged = (body: unknown, total: number) =>
  new Response(JSON.stringify(body), {
    headers: { "Content-Type": "application/json", "X-Total-Count": String(total) },
  });

describe("Students (#200)", () => {
  it("is in the navigation only for someone with students", async () => {
    renderSignedIn("/profile", { "GET /me": () => json(200, { ...me, hasStudents: false }) });
    await screen.findByRole("region", { name: "Your details" });

    expect(screen.queryByRole("link", { name: "Students" })).not.toBeInTheDocument();
  });

  it("appears before Profile once there are students", async () => {
    renderSignedIn("/profile", { "GET /me": () => json(200, { ...me, hasStudents: true }) });

    await screen.findByRole("link", { name: "Students" });
    const links = within(screen.getByRole("navigation")).getAllByRole("link");
    expect(links.map((link) => link.textContent).slice(-2)).toEqual(["Students", "Profile"]);
  });

  it("shows each student's last activity, unread replies and what's shared", async () => {
    renderSignedIn("/students", {
      "GET /me": () => json(200, { ...me, hasStudents: true }),
      "GET /coach/students": () =>
        json(200, [
          { ...student, unreadReplies: 2 },
          { ...student, golferId: "d", displayName: "Ann", lastActivityOn: null, shareReflections: true },
        ]),
    });

    expect(await screen.findByRole("heading", { name: "Sam" })).toBeInTheDocument();
    expect(screen.getByText("Last activity: Nov 1, 2026")).toBeInTheDocument();
    expect(screen.getByText("2 unread replies")).toBeInTheDocument();
    expect(screen.getByText("You see: Rounds and progress and Practice")).toBeInTheDocument();
    expect(screen.getByText("No activity yet")).toBeInTheDocument();
  });

  it("says when there are none, and offers Try again when they can't load", async () => {
    let fail = true;
    const { user } = renderSignedIn("/students", {
      "GET /me": () => json(200, me),
      "GET /coach/students": () => (fail ? json(404, { status: 404 }) : json(200, [])),
    });

    const tryAgain = await screen.findByRole("button", { name: "Try again" });
    expect(screen.getByText("Couldn't load your students.")).toBeInTheDocument();
    fail = false;
    await user.click(tryAgain);

    expect(await screen.findByText(/No students yet/)).toBeInTheDocument();
  });

  it("stops coaching after asking, and leaves the navigation with the last student", async () => {
    let students = [student];
    const { user } = renderSignedIn("/students", {
      "GET /me": () => json(200, { ...me, hasStudents: students.length > 0 }),
      "GET /coach/students": () => json(200, students),
      [`DELETE /coach/students/${student.golferId}`]: () => {
        students = [];
        return noContent();
      },
    });
    await screen.findByRole("link", { name: "Students" });

    await user.click(await screen.findByRole("button", { name: "Stop coaching Sam" }));
    const dialog = await screen.findByRole("dialog", { name: "Stop coaching this golfer?" });
    await user.click(within(dialog).getByRole("button", { name: "Stop coaching" }));

    expect(await screen.findByText(/No students yet/)).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole("link", { name: "Students" })).not.toBeInTheDocument());
  });

  it("loads the next page on request", async () => {
    const { user } = renderSignedIn("/students", {
      "GET /me": () => json(200, me),
      "GET /coach/students": (request) =>
        new URL(request.url).searchParams.get("skip") === "0"
          ? paged([student], 2)
          : paged([{ ...student, golferId: "d", displayName: "Ann" }], 2),
    });

    await user.click(await screen.findByRole("button", { name: "Show more students" }));

    expect(await screen.findByRole("heading", { name: "Ann" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Show more students" })).not.toBeInTheDocument();
  });
});

describe("the invitation page (#185)", () => {
  it("shows who invites and what they share, then accepts and opens Students", async () => {
    const bodies: unknown[] = [];
    let accepted = false;
    const { router, user } = renderSignedIn(invitationPath, {
      "GET /me": () => json(200, { ...me, hasStudents: accepted }),
      "GET /coach/students": () => json(200, [student]),
      "POST /coach-invitations/preview": () => json(200, offer),
      "POST /coach-invitations/accept": async (request) => {
        bodies.push(await request.json());
        accepted = true;
        return json(200, { coachLinkId: "x", golferId: "y", ...offer });
      },
    });

    expect(await screen.findByText("Sam invited you to be their coach.")).toBeInTheDocument();
    expect(screen.getByText("Rounds and progress")).toBeInTheDocument();
    expect(screen.getByText("Practice")).toBeInTheDocument();
    expect(screen.queryByText("Reflections and notes")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Accept" }));

    expect(await screen.findByRole("heading", { name: "Sam" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/students");
    expect(await screen.findByRole("link", { name: "Students" })).toHaveAttribute("aria-current", "page");
    expect(bodies).toEqual([{ token: "abc" }]);
  });

  it("declines", async () => {
    const { user } = renderSignedIn(invitationPath, {
      "POST /coach-invitations/preview": () => json(200, offer),
      "POST /coach-invitations/decline": noContent,
    });

    await user.click(await screen.findByRole("button", { name: "Decline" }));

    expect(await screen.findByRole("status")).toHaveTextContent("You declined the invitation.");
  });

  it("says plainly when a link is expired, used or missing", async () => {
    renderSignedIn(invitationPath, { "POST /coach-invitations/preview": () => json(404, { status: 404 }) });

    expect(await screen.findByRole("alert")).toHaveTextContent("This invitation has expired");
  });

  it("needs no request for a link without a token", async () => {
    const { requests } = renderSignedIn("/coach-invitation", {});

    expect(await screen.findByRole("alert")).toHaveTextContent("This invitation has expired");
    expect(requests.some((r) => r.url.includes("coach-invitations"))).toBe(false);
  });

  it("explains a link meant for another address, and offers another account", async () => {
    const { user } = renderSignedIn(invitationPath, {
      "POST /coach-invitations/preview": () => json(403, { status: 403 }),
      "POST /auth/logout": noContent,
    });

    expect(await screen.findByRole("alert")).toHaveTextContent("This invitation is for another email address.");
    await user.click(screen.getByRole("button", { name: "Sign in with another account" }));

    expect(await screen.findByRole("link", { name: "Sign in" })).toBeInTheDocument();
  });

  it("explains when the golfer reached their coaches meanwhile, and keeps the offer", async () => {
    const { user } = renderSignedIn(invitationPath, {
      "POST /coach-invitations/preview": () => json(200, offer),
      "POST /coach-invitations/accept": () => json(409, { status: 409 }),
    });

    await user.click(await screen.findByRole("button", { name: "Accept" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("already has the most coaches");
    expect(screen.getByRole("button", { name: "Accept" })).toBeEnabled();
  });

  it("offers Try again when the server fails", async () => {
    let fail = true;
    const { user } = renderSignedIn(invitationPath, {
      "POST /coach-invitations/preview": () => (fail ? json(500, { status: 500 }) : json(200, offer)),
    });

    const tryAgain = await screen.findByRole("button", { name: "Try again" }, { timeout: 4000 });
    fail = false;
    await user.click(tryAgain);

    expect(await screen.findByText("Sam invited you to be their coach.")).toBeInTheDocument();
  });
});

/** The whole app signed out, as someone opening the link from their email. */
function renderSignedOut(path: string, handlers: Record<string, Handler>) {
  const all: Record<string, Handler> = {
    "GET /client-config": () => json(200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "1.0.0" } }),
    "POST /auth/refresh": () => json(401, { status: 401, title: "Unauthorized" }),
    ...handlers,
  };
  const session = new Session();
  const api = createApiClient({
    baseUrl: "https://api.example.test",
    tokens: session,
    fetch: vi.fn(async (request: Request) => {
      const handler = all[`${request.method} ${new URL(request.url).pathname}`];
      return handler ? handler(request) : json(404, { status: 404 });
    }),
  });
  session.attach(api);
  void session.restore();
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <App api={api} session={session} appVersion="1.0.0">
      <RouterProvider router={router} />
    </App>,
  );
  return { router, user: userEvent.setup() };
}

const tokens = {
  userId,
  token: "access-1",
  refreshToken: null,
  expiresAt: "2026-11-15T10:00:00Z",
};

describe("the invitation page, signed out (#185)", () => {
  it("asks to sign in, then comes back to the invitation", async () => {
    const { router, user } = renderSignedOut(invitationPath, {
      "POST /auth/login": () => json(200, tokens),
      "POST /coach-invitations/preview": () => json(200, offer),
    });

    expect(await screen.findByText(/Sign in or create an account/)).toBeInTheDocument();
    await user.click(screen.getByRole("link", { name: "Sign in" }));
    await user.type(await screen.findByLabelText("Email"), "pro@example.test");
    await user.type(screen.getByLabelText("Password"), "a-long-password");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Sam invited you to be their coach.")).toBeInTheDocument();
    expect(router.state.location.search).toBe("?token=abc");
  });

  it("comes back to the invitation from Create account, ahead of onboarding", async () => {
    const { router, user } = renderSignedOut(invitationPath, {
      "POST /auth/register": () => json(200, tokens),
      "POST /coach-invitations/preview": () => json(200, offer),
    });

    await user.click(await screen.findByRole("link", { name: "Create account" }));
    await user.type(await screen.findByLabelText("Name"), "Mark");
    await user.type(screen.getByLabelText("Email"), "pro@example.test");
    await user.type(screen.getByLabelText("Password"), "a-long-password");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByText("Sam invited you to be their coach.")).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/coach-invitation");
  });
});

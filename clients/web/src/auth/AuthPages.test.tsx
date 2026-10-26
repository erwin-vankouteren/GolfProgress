import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createApiClient } from "../api";
import { App } from "../app/App";
import { routes } from "../app/routes";
import { Session } from "../session/session";

type Handler = (request: Request) => Response | Promise<Response>;

const signedIn = {
  userId: "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f",
  token: "access-1",
  refreshToken: null,
  expiresAt: "2026-10-15T10:00:00Z",
};

const json = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json", ...headers },
  });

/**
 * A fake API: `/client-config` always passes, `/auth/refresh` fails unless overridden
 * (no cookie), and the other routes answer from `handlers`, keyed by "METHOD /path".
 */
function fakeApi(handlers: Record<string, Handler> = {}) {
  const requests: Request[] = [];
  const all: Record<string, Handler> = {
    "GET /client-config": () => json(200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "1.0.0" } }),
    "POST /auth/refresh": () => json(401, { status: 401, title: "Unauthorized" }),
    "POST /auth/logout": () => new Response(null, { status: 204 }),
    ...handlers,
  };
  const fetch = vi.fn(async (request: Request) => {
    requests.push(request);
    const handler = all[`${request.method} ${new URL(request.url).pathname}`];
    return handler ? handler(request) : json(404, { status: 404 });
  });
  return { requests, fetch };
}

/** Renders the whole app with a real Session over the fake API, restoring like main.tsx. */
function renderApp(path: string, handlers: Record<string, Handler> = {}) {
  const fake = fakeApi(handlers);
  const session = new Session();
  const api = createApiClient({
    baseUrl: "https://api.example.test",
    tokens: session,
    fetch: fake.fetch,
  });
  session.attach(api);
  void session.restore();

  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <App api={api} session={session} appVersion="1.0.0">
      <RouterProvider router={router} />
    </App>,
  );
  return { router, session, requests: fake.requests, user: userEvent.setup() };
}

const requestTo = (requests: Request[], path: string) => requests.find((r) => new URL(r.url).pathname === path);

/** A request the API never answers, like one waiting on a paused database. */
const never: Handler = () => new Promise<Response>(() => {});

/** Moves the fake clock on, letting React render what the timers set off. */
const wait = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

afterEach(() => {
  vi.useRealTimers();
  localStorage.clear();
  sessionStorage.clear();
});

describe("session restore", () => {
  it("sends a signed-out visitor to Welcome", async () => {
    const { router } = renderApp("/rounds");

    expect(await screen.findByRole("heading", { level: 1, name: "GolfProgress" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/welcome");
    expect(screen.getByRole("link", { name: "Create account" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Sign in" })).toBeInTheDocument();
  });

  it("signs straight back in from the refresh cookie", async () => {
    const { router, requests } = renderApp("/rounds", { "POST /auth/refresh": () => json(200, signedIn) });

    expect(await screen.findByRole("heading", { level: 1, name: "Rounds" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/rounds");

    const refresh = requestTo(requests, "/auth/refresh")!;
    expect(refresh.headers.get("X-Refresh-Token-Mode")).toBe("cookie");
    expect(refresh.credentials).toBe("include");
  });

  it("treats an unreachable API as signed out", async () => {
    renderApp("/", {
      "POST /auth/refresh": () => {
        throw new TypeError("Failed to fetch");
      },
    });

    expect(await screen.findByRole("link", { name: "Create account" })).toBeInTheDocument();
  });

  it("explains a slow restore, then gives up and treats the visitor as signed out", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    renderApp("/", { "POST /auth/refresh": never });

    expect(await screen.findByText("Signing you in…")).toBeInTheDocument();
    expect(screen.queryByText(/Waking up the server/)).not.toBeInTheDocument();

    await wait(5_000);
    expect(screen.getByText(/Waking up the server/)).toBeInTheDocument();

    await wait(90_000);
    expect(await screen.findByRole("link", { name: "Create account" })).toBeInTheDocument();
  });
});

describe("sign in", () => {
  it("signs in and returns to where the user was heading", async () => {
    const { router, user, requests } = renderApp("/practice", { "POST /auth/login": () => json(200, signedIn) });

    await user.click(await screen.findByRole("link", { name: "Sign in" }));
    await user.type(screen.getByLabelText("Email"), "  erwin@example.test ");
    await user.type(screen.getByLabelText("Password"), "correct horse");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Practice" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/practice");

    const login = requestTo(requests, "/auth/login")!;
    expect(login.headers.get("X-Refresh-Token-Mode")).toBe("cookie");
    expect(login.credentials).toBe("include");
    expect(await login.clone().json()).toEqual({ email: "erwin@example.test", password: "correct horse" });
  });

  it("keeps tokens out of browser storage", async () => {
    const { user } = renderApp("/sign-in", { "POST /auth/login": () => json(200, signedIn) });

    await user.type(await screen.findByLabelText("Email"), "erwin@example.test");
    await user.type(screen.getByLabelText("Password"), "correct horse");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await screen.findByRole("heading", { level: 1, name: "Home" });

    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it("checks required fields and email shape before calling the API", async () => {
    const { user, requests } = renderApp("/sign-in");

    await user.click(await screen.findByRole("button", { name: "Sign in" }));

    expect(screen.getByText("Email is required.")).toBeInTheDocument();
    expect(screen.getByText("Password is required.")).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByLabelText("Email")).toHaveAccessibleDescription("Email is required.");

    await user.type(screen.getByLabelText("Email"), "not-an-email");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    expect(screen.getByText("Enter a valid email address.")).toBeInTheDocument();

    expect(requestTo(requests, "/auth/login")).toBeUndefined();
  });

  it("says so when the email or password is wrong", async () => {
    const { user } = renderApp("/sign-in", {
      "POST /auth/login": () => json(401, { status: 401, title: "Unauthorized" }),
    });

    await user.type(await screen.findByLabelText("Email"), "erwin@example.test");
    await user.type(screen.getByLabelText("Password"), "wrong");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Email or password is wrong.");
  });

  it("shows how long to wait after too many attempts", async () => {
    const { user } = renderApp("/sign-in", {
      "POST /auth/login": () => json(429, { status: 429, title: "Too Many Requests" }, { "Retry-After": "42" }),
    });

    await user.type(await screen.findByLabelText("Email"), "erwin@example.test");
    await user.type(screen.getByLabelText("Password"), "whatever");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Too many attempts. Try again in 42 seconds.");
  });

  it("reports a connection problem", async () => {
    const { user } = renderApp("/sign-in", {
      "POST /auth/login": () => {
        throw new TypeError("Failed to fetch");
      },
    });

    await user.type(await screen.findByLabelText("Email"), "erwin@example.test");
    await user.type(screen.getByLabelText("Password"), "whatever");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Couldn't reach GolfProgress.");
  });

  it("explains a slow sign-in, then gives up so the user can try again", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const { user } = renderApp("/sign-in", { "POST /auth/login": never });

    await user.type(await screen.findByLabelText("Email"), "erwin@example.test");
    await user.type(screen.getByLabelText("Password"), "whatever");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(screen.getByRole("button", { name: "Please wait…" })).toBeDisabled();
    await wait(5_000);
    expect(screen.getByRole("status")).toHaveTextContent("Waking up the server");

    await wait(90_000);
    expect(await screen.findByRole("alert")).toHaveTextContent("Couldn't reach GolfProgress.");
    expect(screen.getByRole("button", { name: "Sign in" })).toBeEnabled();
    expect(screen.queryByText(/Waking up the server/)).not.toBeInTheDocument();
  });
});

describe("create account", () => {
  it("creates the account, signs in and starts onboarding", async () => {
    const { user, requests } = renderApp("/create-account", { "POST /auth/register": () => json(200, signedIn) });

    await user.type(await screen.findByLabelText("Name"), "Erwin");
    await user.type(screen.getByLabelText("Email"), "erwin@example.test");
    await user.type(screen.getByLabelText("Password"), "correct horse battery");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Which units do you use?" })).toBeInTheDocument();
    expect(await requestTo(requests, "/auth/register")!.clone().json()).toEqual({
      email: "erwin@example.test",
      displayName: "Erwin",
      password: "correct horse battery",
    });
  });

  it("shows the API's validation messages next to their fields", async () => {
    const { user } = renderApp("/create-account", {
      "POST /auth/register": () =>
        json(400, { status: 400, errors: { Password: ["Password must be at least 12 characters."] } }),
    });

    await user.type(await screen.findByLabelText("Name"), "Erwin");
    await user.type(screen.getByLabelText("Email"), "erwin@example.test");
    await user.type(screen.getByLabelText("Password"), "short");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByText("Password must be at least 12 characters.")).toBeInTheDocument();
    expect(screen.getByLabelText("Password")).toHaveAttribute("aria-invalid", "true");
  });

  it("explains when the email is already taken", async () => {
    const { user } = renderApp("/create-account", {
      "POST /auth/register": () => json(409, { status: 409, title: "Conflict" }),
    });

    await user.type(await screen.findByLabelText("Name"), "Erwin");
    await user.type(screen.getByLabelText("Email"), "erwin@example.test");
    await user.type(screen.getByLabelText("Password"), "correct horse battery");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("An account with this email already exists.");
  });
});

describe("sign out", () => {
  it("revokes the cookie and returns to Welcome", async () => {
    const { user, router, requests, session } = renderApp("/profile", {
      "POST /auth/refresh": () => json(200, signedIn),
    });

    await user.click(await screen.findByRole("button", { name: "Sign out" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/welcome"));
    expect(requestTo(requests, "/auth/logout")!.credentials).toBe("include");
    expect(session.getState()).toEqual({ status: "signedOut" });
    expect(session.accessToken()).toBeNull();
  });

  it("returns to Welcome when the session expires", async () => {
    let cookieValid = true;
    const { router, session } = renderApp("/rounds", {
      "POST /auth/refresh": () => (cookieValid ? json(200, signedIn) : json(401, { status: 401 })),
    });
    await screen.findByRole("heading", { level: 1, name: "Rounds" });

    // The refresh token was revoked elsewhere, so the next refresh (after a 401) fails.
    cookieValid = false;
    await session.refreshAccessToken();

    await waitFor(() => expect(router.state.location.pathname).toBe("/welcome"));
  });
});

describe("password reset (#86)", () => {
  it("asks for a link from Sign in, and says the same whatever the address", async () => {
    const { user, requests } = renderApp("/sign-in", {
      "POST /auth/password-reset": () => new Response(null, { status: 202 }),
    });

    await user.click(await screen.findByRole("link", { name: "Forgot password?" }));
    expect(await screen.findByRole("heading", { level: 1, name: "Reset your password" })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Send reset link" }));
    expect(screen.getByText("Email is required.")).toBeInTheDocument();

    await user.type(screen.getByLabelText("Email"), " erwin@example.test ");
    await user.click(screen.getByRole("button", { name: "Send reset link" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "If erwin@example.test has a GolfProgress account, a link to reset its password is on its way.",
    );
    expect(await requestTo(requests, "/auth/password-reset")!.clone().json()).toEqual({ email: "erwin@example.test" });
  });

  it("sets the new password from the link", async () => {
    const { user, requests } = renderApp("/reset-password?token=abc-123", {
      "POST /auth/password-reset/confirm": () => new Response(null, { status: 204 }),
    });

    const password = await screen.findByLabelText("New password");
    await user.type(password, "short");
    await user.click(screen.getByRole("button", { name: "Save new password" }));
    expect(screen.getByText("Use at least 8 characters.", { selector: ".auth-field-error" })).toBeInTheDocument();

    await user.type(password, " and longer");
    await user.click(screen.getByRole("button", { name: "Save new password" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Your password is changed");
    expect(await requestTo(requests, "/auth/password-reset/confirm")!.clone().json()).toEqual({
      token: "abc-123",
      newPassword: "short and longer",
    });
  });

  it("explains a used or expired link, and offers a new one", async () => {
    const { user } = renderApp("/reset-password?token=old", {
      "POST /auth/password-reset/confirm": () =>
        json(400, { status: 400, errors: { Token: ["This reset link is invalid or has expired."] } }),
    });

    await user.type(await screen.findByLabelText("New password"), "a long new password");
    await user.click(screen.getByRole("button", { name: "Save new password" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("This reset link is invalid or has expired");
    expect(screen.getByRole("link", { name: "Ask for a new link" })).toHaveAttribute("href", "/forgot-password");
  });
});

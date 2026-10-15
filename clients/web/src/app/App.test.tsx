import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it } from "vitest";
import { createApiClient } from "../api";
import { Session } from "../session/session";
import { compareVersions } from "../version/compareVersions";
import { App } from "./App";
import { routes } from "./routes";

/** A fake API whose GET /client-config answers with `minimumWeb`, or fails. */
function fakeApi(minimumWeb: string | "error" = "1.0.0") {
  return createApiClient({
    baseUrl: "https://api.example.test",
    tokens: { accessToken: () => null, refreshAccessToken: async () => null },
    fetch: async () =>
      minimumWeb === "error"
        ? new Response(JSON.stringify({ status: 500 }), {
            status: 500,
            headers: { "Content-Type": "application/problem+json" },
          })
        : new Response(JSON.stringify({ minimumVersions: { ios: "1.0.0", android: "1.0.0", web: minimumWeb } }), {
            headers: { "Content-Type": "application/json" },
          }),
  });
}

function renderApp({ path = "/", appVersion = "1.0.0", minimumWeb = "1.0.0" as string } = {}) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  const api = fakeApi(minimumWeb);
  const session = new Session({ accessToken: "token", userId: "user-1" });
  session.attach(api);
  render(
    <App api={api} session={session} appVersion={appVersion}>
      <RouterProvider router={router} />
    </App>,
  );
  return { router, user: userEvent.setup() };
}

describe("app shell", () => {
  it("shows the five sections, with the current one marked", () => {
    renderApp();

    const nav = screen.getByRole("navigation", { name: "Main" });
    for (const name of ["Home", "Rounds", "Practice", "Progress", "Profile"]) {
      expect(nav).toContainElement(screen.getByRole("link", { name }));
    }
    expect(screen.getByRole("link", { name: "Home" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("heading", { level: 1, name: "Home" })).toBeInTheDocument();
  });

  it("moves between sections, and focuses the new page", async () => {
    const { user } = renderApp();

    await user.click(screen.getByRole("link", { name: "Rounds" }));

    expect(screen.getByRole("heading", { level: 1, name: "Rounds" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Rounds" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("main")).toHaveFocus();

    await user.click(screen.getByRole("link", { name: "Practice" }));
    expect(screen.getByRole("heading", { level: 1, name: "Practice" })).toBeInTheDocument();
  });

  it("reaches Log round from the header", async () => {
    const { user, router } = renderApp({ path: "/progress" });

    await user.click(screen.getAllByRole("link", { name: "Log round" })[0]!);

    expect(router.state.location.pathname).toBe("/rounds/new");
    expect(screen.getByRole("heading", { level: 1, name: "Log round" })).toBeInTheDocument();
  });

  it("offers a skip link to the main content", () => {
    renderApp();

    expect(screen.getByRole("link", { name: "Skip to content" })).toHaveAttribute("href", "#main");
  });

  it("toggles the narrow-screen menu and reports its state", async () => {
    const { user } = renderApp();
    const button = screen.getByRole("button", { name: "Open menu" });

    expect(button).toHaveAttribute("aria-expanded", "false");
    await user.click(button);
    expect(screen.getByRole("button", { name: "Close menu" })).toHaveAttribute("aria-expanded", "true");
  });

  it("shows a not-found page for unknown paths", () => {
    renderApp({ path: "/nowhere" });

    expect(screen.getByRole("heading", { level: 1, name: "Page not found" })).toBeInTheDocument();
  });
});

describe("minimum version (GET /client-config)", () => {
  it("shows Update required when this build is older than the minimum", async () => {
    renderApp({ appVersion: "1.0.0", minimumWeb: "1.2.0" });

    expect(await screen.findByRole("heading", { name: "Update required" })).toBeInTheDocument();
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  });

  it("shows the app when this build meets the minimum", async () => {
    renderApp({ appVersion: "1.2.0", minimumWeb: "1.2.0" });

    await waitFor(() => expect(screen.getByRole("navigation", { name: "Main" })).toBeInTheDocument());
    expect(screen.queryByRole("heading", { name: "Update required" })).not.toBeInTheDocument();
  });

  it("keeps the app usable when the check fails", async () => {
    renderApp({ minimumWeb: "error" });

    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(screen.getByRole("navigation", { name: "Main" })).toBeInTheDocument();
  });
});

describe("compareVersions", () => {
  it.each([
    ["1.0.0", "1.0.0", 0],
    ["1.0.0", "1.0.1", -1],
    ["1.10.0", "1.9.0", 1],
    ["2.0.0", "1.99.99", 1],
    ["1.0", "1.0.0", 0],
  ])("%s vs %s", (a, b, sign) => {
    expect(Math.sign(compareVersions(a, b))).toBe(sign);
  });
});

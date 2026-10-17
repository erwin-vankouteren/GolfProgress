import { screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { countTo, json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";

const me = {
  userId,
  email: "erwin@example.test",
  displayName: "Erwin",
  timezone: null,
  units: "Metric",
  trackingMode: "Minimal",
  handicapIndex: 18.4,
  typical18Score: 92,
  goalFocus: ["Putting"],
  createdAt: "2026-05-01T10:00:00Z",
  updatedAt: "2026-05-01T10:00:00Z",
};

/** A fake API holding the profile in memory; PATCH merges into it, as the API does. */
function renderProfile(path = "/profile", handlers: Record<string, Handler> = {}) {
  let current: Record<string, unknown> = { ...me };
  const patches: Record<string, unknown>[] = [];
  const view = renderSignedIn(path, {
    "GET /me": () => json(200, current),
    "PATCH /me": async (request) => {
      const body = (await request.json()) as Record<string, unknown>;
      patches.push(body);
      current = { ...current, ...body };
      return json(200, current);
    },
    "POST /auth/logout": () => new Response(null, { status: 204 }),
    ...handlers,
  });
  return { ...view, patches };
}

const region = (name: string) => screen.findByRole("region", { name });

describe("profile (#111)", () => {
  it("saves your details, clearing an emptied handicap", async () => {
    const { user, patches } = renderProfile();
    const details = await region("Your details");

    expect(within(details).getByLabelText("Email")).toHaveAttribute("readonly");
    const name = within(details).getByLabelText("Name");
    await user.clear(name);
    await user.type(name, "Erwin van K");
    await user.clear(within(details).getByLabelText("Handicap index"));
    await user.click(within(details).getByRole("button", { name: "Save" }));

    expect(await within(details).findByRole("status")).toHaveTextContent("Changes saved.");
    expect(patches).toEqual([{ displayName: "Erwin van K", handicapIndex: null, typical18Score: 92 }]);
  });

  it("checks the rules before sending", async () => {
    const { user, patches } = renderProfile();
    const details = await region("Your details");

    const typical = within(details).getByLabelText("Typical 18-hole score");
    await user.clear(typical);
    await user.type(typical, "300");
    await user.click(within(details).getByRole("button", { name: "Save" }));

    expect(within(details).getByText("Enter a number from 18 to 200.")).toBeInTheDocument();
    expect(typical).toHaveAttribute("aria-invalid", "true");
    expect(typical).toHaveFocus();
    expect(patches).toEqual([]);
  });

  it("changes the tracking mode, and Log round shows its fields at once", async () => {
    const { user, patches, router } = renderProfile();
    const preferences = await region("Preferences");

    await user.click(within(preferences).getByRole("radio", { name: /Detailed/ }));
    await user.click(within(preferences).getByRole("button", { name: "Save" }));
    await within(preferences).findByRole("status");
    expect(patches).toEqual([{ units: "Metric", trackingMode: "Detailed", goalFocus: ["Putting"] }]);

    await router.navigate("/rounds/new");
    expect(await screen.findByLabelText(/Fairways hit/, { selector: "input" })).toBeInTheDocument();
  });

  it("allows three goal focus areas at most", async () => {
    const { user } = renderProfile();
    const preferences = await region("Preferences");
    const box = (name: string) => within(preferences).getByRole("checkbox", { name });

    await user.click(box("Penalties"));
    await user.click(box("Tee shots"));

    expect(box("Consistency")).toBeDisabled();
    await user.click(box("Putting"));
    expect(box("Consistency")).toBeEnabled();
  });

  it("shows a wrong current password next to it, and stays signed in", async () => {
    const { user, router } = renderProfile("/profile", {
      "POST /me/password": () =>
        json(400, { status: 400, errors: { CurrentPassword: ["The current password is incorrect."] } }),
    });
    const account = await region("Account");

    await user.type(within(account).getByLabelText("Current password"), "wrong one");
    await user.type(within(account).getByLabelText("New password"), "a brand new passphrase");
    await user.click(within(account).getByRole("button", { name: "Change password" }));

    expect(await within(account).findByText("The current password is incorrect.")).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/profile");
  });

  it("changes the password", async () => {
    const { user, requests } = renderProfile("/profile", {
      "POST /me/password": () => new Response(null, { status: 204 }),
    });
    const account = await region("Account");

    await user.type(within(account).getByLabelText("Current password"), "old password");
    await user.type(within(account).getByLabelText("New password"), "short");
    await user.click(within(account).getByRole("button", { name: "Change password" }));
    expect(within(account).getByText("Use at least 8 characters.")).toBeInTheDocument();
    expect(countTo(requests, "/me/password")).toBe(0);

    await user.type(within(account).getByLabelText("New password"), " and longer");
    await user.click(within(account).getByRole("button", { name: "Change password" }));

    expect(await within(account).findByText("Password changed.", { selector: ".visually-hidden" })).toBeInTheDocument();
    expect(within(account).getByLabelText("Current password")).toHaveValue("");
  });

  it("deletes the account after asking for the password, then returns to Welcome", async () => {
    const { user, requests, router } = renderProfile("/profile", {
      "DELETE /me": async (request) => {
        const { password } = (await request.json()) as { password: string };
        return password === "right one"
          ? new Response(null, { status: 204 })
          : json(400, { status: 400, errors: { Password: ["The password is incorrect."] } });
      },
    });
    const account = await region("Account");

    await user.click(within(account).getByRole("button", { name: "Delete account" }));
    const dialog = screen.getByRole("dialog", { name: "Delete your account?" });
    const password = within(dialog).getByLabelText("Password");
    expect(password).toHaveFocus();

    await user.type(password, "wrong one");
    await user.click(within(dialog).getByRole("button", { name: "Delete my account" }));
    expect(await within(dialog).findByRole("alert")).toHaveTextContent("The password is incorrect.");
    expect(router.state.location.pathname).toBe("/profile");

    await user.clear(password);
    await user.type(password, "right one{Enter}");

    await waitFor(() => expect(router.state.location.pathname).toBe("/welcome"));
    expect(countTo(requests, "/auth/logout")).toBe(1);
  });

  it("shows the app's version", async () => {
    renderProfile();
    const about = await region("About");
    expect(within(about).getByText(/^Version \d+\.\d+\.\d+$/)).toBeInTheDocument();
  });
});

describe("onboarding (#111)", () => {
  it("asks units, tracking mode and goals, saves them together, then opens Log round", async () => {
    const { user, patches, router } = renderProfile("/onboarding");

    expect(await screen.findByRole("heading", { level: 1, name: "Which units do you use?" })).toBeInTheDocument();
    expect(screen.getByText("Step 1 of 3")).toBeInTheDocument();
    await user.click(screen.getByRole("radio", { name: "Imperial" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    const tracking = screen.getByRole("heading", { level: 1, name: "How much do you want to track?" });
    expect(tracking).toHaveFocus();
    await user.click(screen.getByRole("radio", { name: /Balanced/ }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.click(screen.getByRole("checkbox", { name: "Penalties" }));
    await user.click(screen.getByRole("button", { name: "Log your first round" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/rounds/new"));
    expect(patches).toHaveLength(1);
    expect(patches[0]).toMatchObject({ units: "Imperial", trackingMode: "Balanced", goalFocus: ["Penalties"] });
    expect(typeof patches[0]!.timezone).toBe("string");
  });

  it("can go back a step, or be skipped", async () => {
    const { user, patches, router } = renderProfile("/onboarding");

    await user.click(await screen.findByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Back" }));
    expect(screen.getByRole("heading", { level: 1, name: "Which units do you use?" })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Skip for now" }));
    expect(router.state.location.pathname).toBe("/");
    expect(patches).toEqual([]);
  });
});

import { screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { json, renderSignedIn, testUserId as userId } from "../test/fakeApi";
import { formatPrice, planBanner } from "./plan";
import { goTo } from "./SubscriptionSection";

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
  hasStudents: false,
  emailFeedbackDigest: true,
  emailCoachDigest: true,
  plan: "Trial",
  trialEndsAt: null,
  coaching: true,
  subscription: null,
};

const prices = { currency: "eur", premiumMonth: 299, premiumYear: 2900, coachingMonth: 199, coachingYear: 1900 };
const soon = new Date(Date.now() + 10 * 86_400_000).toISOString();

beforeAll(async () => {
  await import("../profile/ProfilePage");
  await import("../coaching/StudentsPage");
});

afterEach(() => vi.restoreAllMocks());

describe("plan rules (#313)", () => {
  it("shows a banner after the trial, and while the trial runs out, not before or when subscribed", () => {
    const now = new Date("2026-12-01T00:00:00Z");
    expect(planBanner({ plan: "ReadOnly", trialEndsAt: "2026-11-01T00:00:00Z" }, now)).toEqual({ kind: "readOnly" });
    expect(planBanner({ plan: "Trial", trialEndsAt: "2026-12-20T00:00:00Z" }, now)).toEqual({
      kind: "trialEnding",
      endsAt: "2026-12-20T00:00:00Z",
    });
    expect(planBanner({ plan: "Trial", trialEndsAt: "2027-03-01T00:00:00Z" }, now)).toBeNull();
    expect(planBanner({ plan: "Trial", trialEndsAt: null }, now)).toBeNull();
    expect(planBanner({ plan: "Subscribed", trialEndsAt: "2026-11-01T00:00:00Z" }, now)).toBeNull();
  });

  it("formats prices in the user's language", () => {
    expect(formatPrice(299, "eur", "en")).toBe("€2.99");
    expect(formatPrice(2900, "eur", "nl")).toMatch(/€\s?29,00/);
  });
});

const subscriptionRegion = () => screen.findByRole("region", { name: "Subscription" });

describe("Profile → Subscription (#313)", () => {
  it("says GolfProgress is free in the launch period, with nothing to subscribe to", async () => {
    renderSignedIn("/profile", {
      "GET /me": () => json(200, me),
      "GET /me/coaches": () => json(200, { coaches: [], invitations: [] }),
    });
    const section = await subscriptionRegion();

    expect(within(section).getByText("GolfProgress is free during its launch period.")).toBeInTheDocument();
    expect(within(section).queryByRole("button", { name: "Subscribe" })).not.toBeInTheDocument();
  });

  it("subscribes with the chosen period and Coaching, through Stripe's page", async () => {
    const go = vi.spyOn(goTo, "url").mockImplementation(() => {});
    const bodies: unknown[] = [];
    const { user } = renderSignedIn("/profile", {
      "GET /me": () => json(200, { ...me, trialEndsAt: soon }),
      "GET /me/coaches": () => json(200, { coaches: [], invitations: [] }),
      "GET /subscription/prices": () => json(200, prices),
      "POST /me/subscription/checkout": async (request) => {
        bodies.push(await request.json());
        return json(200, { url: "https://checkout.stripe.test/c/1" });
      },
    });
    const section = await subscriptionRegion();

    expect(within(section).getByText(/Your free trial runs until/)).toBeInTheDocument();
    await user.click(await within(section).findByRole("radio", { name: "€29.00 a year" }));
    await user.click(within(section).getByRole("checkbox", { name: "Add Coaching, €19.00 a year" }));
    await user.click(within(section).getByRole("button", { name: "Subscribe" }));

    await waitFor(() => expect(go).toHaveBeenCalledWith("https://checkout.stripe.test/c/1"));
    expect(bodies).toEqual([{ period: "Year", coaching: true }]);
  });

  it("says when subscribing isn't set up yet", async () => {
    renderSignedIn("/profile", {
      "GET /me": () => json(200, { ...me, plan: "ReadOnly", trialEndsAt: "2026-10-01T00:00:00Z" }),
      "GET /me/coaches": () => json(200, { coaches: [], invitations: [] }),
      "GET /subscription/prices": () => json(503, { status: 503, title: "Subscribing isn't available yet." }),
    });
    const section = await subscriptionRegion();

    expect(await within(section).findByText("Subscribing isn't available yet.")).toBeInTheDocument();
    expect(within(section).getByText("Your free trial has ended.")).toBeInTheDocument();
  });

  it("shows a subscription with Manage, which opens Stripe's portal", async () => {
    const go = vi.spyOn(goTo, "url").mockImplementation(() => {});
    const { user } = renderSignedIn("/profile", {
      "GET /me": () =>
        json(200, {
          ...me,
          plan: "Subscribed",
          trialEndsAt: "2026-10-01T00:00:00Z",
          subscription: {
            source: "Stripe",
            period: "Month",
            renewsAt: "2027-01-05T00:00:00Z",
            cancelsAt: null,
            coachingAddOn: true,
          },
        }),
      "GET /me/coaches": () => json(200, { coaches: [], invitations: [] }),
      "POST /me/subscription/portal": () => json(200, { url: "https://billing.stripe.test/p/1" }),
    });
    const section = await subscriptionRegion();

    expect(within(section).getByText(/Monthly subscription/)).toHaveTextContent(
      "Monthly subscription · Coaching included",
    );
    expect(within(section).getByText(/Renews on/)).toBeInTheDocument();
    await user.click(within(section).getByRole("button", { name: "Manage subscription" }));
    await waitFor(() => expect(go).toHaveBeenCalledWith("https://billing.stripe.test/p/1"));
  });

  it("sends someone who subscribed in the Android app to Google Play", async () => {
    renderSignedIn("/profile", {
      "GET /me": () =>
        json(200, {
          ...me,
          plan: "Subscribed",
          trialEndsAt: "2026-10-01T00:00:00Z",
          subscription: {
            source: "GooglePlay",
            period: "Year",
            renewsAt: null,
            cancelsAt: "2027-02-01T00:00:00Z",
            coachingAddOn: false,
          },
        }),
      "GET /me/coaches": () => json(200, { coaches: [], invitations: [] }),
    });
    const section = await subscriptionRegion();

    expect(within(section).getByText(/Ends on/)).toBeInTheDocument();
    expect(
      within(section).getByText("You subscribed in the Android app. Manage it in Google Play."),
    ).toBeInTheDocument();
    expect(within(section).queryByRole("button", { name: "Manage subscription" })).not.toBeInTheDocument();
  });
});

describe("read-only after the trial (#313)", () => {
  const readOnly = { ...me, plan: "ReadOnly", trialEndsAt: "2026-10-01T00:00:00Z" };

  it("says so above every page, and takes Log round away", async () => {
    renderSignedIn("/", {
      "GET /me": () => json(200, readOnly),
      "GET /me/dashboard": () => json(404, { status: 404 }),
      "GET /me/feedback/unread": () => json(200, { count: 0, threads: [] }),
    });

    expect(
      await screen.findByText(/Your free trial has ended. Everything you've logged is still here/),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Subscribe" })).toHaveAttribute("href", "/profile#subscription");
    expect(screen.queryByRole("link", { name: "Log round" })).not.toBeInTheDocument();
  });

  it("keeps the trial's last weeks in a banner too", async () => {
    renderSignedIn("/", {
      "GET /me": () => json(200, { ...me, trialEndsAt: soon }),
      "GET /me/dashboard": () => json(404, { status: 404 }),
      "GET /me/feedback/unread": () => json(200, { count: 0, threads: [] }),
    });

    expect(await screen.findByText(/Your free trial ends on/)).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: "Log round" }).length).toBeGreaterThan(0);
  });
});

describe("Coaching as an add-on (#312)", () => {
  it("tells a golfer without Coaching how to get it, instead of the invite form", async () => {
    renderSignedIn("/profile", {
      "GET /me": () => json(200, { ...me, plan: "Subscribed", trialEndsAt: "2026-10-01T00:00:00Z", coaching: false }),
      "GET /me/coaches": () => json(200, { coaches: [], invitations: [] }),
    });
    const coaches = await screen.findByRole("region", { name: "Coaches" });

    expect(await within(coaches).findByText(/Add Coaching to your subscription to invite a coach/)).toBeInTheDocument();
    expect(within(coaches).getByRole("link", { name: "Add Coaching" })).toHaveAttribute(
      "href",
      "/profile#subscription",
    );
    expect(within(coaches).queryByRole("button", { name: "Send invitation" })).not.toBeInTheDocument();
  });

  it("shows a coach a paused student, without a link to their golf", async () => {
    const student = {
      golferId: "00000000-0000-0000-0000-00000000000c",
      displayName: "Sam",
      lastActivityOn: null,
      unreadReplies: 0,
      shareReflections: true,
      sharePractice: true,
      paused: true,
    };
    renderSignedIn("/students", {
      "GET /me": () => json(200, { ...me, hasStudents: true }),
      "GET /coach/students": () =>
        new Response(JSON.stringify([student]), {
          status: 200,
          headers: { "Content-Type": "application/json", "X-Total-Count": "1" },
        }),
    });

    expect(await screen.findByText(/Paused: they don't have Coaching at the moment/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Sam" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Sam" })).not.toBeInTheDocument();
  });
});

import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page, type Route } from "@playwright/test";

/**
 * The accessibility audit's automated part (#120): every screen of the web app, in light
 * and dark mode, checked by axe against WCAG 2.1 A and AA. The API is mocked with the
 * data each screen needs, so every section shows content rather than an empty state.
 * Screen readers and keyboard use still need checking by hand (docs/clients/accessibility.md).
 */
const userId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f";
const roundId = "00000000-0000-0000-0000-000000000001";
const sessionId = "00000000-0000-0000-0001-000000000001";
const now = "2026-10-11T10:00:00Z";

const me = {
  userId,
  email: "erwin@example.test",
  displayName: "Erwin",
  timezone: "Europe/Amsterdam",
  units: "Metric",
  trackingMode: "Detailed",
  handicapIndex: 18.4,
  typical18Score: 92,
  goalFocus: ["Putting"],
  createdAt: now,
  updatedAt: now,
};

const round = {
  roundId,
  userId,
  playedAt: "2026-10-11",
  holes: 18,
  courseName: "Hilversum",
  teeName: "Yellow",
  totalStrokes: 88,
  totalPutts: 33,
  penalties: 1,
  fairwaysHit: 7,
  gir: 5,
  threePutts: 2,
  blowUpHoles: 1,
  source: "Manual",
  notes: "Windy back nine.",
  createdAt: now,
  updatedAt: now,
  fitnessScore: 72,
  fitnessConfidence: "Medium",
};

const score = {
  fitnessScoreId: "00000000-0000-0000-0002-000000000001",
  userId,
  roundId,
  scoreTotal: 72,
  execution: 18,
  damageControl: 21,
  consistency: 23,
  habits: 10,
  confidenceLevel: "Medium",
  computedAt: now,
  explanation: {
    baselineAvgScore: 90,
    baselineStdDev: 3,
    roundsInHabitWindow: 3,
    practiceSessionsInHabitWindow: 1,
    reflected: false,
  },
};

const microWin = {
  microWinId: "00000000-0000-0000-0003-000000000001",
  roundId,
  userId,
  category: "StatDelta",
  metric: "penalties",
  direction: "Improved",
  deltaValue: 1,
  confidenceLevel: "Medium",
  title: "Fewer penalties",
  message: "One penalty, down from two.",
  createdAt: now,
};

const lever = {
  roundId,
  userId,
  leverKey: "three_putts",
  title: "Cut down three-putts",
  recommendation: "On long putts, aim for a one-metre circle: pace first.",
  rationale: "Three-putts cost you most strokes last round.",
  confidenceLevel: "Medium",
  createdAt: now,
};

const session = {
  practiceSessionId: sessionId,
  userId,
  practicedAt: "2026-10-09",
  durationMinutes: 45,
  sessionType: "Putting",
  venueName: "Home course",
  tags: ["lag putting"],
  primaryGoal: "Pace control",
  notes: null,
  createdAt: now,
};

const point = (i: number, playedAt: string, fitnessScoreTotal: number) => ({
  roundId: `00000000-0000-0000-0000-00000000000${i}`,
  playedAt,
  holes: 18,
  totalStrokes: 90 - i,
  totalPutts: null,
  penalties: null,
  threePutts: null,
  blowUpHoles: null,
  fitnessScoreTotal,
  fitnessConfidence: "Medium",
  microWinTitle: null,
});

const month = (monthStart: string, fitnessAvg: number | null, roundsLogged: number) => ({
  monthStart,
  fitnessAvg,
  fitnessChange: null,
  roundsLogged,
  highlights: [],
  hasReview: false,
});

async function mockApi(page: Page, signedIn: boolean) {
  const json = (route: Route, status: number, body: unknown, headers: Record<string, string> = {}) =>
    route.fulfill({
      status,
      contentType: status >= 400 ? "application/problem+json" : "application/json",
      headers,
      body: JSON.stringify(body),
    });

  const answers: Record<string, () => [number, unknown, Record<string, string>?]> = {
    "GET /me": () => [200, me],
    "GET /me/dashboard": () => [
      200,
      {
        userId,
        lastRound: {
          roundId,
          playedAt: round.playedAt,
          totalStrokes: 88,
          fitnessScore: 72,
          fitnessConfidence: "Medium",
        },
        microWins: [microWin],
        nextLever: lever,
        baselineAvgScore: 91.4,
        baselineConfidence: "Low",
        monthlyFitnessAvg: 70,
        monthlyFitnessChange: 4,
        generatedAt: now,
      },
    ],
    "GET /me/rounds": () => [200, [round], { "X-Total-Count": "1" }],
    [`GET /rounds/${roundId}`]: () => [200, round],
    [`GET /rounds/${roundId}/fitness-score`]: () => [200, score],
    [`GET /rounds/${roundId}/micro-wins`]: () => [200, [microWin]],
    [`GET /rounds/${roundId}/next-lever`]: () => [200, lever],
    [`GET /rounds/${roundId}/reflections`]: () => [200, []],
    "GET /me/practice-sessions": () => [200, [session], { "X-Total-Count": "1" }],
    [`GET /me/practice-sessions/${sessionId}`]: () => [200, session],
    "GET /me/timeline": () => [
      200,
      {
        userId,
        count: 4,
        items: [
          point(1, "2026-08-02", 55),
          point(2, "2026-09-06", 60),
          point(3, "2026-10-04", 64),
          point(4, "2026-10-11", 70),
        ],
      },
    ],
    "GET /me/timeline/monthly": () => [
      200,
      {
        userId,
        months: 3,
        items: [month("2026-08-01", 55, 1), month("2026-09-01", 60, 1), month("2026-10-01", 67, 2)],
      },
    ],
    "GET /me/baseline": () => [
      200,
      {
        userId,
        windowRounds: 5,
        sampleSize: 4,
        avgScore: 88.3,
        scoreStdDev: 3.4,
        avgPutts: 33.5,
        avgPenalties: 1.2,
        avgThreePutts: null,
        avgBlowUpHoles: null,
        avgFairwaysHit: null,
        avgGir: null,
        completenessRatio: 0.6,
        confidenceLevel: "Medium",
        computedAt: now,
      },
    ],
  };

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");
    const key = `${request.method()} ${path}`;

    if (path === "/client-config")
      return json(route, 200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "1.0.0" } });
    if (path === "/auth/refresh")
      return signedIn
        ? json(route, 200, {
            userId,
            token: "t",
            refreshToken: null,
            expiresAt: new Date(Date.now() + 9e5).toISOString(),
          })
        : json(route, 401, { status: 401 });
    const answer = answers[key];
    if (answer) {
      const [status, body, headers] = answer();
      return json(route, status, body, headers);
    }
    return json(route, 404, { status: 404 });
  });
}

const signedOutPages: [string, string][] = [
  ["/welcome", "GolfProgress"],
  ["/sign-in", "Sign in"],
  ["/create-account", "Create account"],
  ["/forgot-password", "Reset your password"],
  ["/reset-password?token=abc", "Choose a new password"],
];

const signedInPages: [string, string][] = [
  ["/", "Home"],
  ["/rounds", "Rounds"],
  [`/rounds/${roundId}`, "Oct 11, 2026"],
  ["/rounds/new", "Log round"],
  [`/rounds/${roundId}/edit`, "Edit round"],
  ["/practice", "Practice"],
  ["/practice/new", "Log practice"],
  [`/practice/${sessionId}`, "Putting"],
  ["/progress", "Progress"],
  ["/profile", "Profile"],
  ["/onboarding", "Which units do you use?"],
];

async function expectNoViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"]).analyze();
  // The list in the failure message says what to fix where.
  expect(
    results.violations.map((v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(" ")).join(", ")})`),
  ).toEqual([]);
}

for (const colorScheme of ["light", "dark"] as const) {
  test.describe(`${colorScheme} mode`, () => {
    test.use({ colorScheme });

    for (const [path, heading] of signedOutPages) {
      test(`signed out: ${path}`, async ({ page }) => {
        await mockApi(page, false);
        await page.goto(path);
        await expect(page.getByRole("heading", { level: 1, name: heading })).toBeVisible();
        await expectNoViolations(page);
      });
    }

    for (const [path, heading] of signedInPages) {
      test(`signed in: ${path}`, async ({ page }) => {
        await mockApi(page, true);
        await page.goto(path);
        await expect(page.getByRole("heading", { name: heading }).first()).toBeVisible();
        // Wait for content rather than skeletons.
        await expect(page.getByRole("status", { name: "Loading…" })).toHaveCount(0);
        await expectNoViolations(page);
      });
    }
  });
}

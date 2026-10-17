import { expect, test, type Page, type Route } from "@playwright/test";

/**
 * Progress (#107) in a real browser, against a mocked API: the charts draw, swap for
 * their tables, and a monthly review is created for the latest month with rounds.
 */
const userId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f";

const point = (i: number, playedAt: string, score: number, holes = 18) => ({
  roundId: `00000000-0000-0000-0000-${String(i).padStart(12, "0")}`,
  playedAt,
  holes,
  totalStrokes: holes === 18 ? 96 - i * 2 : 46,
  totalPutts: null,
  penalties: null,
  threePutts: null,
  blowUpHoles: null,
  fitnessScoreTotal: score,
  fitnessConfidence: i < 4 ? "Low" : "Medium",
  microWinTitle: null,
});

const timeline = [
  point(1, "2026-06-07", 48),
  point(2, "2026-07-12", 55),
  point(3, "2026-08-09", 52, 9),
  point(4, "2026-09-13", 61),
  point(5, "2026-10-04", 66),
  point(6, "2026-10-11", 70),
];

const months = [
  "2025-11",
  "2025-12",
  "2026-01",
  "2026-02",
  "2026-03",
  "2026-04",
  "2026-05",
  "2026-06",
  "2026-07",
  "2026-08",
  "2026-09",
  "2026-10",
].map((m) => {
  const inMonth = timeline.filter((p) => p.playedAt.startsWith(m));
  const avg = inMonth.length ? inMonth.reduce((s, p) => s + p.fitnessScoreTotal, 0) / inMonth.length : null;
  return {
    monthStart: `${m}-01`,
    fitnessAvg: avg,
    fitnessChange: null,
    roundsLogged: inMonth.length,
    highlights: [],
    hasReview: false,
  };
});

async function mockApi(page: Page) {
  const reviews: Record<string, unknown> = {};

  const json = (route: Route, status: number, body: unknown, headers: Record<string, string> = {}) =>
    route.fulfill({
      status,
      contentType: status >= 400 ? "application/problem+json" : "application/json",
      headers,
      body: JSON.stringify(body),
    });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");
    const key = `${request.method()} ${path}`;

    if (path === "/client-config")
      return json(route, 200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "1.0.0" } });
    if (path === "/auth/refresh")
      return json(route, 200, {
        userId,
        token: "t",
        refreshToken: null,
        expiresAt: new Date(Date.now() + 9e5).toISOString(),
      });
    if (key === "GET /me/rounds")
      return json(route, 200, timeline.slice(-1), { "X-Total-Count": String(timeline.length) });
    if (key === "GET /me/timeline") return json(route, 200, { userId, count: timeline.length, items: timeline });
    if (key === "GET /me/timeline/monthly")
      return json(route, 200, {
        userId,
        months: 12,
        items: months.map((m) => ({ ...m, hasReview: m.monthStart in reviews })),
      });
    if (key === "GET /me/baseline")
      return json(route, 200, {
        userId,
        windowRounds: 5,
        sampleSize: 5,
        avgScore: 89.2,
        scoreStdDev: 3.1,
        avgPutts: 33,
        avgPenalties: 1.4,
        avgThreePutts: null,
        avgBlowUpHoles: null,
        avgFairwaysHit: null,
        avgGir: null,
        completenessRatio: 0.5,
        confidenceLevel: "Medium",
        computedAt: "2026-10-11T10:00:00Z",
      });
    if (key === "POST /me/monthly-reviews/generate") {
      const body = request.postDataJSON() as { monthStart: string; focusNextMonth?: string };
      reviews[body.monthStart] = {
        monthlyReviewId: "77777777-7777-7777-7777-777777777777",
        userId,
        monthStart: body.monthStart,
        fitnessAvg: 68,
        fitnessChange: 7,
        focusNextMonth: body.focusNextMonth ?? null,
        highlights: [],
        computedAt: "2026-10-17T09:00:00Z",
      };
      return json(route, 200, reviews[body.monthStart]);
    }
    if (request.method() === "GET" && path.startsWith("/me/monthly-reviews/")) {
      const review = reviews[path.split("/").pop()!];
      return review ? json(route, 200, review) : json(route, 404, { status: 404 });
    }
    return json(route, 404, { status: 404 });
  });
}

test("see progress as charts and tables, and create a monthly review", async ({ page }) => {
  await mockApi(page);
  // Reduced motion: the charts draw at once instead of animating in.
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/progress");

  const timelineSection = page.getByRole("region", { name: "Rounds over time" });
  await expect(timelineSection.getByText("6 rounds from Jun 7, 2026 to Oct 11, 2026.", { exact: false })).toBeVisible();
  // Two lines drawn: fitness and 18-hole strokes.
  await expect(timelineSection.locator(".recharts-line")).toHaveCount(2);

  const monthly = page.getByRole("region", { name: "Monthly trend" });
  await expect(monthly.locator(".recharts-bar-rectangle path, .recharts-bar-rectangle rect").first()).toBeVisible();

  await timelineSection.getByRole("button", { name: "Show as table" }).click();
  await expect(timelineSection.getByRole("table").getByRole("row")).toHaveCount(7);
  await timelineSection.getByRole("button", { name: "Show as chart" }).click();
  await expect(timelineSection.locator(".recharts-line")).toHaveCount(2);

  const review = page.getByRole("region", { name: "Monthly review" });
  await expect(review.getByLabel("Month", { exact: true })).toHaveValue("2026-10-01");
  await review.getByLabel("Your focus for next month").fill("Fewer three-putts");
  await review.getByRole("button", { name: "Create review" }).click();
  await expect(review.getByRole("status")).toHaveText("Review ready.");
  await expect(review.getByText("Up 7 on the month before")).toBeVisible();

  await page.screenshot({ path: "test-results/progress.png", fullPage: true });
  await page.emulateMedia({ colorScheme: "dark", reducedMotion: "reduce" });
  await page.screenshot({ path: "test-results/progress-dark.png", fullPage: true });
});

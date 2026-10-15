import { defineConfig, devices } from "@playwright/test";

const port = 4173;

/**
 * End-to-end tests (#69): the production build in a real browser. The API is mocked per
 * test with `page.route` under the same origin (`/api`), so no backend is needed.
 */
export default defineConfig({
  testDir: "e2e",
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: `http://localhost:${port}`,
    trace: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: `npx vite build && npx vite preview --port ${port} --strictPort`,
    url: `http://localhost:${port}`,
    env: { VITE_API_BASE_URL: `http://localhost:${port}/api` },
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});

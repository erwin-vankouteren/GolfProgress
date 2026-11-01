/// <reference types="vitest/config" />
import react from "@vitejs/plugin-react";
import { readFileSync } from "node:fs";
import { defineConfig, searchForWorkspaceRoot } from "vite";

const { version } = JSON.parse(readFileSync(new URL("./package.json", import.meta.url), "utf8")) as { version: string };

export default defineConfig({
  plugins: [react()],
  define: {
    // Compared with GET /client-config's minimum web version (#66, #75).
    __APP_VERSION__: JSON.stringify(version),
  },
  build: {
    // For crash reports' stack traces (#119): the deploy uploads them to Sentry and then
    // deletes them, so they're never served. "hidden" leaves no sourceMappingURL comment.
    sourcemap: "hidden",
    rolldownOptions: {
      output: {
        // Sentry in a chunk of its own, loaded alongside the main one at start-up, so the
        // main bundle stays under 500 kB (routes.tsx).
        codeSplitting: { groups: [{ name: "sentry", test: /node_modules[\\/]@sentry/ }] },
      },
    },
  },
  server: {
    // The legal pages import their text from docs/legal (#121), outside this project.
    fs: { allow: [searchForWorkspaceRoot(process.cwd()), "../../docs/legal"] },
  },
  test: {
    environment: "jsdom",
    // e2e/ is Playwright's (npm run test:e2e).
    include: ["src/**/*.test.{ts,tsx}"],
    setupFiles: ["./src/test/setup.ts"],
  },
});

/// <reference types="vitest/config" />
import react from "@vitejs/plugin-react";
import { readFileSync } from "node:fs";
import { defineConfig } from "vite";

const { version } = JSON.parse(readFileSync(new URL("./package.json", import.meta.url), "utf8")) as { version: string };

export default defineConfig({
  plugins: [react()],
  define: {
    // Compared with GET /client-config's minimum web version (#66, #75).
    __APP_VERSION__: JSON.stringify(version),
  },
  test: {
    environment: "jsdom",
    // e2e/ is Playwright's (npm run test:e2e).
    include: ["src/**/*.test.{ts,tsx}"],
    setupFiles: ["./src/test/setup.ts"],
  },
});

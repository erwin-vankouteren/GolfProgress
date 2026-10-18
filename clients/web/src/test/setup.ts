import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

// Vitest runs without globals here, so Testing Library can't register this itself. The
// remembered language goes too, so one test's choice doesn't start the next in Dutch.
afterEach(() => {
  cleanup();
  localStorage.clear();
});

// jsdom has no ResizeObserver, which Recharts' ResponsiveContainer needs (#107). Charts
// then draw at zero size, and tests read the summary and the table instead.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
};

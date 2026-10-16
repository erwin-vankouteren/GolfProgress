import { useSyncExternalStore } from "react";

/** Desktop: wide enough for the sidebar, a dialog, or a list beside its detail. */
export const wideScreen = "(min-width: 901px)";

/**
 * Whether a CSS media query matches; re-renders when that changes. False where
 * matchMedia isn't available (jsdom), which gives the phone layout.
 */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (onChange) => {
      const list = window.matchMedia?.(query);
      list?.addEventListener("change", onChange);
      return () => list?.removeEventListener("change", onChange);
    },
    () => window.matchMedia?.(query).matches ?? false,
  );
}

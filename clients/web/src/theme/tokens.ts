// Generated from design/tokens.json by design/build-tokens.mjs (#73). Don't edit by hand.

export const colorNames = ["background", "surface", "surfaceVariant", "border", "borderStrong", "textPrimary", "textSecondary", "primary", "onPrimary", "primaryContainer", "onPrimaryContainer", "error", "onError", "success", "warning", "info", "pillarExecution", "pillarDamageControl", "pillarConsistency", "pillarHabits", "confidenceLow", "confidenceMedium", "confidenceHigh"] as const;
export type ColorName = (typeof colorNames)[number];

/** CSS custom property for a colour, e.g. cssColor("primary") -> "var(--color-primary)". */
export const cssColor = (name: ColorName) => `var(--color-${name.replace(/[A-Z]/g, (c) => "-" + c.toLowerCase())})`;

export const spacing = { xxs: 2, xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32, xxxl: 48 } as const;
export const radius = { sm: 4, md: 8, lg: 16, full: 999 } as const;
export const touchTarget = 44;

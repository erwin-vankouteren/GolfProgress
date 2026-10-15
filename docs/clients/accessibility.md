# Accessibility and localisation checklist

Every screen on web, iOS and Android meets this checklist before its issue is closed
(#78). It's part of "done", not a later pass. The M3 accessibility audit (#120) checks the
whole app against it again.

The target is **WCAG 2.1 AA** on the web, plus the equivalent platform guidelines: Apple's
Human Interface Guidelines and Android's accessibility guidelines.

## The checklist

| # | Check | Web | iOS | Android |
|---|---|---|---|---|
| 1 | **Contrast:** text 4.5:1, graphics and input borders 3:1, in light and dark | Use token colours only | Use `DesignTokens.Colors` only | Use the theme and `DesignTokens` only |
| 2 | **Text scales** with the user's setting and nothing is cut off at the largest size | `rem` sizes from the tokens; check at 200% browser zoom | Dynamic Type text styles; check at the largest accessibility size | `sp` sizes; check at 200% font scale |
| 3 | **Screen readers** announce every control with a name, role and state | Semantic HTML first; `aria-*` only where HTML can't say it | `accessibilityLabel` on icon-only controls | `contentDescription` on icon-only controls |
| 4 | **Charts and images** have a text alternative: a one-sentence summary, plus a table where data matters | Summary text + "Show as table" | `accessibilityChartDescriptor` / summary label | Content description with the summary |
| 5 | **Headings** mark each screen's title and sections | `<h1>`–`<h3>` in order | `.accessibilityAddTraits(.isHeader)` where the title isn't a navigation title | `Modifier.semantics { heading() }` |
| 6 | **Focus order** follows the visual order; after navigating, focus lands on the new content | Tab order; focus moves to `<main>` on route change | Default SwiftUI order | Default Compose order |
| 7 | **Focus is visible** | `:focus-visible` ring (global.css) | System | System |
| 8 | **Touch targets** are at least 44×44 pt (iOS, web) / 48×48 dp (Android) | `var(--touch-target)` | `DesignTokens.touchTarget` | `DesignTokens.touchTarget` |
| 9 | **Colour is never the only signal:** a pillar, confidence or error also has text or an icon | Label next to the colour | Label next to the colour | Label next to the colour |
| 10 | **Motion** respects "reduce motion" | `prefers-reduced-motion` (global.css) | `accessibilityReduceMotion` | `Settings.Global.ANIMATOR_DURATION_SCALE` |
| 11 | **Errors** say what's wrong and how to fix it, next to the field, and are announced | `aria-describedby` + `aria-invalid` | Text below the field + `accessibilityHint` | `supportingText` + `isError` |

## Localisation

The apps ship in English only, but **adding a language must only need translations**:

| Rule | Web | iOS | Android |
|---|---|---|---|
| No user-facing text in code | `src/i18n/messages.ts`, through react-intl | `LocalizedStringKey` → `Localizable.xcstrings` | `res/values/strings.xml` |
| Plurals and numbers through the platform's rules, never string concatenation | ICU `{count, plural, …}` | String Catalog plural variants | `<plurals>` |
| Dates, numbers and units formatted for the user's locale | `Intl.*` / `FormattedDate` | `FormatStyle` | `DateTimeFormatter`, `NumberFormat` |
| Layout survives longer text (German is about 30% longer) and right-to-left | Flexible layout, no fixed widths on text | Same | `supportsRtl="true"`; `start`/`end`, not `left`/`right` |

## What's automated

| Check | How |
|---|---|
| Contrast of every token colour pair | `design/build-tokens.mjs` fails the build below 4.5:1 / 3:1 (Design CI) |
| Web: common accessibility mistakes | `eslint-plugin-jsx-a11y` in `npm run lint` (Web CI) |
| Web: landmarks, headings, names, focus after navigation | Testing Library queries by role and name (`App.test.tsx`) |
| Android: headings, tab roles, content descriptions, layout at 200% font scale | Compose UI tests on Robolectric (`AppShellTest.kt`) |
| iOS: the shell at the largest Dynamic Type size | SwiftUI previews in `MainTabView.swift`, built in iOS CI |

Screen readers themselves (VoiceOver, TalkBack, NVDA) can't be automated here; test with
them by hand before closing a feature issue. Tests that query by role, name and heading
are the closest automated stand-in, because those are exactly what a screen reader reads.

## Status of the app shells (#75–#77)

| | Web | iOS | Android |
|---|---|---|---|
| All text localisable | react-intl, `messages.ts` | String Catalog | `strings.xml` |
| Accessibility linting or tests | `jsx-a11y`; role-based tests | Previews at the largest text size | Role, heading and 200% font-scale tests |
| Headings, labelled icon buttons, touch targets, visible focus, token colours | ✓ | ✓ | ✓ |
| Skip link and focus after navigating | ✓ (skip link, focus on `<main>`) | n/a (system navigation) | n/a (system navigation) |

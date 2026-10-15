# Design system

Design tokens shared by the web, iOS and Android clients (#73). [`tokens.json`](tokens.json)
is the single source for:
- colours, in light and dark, including one per score pillar and one per confidence level;
- typography, mapped to each platform's scalable text styles;
- spacing, corner radii and elevation;
- the minimum touch-target size.

Screens use tokens only, never literal colours or sizes, so the three apps look the same
and support dark mode from the start.

Background: [`docs/architecture.md`](../docs/architecture.md) → "Design system".

## Changing a token

```bash
node design/build-tokens.mjs            # checks contrast, then regenerates each client's file
node design/build-tokens.mjs --report   # also prints the contrast of every pair
```

Commit `tokens.json` together with the regenerated files. They're **generated, not kept in
sync by hand**, and CI fails if they differ from `tokens.json`.

| Client | Generated file | Used as |
|---|---|---|
| Web | `clients/web/src/theme/tokens.css`, `tokens.ts` | CSS custom properties (`var(--color-primary)`); dark mode follows the system unless `data-theme` is set |
| iOS | `clients/ios/GolfProgress/DesignSystem/DesignTokens.swift` | `DesignTokens.Colors.primary` (switches with light and dark mode), `DesignTokens.Typography.body` (Dynamic Type) |
| Android | `clients/android/app/src/main/kotlin/.../ui/theme/DesignTokens.kt` | `DesignTokens.Light` / `DesignTokens.Dark` palettes for the Compose theme; text styles in `sp` |

## Contrast is checked, not assumed

`tokens.json` → `contrast` lists every colour pair the apps put together:
- **Text** must reach WCAG 2.1 AA **4.5:1**.
- **Graphics** (chart marks, input borders, icons) must reach **3:1**.

The build checks every pair in both themes and **refuses to generate anything if one
fails**. When you use two colours together in a new way, add the pair here first.

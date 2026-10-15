#!/usr/bin/env node
// Builds each client's design tokens from design/tokens.json (#73).
//
//   node design/build-tokens.mjs           check contrast, then write the generated files
//   node design/build-tokens.mjs --check   check contrast and that the generated files are
//                                          up to date, without writing (CI)
//
// Contrast is checked for every pair declared in tokens.json, in both themes: text must
// reach WCAG 2.1 AA 4.5:1, graphics 3:1. A failing pair stops the build, so an
// inaccessible colour can't reach any client. No dependencies: plain Node.

import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const tokens = JSON.parse(readFileSync(join(root, "design/tokens.json"), "utf8"));
const check = process.argv.includes("--check");

const HEADER = "Generated from design/tokens.json by design/build-tokens.mjs (#73). Don't edit by hand.";
const themes = ["light", "dark"];
const colorNames = Object.keys(tokens.color.light);
const typography = Object.entries(tokens.typography).filter(([k]) => !k.startsWith("$"));

// ---------------------------------------------------------------- validation

function fail(message) {
  console.error(`design tokens: ${message}`);
  process.exitCode = 1;
}

for (const theme of themes) {
  const missing = colorNames.filter((n) => !(n in tokens.color[theme]));
  const extra = Object.keys(tokens.color[theme]).filter((n) => !colorNames.includes(n));
  if (missing.length || extra.length) fail(`${theme} theme differs from light: missing [${missing}] extra [${extra}]`);
  for (const [name, hex] of Object.entries(tokens.color[theme])) {
    if (!/^#[0-9A-Fa-f]{6}$/.test(hex)) fail(`${theme}.${name} must be #RRGGBB, got ${hex}`);
  }
}

function luminance(hex) {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
  const lin = (c) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
}

export function contrast(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

const report = [];
for (const [kind, minimum] of [["text", 4.5], ["graphics", 3]]) {
  for (const [fg, bg] of tokens.contrast[kind]) {
    for (const theme of themes) {
      const ratio = contrast(tokens.color[theme][fg], tokens.color[theme][bg]);
      const ok = ratio >= minimum;
      report.push(`${ok ? "ok  " : "FAIL"} ${kind.padEnd(8)} ${theme.padEnd(5)} ${fg} on ${bg}: ${ratio.toFixed(2)} (needs ${minimum})`);
      if (!ok) fail(`${theme}: ${fg} on ${bg} is ${ratio.toFixed(2)}:1, below ${minimum}:1 for ${kind}`);
    }
  }
}
if (process.argv.includes("--report") || process.exitCode) console.log(report.join("\n"));
if (process.exitCode) process.exit();

// ---------------------------------------------------------------- generators

const kebab = (s) => s.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`);
const pascal = (s) => s[0].toUpperCase() + s.slice(1);
const scale = (group) => Object.entries(tokens[group]).filter(([k]) => !k.startsWith("$"));

function css() {
  const vars = (theme) => colorNames.map((n) => `  --color-${kebab(n)}: ${tokens.color[theme][n]};`).join("\n");
  const shared = [
    ...scale("spacing").map(([k, v]) => `  --space-${k}: ${v}px;`),
    ...scale("radius").map(([k, v]) => `  --radius-${k}: ${v}px;`),
    // rem, so text scales with the browser's font-size setting (16px base).
    ...typography.flatMap(([k, t]) => [
      `  --font-size-${k}: ${t.size / 16}rem;`,
      `  --line-height-${k}: ${t.lineHeight / 16}rem;`,
      `  --font-weight-${k}: ${t.weight};`,
    ]),
    `  --touch-target: ${tokens.touchTarget.web}px;`,
  ].join("\n");

  return `/* ${HEADER} */

:root {
  color-scheme: light dark;
${shared}
${vars("light")}
}

@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
${vars("dark").replace(/^/gm, "  ")}
  }
}

:root[data-theme="dark"] {
${vars("dark")}
}
`;
}

function ts() {
  const obj = (entries) => `{ ${entries.map(([k, v]) => `${k}: ${v}`).join(", ")} }`;
  return `// ${HEADER}

export const colorNames = [${colorNames.map((n) => `"${n}"`).join(", ")}] as const;
export type ColorName = (typeof colorNames)[number];

/** CSS custom property for a colour, e.g. cssColor("primary") -> "var(--color-primary)". */
export const cssColor = (name: ColorName) => \`var(--color-\${name.replace(/[A-Z]/g, (c) => "-" + c.toLowerCase())})\`;

export const spacing = ${obj(scale("spacing"))} as const;
export const radius = ${obj(scale("radius"))} as const;
export const touchTarget = ${tokens.touchTarget.web};
`;
}

function swift() {
  const hex = (h) => `0x${h.slice(1).toUpperCase()}`;
  const colors = colorNames
    .map((n) => `        static let ${n} = SwiftUI.Color(light: ${hex(tokens.color.light[n])}, dark: ${hex(tokens.color.dark[n])})`)
    .join("\n");
  const cg = (group) => scale(group).map(([k, v]) => `        static let ${k}: CGFloat = ${v}`).join("\n");
  const weight = { 400: "regular", 500: "medium", 600: "semibold", 700: "bold" };
  const fonts = typography
    .map(([k, t]) => `        static let ${k} = Font.system(.${t.ios}).weight(.${weight[t.weight]})`)
    .join("\n");

  return `// ${HEADER}

import SwiftUI
import UIKit

enum DesignTokens {
    /// Colours that follow the system light/dark setting.
    enum Colors {
${colors}
    }

    enum Spacing {
${cg("spacing")}
    }

    enum Radius {
${cg("radius")}
    }

    /// Text styles built on Dynamic Type, so they scale with the user's text size.
    enum Typography {
${fonts}
    }

    static let touchTarget: CGFloat = ${tokens.touchTarget.ios}
}

extension SwiftUI.Color {
    init(light: UInt32, dark: UInt32) {
        self.init(uiColor: UIColor { traits in
            UIColor(rgb: traits.userInterfaceStyle == .dark ? dark : light)
        })
    }
}

private extension UIColor {
    convenience init(rgb: UInt32) {
        self.init(
            red: CGFloat((rgb >> 16) & 0xFF) / 255,
            green: CGFloat((rgb >> 8) & 0xFF) / 255,
            blue: CGFloat(rgb & 0xFF) / 255,
            alpha: 1
        )
    }
}
`;
}

function kotlin() {
  const argb = (h) => `Color(0xFF${h.slice(1).toUpperCase()})`;
  const palette = (theme) => colorNames.map((n) => `        val ${n} = ${argb(tokens.color[theme][n])}`).join("\n");
  const dp = (group) => scale(group).map(([k, v]) => `        val ${k} = ${v}.dp`).join("\n");
  const weight = { 400: "Normal", 500: "Medium", 600: "SemiBold", 700: "Bold" };
  const styles = typography
    .map(([k, t]) => `        val ${k} = TextStyle(fontSize = ${t.size}.sp, lineHeight = ${t.lineHeight}.sp, fontWeight = FontWeight.${weight[t.weight]})`)
    .join("\n");

  return `// ${HEADER}

package com.golfprogress.app.ui.theme

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

object DesignTokens {
    /** The colours of one theme; [Light] and [Dark] have the same names. */
    interface Palette {
${colorNames.map((n) => `        val ${n}: Color`).join("\n")}
    }

    object Light : Palette {
${palette("light").replace(/        val /g, "        override val ")}
    }

    object Dark : Palette {
${palette("dark").replace(/        val /g, "        override val ")}
    }

    object Spacing {
${dp("spacing")}
    }

    object Radius {
${dp("radius")}
    }

    /** In sp, so text scales with the user's font size. */
    object Typography {
${styles}
    }

    val touchTarget = ${tokens.touchTarget.android}.dp
}
`;
}

const outputs = {
  "clients/web/src/theme/tokens.css": css(),
  "clients/web/src/theme/tokens.ts": ts(),
  "clients/ios/GolfProgress/DesignSystem/DesignTokens.swift": swift(),
  "clients/android/app/src/main/kotlin/com/golfprogress/app/ui/theme/DesignTokens.kt": kotlin(),
};

for (const [path, content] of Object.entries(outputs)) {
  const file = join(root, path);
  if (check) {
    const current = existsSync(file) ? readFileSync(file, "utf8").replace(/\r\n/g, "\n") : null;
    if (current !== content) fail(`${path} is out of date. Run \`node design/build-tokens.mjs\` and commit the result.`);
  } else {
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, content);
    console.log(`wrote ${relative(root, file)}`);
  }
}

if (check && !process.exitCode) console.log("design tokens: contrast ok, generated files up to date");

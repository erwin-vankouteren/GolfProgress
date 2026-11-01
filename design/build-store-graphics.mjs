#!/usr/bin/env node
// Builds Google Play's feature graphic (1024 × 500), in English and Dutch, from the app
// icon's glyph and the brand colours.
//
//   node design/build-store-graphics.mjs
//
// Renders with the web client's Playwright Chromium, like build-icons.mjs. The output is
// committed and uploaded by hand in Play Console (docs/clients/play-store-listing.md).
// JPEG, because Play refuses a feature graphic with an alpha channel.

import { readFileSync, mkdirSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const { chromium } = createRequire(join(root, "clients/web/package.json"))("playwright");

const { color } = JSON.parse(readFileSync(join(root, "design/tokens.json"), "utf8"));
const glyph = readFileSync(join(root, "design/app-icon/glyph.svg"), "utf8").match(/<svg[^>]*>([\s\S]*)<\/svg>/)[1].trim();

const graphics = [
  {
    path: "design/store/play-feature-graphic.jpg",
    headline: "See your progress beyond the score",
    line: "Log a round in a minute. Practice counts too.",
  },
  {
    path: "design/store/play-feature-graphic-nl.jpg",
    headline: "Zie je voortgang verder dan de score",
    line: "Ronde invoeren in een minuut. Training telt mee.",
  },
];

// Play may lay a play button or the app's name over the edges, so the text stays well
// inside them.
const html = ({ headline, line }) => `<!doctype html>
<html><body style="margin:0">
<div style="width:1024px;height:500px;box-sizing:border-box;padding:0 72px;display:flex;align-items:center;gap:48px;
  background:${color.light.primary};color:${color.light.onPrimary};font-family:'Segoe UI',system-ui,sans-serif">
  <svg viewBox="0 0 1024 1024" width="260" height="260" style="flex:none">${glyph}</svg>
  <div>
    <div style="font-size:30px;font-weight:600;opacity:.85;margin-bottom:12px">GolfProgress</div>
    <div style="font-size:54px;font-weight:700;line-height:1.1;margin-bottom:20px">${headline}</div>
    <div style="font-size:26px;line-height:1.3;color:${color.light.primaryContainer}">${line}</div>
  </div>
</div>
</body></html>`;

const browser = await chromium.launch();
try {
  const page = await browser.newPage({ viewport: { width: 1024, height: 500 } });
  for (const graphic of graphics) {
    await page.setContent(html(graphic));
    mkdirSync(dirname(join(root, graphic.path)), { recursive: true });
    await page.screenshot({ path: join(root, graphic.path), type: "jpeg", quality: 95 });
    console.log(`wrote ${graphic.path}`);
  }
} finally {
  await browser.close();
}

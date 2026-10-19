#!/usr/bin/env node
// Builds the app icon for each client from design/app-icon/glyph.svg.
//
//   node design/build-icons.mjs
//
// Renders with the web client's Playwright Chromium, so run `npm ci` and
// `npx playwright install chromium` in clients/web first. The output is committed:
// run this only after changing the glyph or the brand green, and commit the results.
//
// Android is not rendered here: its launcher icon is an adaptive vector drawable
// (res/drawable/ic_launcher_*.xml) that mirrors the glyph's shapes by hand.

import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { crc32, deflateSync } from "node:zlib";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const { chromium } = createRequire(join(root, "clients/web/package.json"))("playwright");

const green = JSON.parse(readFileSync(join(root, "design/tokens.json"), "utf8")).color.light.primary;
const glyph = readFileSync(join(root, "design/app-icon/glyph.svg"), "utf8").match(/<svg[^>]*>([\s\S]*)<\/svg>/)[1].trim();

/**
 * The glyph on the brand green. `corner` rounds the square (0 for platforms that apply
 * their own mask); `scale` shrinks the glyph about the centre, to fit a mask's safe zone.
 */
function icon({ corner = 0, scale = 1 } = {}) {
  const offset = 512 * (1 - scale);
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024">
  <rect width="1024" height="1024" rx="${corner}" fill="${green}"/>
  <g transform="translate(${offset} ${offset}) scale(${scale})">
    ${glyph}
  </g>
</svg>
`;
}

const rounded = { corner: 224 };

const svgs = {
  // The favicon, and the logo in the web app's header and welcome page.
  "clients/web/public/favicon.svg": icon(rounded),
};

const pngs = [
  // iOS and the stores apply their own rounded mask, and iOS refuses transparency.
  { path: "clients/ios/GolfProgress/Resources/Assets.xcassets/AppIcon.appiconset/AppIcon.png", size: 1024, svg: icon() },
  { path: "design/app-icon/play-store-512.png", size: 512, svg: icon() },
  { path: "clients/web/public/apple-touch-icon.png", size: 180, svg: icon() },
  { path: "clients/web/public/icon-192.png", size: 192, svg: icon(rounded) },
  { path: "clients/web/public/icon-512.png", size: 512, svg: icon(rounded) },
  // Android masks installed web apps to any shape; the glyph stays inside the 80% safe zone.
  { path: "clients/web/public/icon-maskable-512.png", size: 512, svg: icon({ scale: 0.9 }) },
];

function write(path, content) {
  mkdirSync(dirname(join(root, path)), { recursive: true });
  writeFileSync(join(root, path), content);
  console.log(`wrote ${path}`);
}

for (const [path, svg] of Object.entries(svgs)) write(path, svg);

/**
 * Encodes RGBA pixels as a PNG: RGB when every pixel is opaque, because the App Store
 * refuses an icon with an alpha channel, and RGBA otherwise.
 */
function png(size, rgba) {
  const opaque = rgba.every((value, i) => i % 4 !== 3 || value === 255);
  const channels = opaque ? 3 : 4;
  const rows = Buffer.alloc(size * (1 + size * channels));
  for (let y = 0, o = 0; y < size; y++) {
    rows[o++] = 0; // filter: none
    for (let x = 0; x < size; x++) {
      const i = (y * size + x) * 4;
      for (let c = 0; c < channels; c++) rows[o++] = rgba[i + c];
    }
  }
  const chunk = (type, data) => {
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const body = Buffer.concat([Buffer.from(type, "ascii"), data]);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(body));
    return Buffer.concat([length, body, crc]);
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(size, 0);
  header.writeUInt32BE(size, 4);
  header[8] = 8; // bits per channel
  header[9] = opaque ? 2 : 6; // RGB or RGBA
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", header),
    chunk("IDAT", deflateSync(rows, { level: 9 })),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  for (const { path, size, svg } of pngs) {
    const rgba = await page.evaluate(
      async ({ svg, size }) => {
        const image = new Image();
        image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
        await image.decode();
        const canvas = document.createElement("canvas");
        canvas.width = canvas.height = size;
        const context = canvas.getContext("2d");
        context.drawImage(image, 0, 0, size, size);
        const bytes = context.getImageData(0, 0, size, size).data;
        let binary = "";
        for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
        return btoa(binary);
      },
      { svg, size },
    );
    write(path, png(size, Buffer.from(rgba, "base64")));
  }
} finally {
  await browser.close();
}

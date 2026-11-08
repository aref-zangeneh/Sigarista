#!/usr/bin/env node
/**
 * ESC/POS structural validation of render.py output (no npm deps):
 *   node tests/escposStructure.test.mjs
 *
 * render.py turns each receipt copy into: ESC @ + GS v 0 raster + ESC d feed
 * + GS V cut. The agent concatenates copies with Buffer.concat and sends the
 * stream to the printer with `lp -o raw`. These assertions hold the byte
 * stream to its contract so a regeneration change (or a broken copy build)
 * fails here instead of on paper:
 *   - every copy starts with ESC @ (expense: MID-STREAM head state resets)
 *   - every raster header is followed by exact w*h bytes — nothing truncated
 *   - every copy ends with GS V (cut)
 */

import { execFileSync } from "node:child_process";
import { existsSync, writeFileSync, mkdirSync, mkdtempSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const base = dirname(fileURLToPath(import.meta.url));
const rootDir = dirname(base);
const fixtures = join(base, "fixtures");
const renderPy = join(rootDir, "render.py");
const venvPython = join(rootDir, "venv", "bin", "python");

if (!existsSync(venvPython)) {
  console.error("⚠ venv python missing — run Sugarista-PrintAgent/install.sh; ESC/POS structure test SKIPPED.");
  process.exit(0);
}

const tmp = mkdtempSync(join(tmpdir(), "sugarista-escpos-test-"));
let failures = [];

function renderCopy(type) {
  const payloadPath = join(tmp, `${type}.json`);
  const outPath = join(tmp, `${type}.escpos`);
  writeFileSync(payloadPath, readFileSync(join(fixtures, `${type}-payload.json`), "utf8"));
  execFileSync(venvPython, [renderPy, payloadPath, outPath], { stdio: "pipe" });
  return readFileSync(outPath);
}

/* ── raster-stream walkthrough ─────────────────────────────────────────── */

function walkStream(stream) {
  // Returns { rasters: [{offset, width, height}], resets, cuts, feedD } and
  // throws when a raster header is truncated/malformed.
  let i = 0;
  const rasters = [];
  const resets = [];
  const cuts = [];
  const feeds = [];
  while (i < stream.length) {
    const next = stream.indexOf(0x1d, i);
    if (next === -1) break;
    if (next + 1 >= stream.length) break;
    const cmd = stream[next + 1];

    if (cmd === 0x76 && stream[next + 2] === 0x30 && stream[next + 3] === 0x00) {
      if (next + 7 > stream.length) throw new Error(`truncated raster header at ${next}`);
      const width = stream.readUInt16LE(next + 4);
      const height = stream.readUInt16LE(next + 6);
      if (width <= 0 || height <= 0) throw new Error(`illegible raster extent at ${next}: ${width}x${height}`);
      const rasterEnd = next + 8 + width * height;
      if (rasterEnd > stream.length) throw new Error(`truncated raster data at ${next}: needs ${rasterEnd}, stream ${stream.length}`);
      rasters.push({ offset: next, width, height });
      i = rasterEnd;
      continue;
    }

    if (cmd === 0x56 && stream[next + 2] === 0x00) {
      cuts.push(next);
      i = next + 3;
      continue;
    }

    if (cmd === 0x28) {
      // GS ( ... — skip the ( k ) ( p) style blocks by their length prefix
      const len = stream.readUInt16LE(next + 2);
      i = next + 4 + len;
      continue;
    }

    i = next + 1;
  }
  return { rasters, cuts };
}

/* ── the actual assertions ─────────────────────────────────────────────── */

try {
  const customer = renderCopy("customer");
  const barista = renderCopy("barista");

  for (const [label, stream] of [["customer", customer], ["barista", barista]]) {
    if (stream[0] !== 0x1b || stream[1] !== 0x40) {
      failures.push(`${label}: stream does not start with ESC @ (init)`);
    }
    if (!stream.subarray(-3).equals(Buffer.from([0x1d, 0x56, 0x00]))) {
      failures.push(`${label}: stream does not end with GS V (cut)`);
    }
    const walk = walkStream(stream);
    if (walk.rasters.length !== 1) {
      failures.push(`${label}: expected exactly 1 raster, got ${walk.rasters.length}`);
    }
    const raster = walk.rasters[0];
    if (raster && raster.width !== 72) {
      failures.push(`${label}: raster line width ${raster.width} bytes — expected 72 (576 dot head)`);
    }
    if (raster && raster.height < 10) {
      failures.push(`${label}: suspiciously short raster height ${raster.height}`);
    }
  }

  // THE regression: both copies concatenated must open with ESC @ each, so an
  // interrupted/dirty head state never leaks into the second copy's pixels.
  const combined = Buffer.concat([customer, barista]);
  if (combined[0] !== 0x1b || combined[1] !== 0x40) {
    failures.push("combined: first copy missing ESC @ init");
  }
  const baristaOffset = customer.length;
  if (combined[baristaOffset] !== 0x1b || combined[baristaOffset + 1] !== 0x40) {
    failures.push("combined: second copy does not start with ESC @ — dirty head state would bleed into copy 2");
  }

  const walk = walkStream(combined);
  if (walk.rasters.length !== 2) {
    failures.push(`combined: expected 2 rasters (one per copy), got ${walk.rasters.length}`);
  }
  if (walk.cuts.length !== 2) {
    failures.push(`combined: expected 2 cuts (one per copy), got ${walk.cuts.length}`);
  }

  console.log(
    `✓ ESC/POS structure OK — customer ${customer.length}B (raster ${walk.rasters[0]?.width}x${walk.rasters[0]?.height}px), ` +
    `barista ${barista.length}B (raster ${walk.rasters[1]?.width}x${walk.rasters[1]?.height}px), ` +
    `combined ${combined.length}B; full reset before each copy.`
  );
} catch (error) {
  failures.push(`renderer failed: ${error.message}`);
} finally {
  try {
    execFileSync("rm", ["-rf", tmp]);
  } catch {}
}

if (failures.length) {
  console.error(`✗ ESC/POS structure test FAILED:\n`);
  for (const failure of failures) console.error(`  • ${failure}`);
  process.exit(1);
}

"use strict";

/**
 * Unit tests for agent-helpers.cjs (run with plain node — no dependencies):
 *   node tests/agentHelpers.test.cjs
 *
 * These lock in the CUPS-output parsing the agent now relies on for its
 * "has the job actually drained?" verification — the fix for receipts that
 * printed as garbage after sitting in a disabled queue while the database
 * already said printed.
 */

const assert = require("node:assert");
const { parseLpRequestId, listQueuedJobIds, isQueueDisabled } = require("../agent-helpers.cjs");

let passed = 0;
function test(name, fn) {
  try {
    fn();
    passed += 1;
  } catch (error) {
    console.error(`✗ ${name}\n  ${error.message}`);
    process.exit(1);
  }
}

/* ── parseLpRequestId ──────────────────────────────────────────────────── */

test("parseLpRequestId extracts the job id from lp stdout", () => {
  const out = "request id is GA-E200I-79 (1 file(s))\n";
  assert.strictEqual(parseLpRequestId(out), "GA-E200I-79");
});

test("parseLpRequestId handles lp's own error-prefixed wording", () => {
  const out = "lp: request id is GA-E200I-104 (2 file(s))\n";
  assert.strictEqual(parseLpRequestId(out), "GA-E200I-104");
});

test("parseLpRequestId returns null when there is no job id", () => {
  assert.strictEqual(parseLpRequestId("lp: no such file"), null);
  assert.strictEqual(parseLpRequestId(""), null);
  assert.strictEqual(parseLpRequestId(undefined), null);
});

test("parseLpRequestId stops at the first whitespace-like char (not every paren)", () => {
  const out = "request id is GA-E200I-9 (1 file(s))\nsending to printer";
  const id = parseLpRequestId(out);
  assert.strictEqual(id, "GA-E200I-9");
});

/* ── listQueuedJobIds ──────────────────────────────────────────────────── */

test("listQueuedJobIds reads lpstat -o rows", () => {
  const out =
    "GA-E200I-79              arefzangeneh     86016   Sun Nov  8 15:25:21 2026\n" +
    "GA-E200I-78              arefzangeneh     86016   Sun Nov  8 14:26:34 2026\n";
  assert.deepStrictEqual(listQueuedJobIds(out), ["GA-E200I-79", "GA-E200I-78"]);
});

test("listQueuedJobIds tolerates real binary timing differences and empty output", () => {
  assert.deepStrictEqual(listQueuedJobIds(""), []);
  assert.deepStrictEqual(listQueuedJobIds("\n"), []);
  // single streaming row, no trailing newline
  assert.deepStrictEqual(listQueuedJobIds("GA-E200I-99   root  100"), ["GA-E200I-99"]);
});

test("listQueuedJobIds ignores header/comment rows without a numeric suffix", () => {
  const out = "lpstat: Printer not responding\nGA-E200I-77   arefzangeneh   99328";
  assert.deepStrictEqual(listQueuedJobIds(out), ["GA-E200I-77"]);
});

/* ── isQueueDisabled ───────────────────────────────────────────────────── */

test("isQueueDisabled only trips on disabled wording", () => {
  assert.strictEqual(isQueueDisabled("printer GA-E200I is idle.  enabled since Sun Nov  8 15:25:21"), false);
  assert.strictEqual(isQueueDisabled("printer GA-E200I disabled since Sun Nov  8 14:05:57 -\n\tUSB port busy"), true);
  assert.strictEqual(isQueueDisabled("printer GA-E200I DISABLED (paused)"), true);
  assert.strictEqual(isQueueDisabled(null), false);
});

console.log(`✓ agent-helpers: ${passed}/${passed} tests passed`);

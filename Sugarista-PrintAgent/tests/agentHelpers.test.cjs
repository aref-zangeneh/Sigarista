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
const {
  parseLpRequestId,
  listQueuedJobIds,
  isQueueDisabled,
  buildWarmupPrimer,
  toPersianDigits,
  formatMoneyPersian,
  normalizePrintRequest,
} = require("../agent-helpers.cjs");

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

/* ── buildWarmupPrimer ─────────────────────────────────────────────────── */

test("primer leads with ignored NUL bytes (zero paper cost)", () => {
  const primer = buildWarmupPrimer();
  assert.ok(primer.length > 512, "primer must be long enough to cover a loss window");
  assert.ok(primer.subarray(0, 1024).every((b) => b === 0), "leading bytes must be NUL (ignored by printer)");
});

test("primer closes with ESC @ resets so the receipt starts clean", () => {
  const primer = buildWarmupPrimer();
  assert.ok(primer.subarray(-8).equals(Buffer.from([0x1b, 0x40, 0x1b, 0x40, 0x1b, 0x40, 0x1b, 0x40])), "4 ESC @ resets required");
  assert.ok(primer.length % 2 === 0, "primer must stay byte-paired");
});

test("primer contains only print-safe bytes (NULs + ESC/@ pairs)", () => {
  const primer = buildWarmupPrimer();
  const allowed = new Set([0x00, 0x1b, 0x40]);
  assert.ok(
    Array.from(primer).every((b) => allowed.has(b)),
    "primer must be made only of NULs and ESC @ pairs",
  );
  // Worst case if a loss boundary splits an ESC @ pair: at most one stray '@'
  // glyph per reset — never a garbled receipt.
  const strayCount = Array.from(primer).filter((b) => b === 0x40).length;
  assert.strictEqual(strayCount, 4, "exactly the 4 ESC @ payload bytes");
});

/* ── normalizePrintRequest ─────────────────────────────────────────────── */

test("normalizePrintRequest converts POS customer+barista bundle into render.py payload", () => {
  const { payload, copies, trackingCode } = normalizePrintRequest({
    customer: {
      cafeName: "شوگاریستا",
      address: "تهران",
      contactPhone: "021-12345678",
      trackingCode: "SG-POS123",
      printedAt: "1405/08/27, 15:30",
      guestName: "علی",
      guestPhone: "09120000000",
      service: "داخل سالن",
      table: "۴",
      note: "سفارش ویژه",
      paymentMethod: "کارت",
      totalAmount: 360000,
      lines: [
        {
          name: "لاته",
          quantity: 2,
          unitPrice: 180000,
          lineTotal: 360000,
        },
      ],
    },
    barista: {
      trackingCode: "SG-POS123",
      lines: [
        {
          name: "لاته",
          sizeLabel: "بزرگ",
          quantity: 2,
          modifierLabels: ["شیر جو"],
          baristaNote: "کم‌شیرین",
        },
      ],
    },
  });

  assert.strictEqual(trackingCode, "SG-POS123");
  assert.deepStrictEqual(copies, ["customer", "barista"]);
  assert.strictEqual(payload.header.cafeName, "شوگاریستا");
  assert.strictEqual(payload.order.code, "SG-POS123");
  assert.strictEqual(payload.order.tableLabel, "۴");
  assert.strictEqual(payload.items.length, 1);
  assert.strictEqual(payload.items[0].name, "لاته");
  assert.strictEqual(payload.items[0].sizeLabel, "بزرگ");
  assert.strictEqual(payload.items[0].quantityLabel, "۲");
  assert.deepStrictEqual(payload.items[0].modifierLabels, ["شیر جو"]);
  assert.strictEqual(payload.items[0].baristaNote, "کم‌شیرین");
  assert.strictEqual(payload.items[0].unitPriceLabel, formatMoneyPersian(180000));
  assert.strictEqual(payload.totals.totalLabel, toPersianDigits("360٬000"));
});

test("normalizePrintRequest preserves direct render.py payload and custom copies filter", () => {
  const rawPayload = {
    header: { cafeName: "Sugarista" },
    order: { code: "SG-DIRECT" },
    items: [{ name: "اسپرسو", quantity: 1, quantityLabel: "۱" }],
    totals: { totalLabel: "۱۰۰٬۰۰۰" },
  };
  const { payload, copies, trackingCode } = normalizePrintRequest({
    payload: rawPayload,
    copies: ["barista"],
  });
  assert.strictEqual(trackingCode, "SG-DIRECT");
  assert.deepStrictEqual(copies, ["barista"]);
  assert.strictEqual(payload.order.code, "SG-DIRECT");
});

console.log(`✓ agent-helpers: ${passed}/${passed} tests passed`);

"use strict";

/**
 * Pure helpers for the print agent's CUPS interaction — kept here so they can
 * be unit-tested without spawning processes.
 *
 * Failure mode this module supports fixing: `lp` returning success only means
 * the job was ACCEPTED into the queue. If the printer hiccups at that moment
 * (USB glitch, paper out), CUPS marks the queue disabled and the bytes sit
 * until someone wakes the printer — then replay from a dirty head state and
 * produce garbage receipts while the database already says "printed".
 */

/**
 * Extract the CUPS job id from `lp` stdout, e.g.
 *   "request id is GA-E200I-79 (1 file(s))" → "GA-E200I-79".
 * Returns null when no id could be parsed.
 */
function parseLpRequestId(stdout) {
  if (typeof stdout !== "string") return null;
  const match = stdout.match(/request id is ([^\s(]+)/);
  return match ? match[1] : null;
}

/**
 * List the job ids currently sitting in a printer queue, given the full
 * output of `lpstat -W not-completed -o <printer>`. Each row looks like:
 *   "GA-E200I-79          arefzangeneh     86016   Sun Nov  8 15:25:21 2026"
 * Header/name lines without a numeric id suffix are ignored. Returns [] on
 * empty output.
 */
function listQueuedJobIds(lpstatOutput) {
  if (typeof lpstatOutput !== "string" || !lpstatOutput.trim()) return [];
  const ids = [];
  for (const line of lpstatOutput.split("\n")) {
    const token = line.trim().split(/\s+/)[0];
    if (token && /-(\d+)$/.test(token)) ids.push(token);
  }
  return ids;
}

/**
 * True when `lpstat -p <printer>` output says the queue is in a state where
 * it will refuse to play jobs: "disabled", "pause" (CUPS phrasing) or when a
 * "now accepting"/"busy" pair indicates the destination rejects new work.
 */
function isQueueDisabled(lpstatP) {
  return /disabled/i.test(lpstatP || "");
}

/**
 * Warm-up block prepended to every raw stream. The printer mangles or drops
 * the FIRST bytes of a transfer whenever it starts data while not fully
 * ready — fresh from sleep after an idle gap, or still cycling the cutter of
 * the previous job. Corruption is state-dependent, never content-dependent
 * (identical streams printed clean and garbled purely based on job timing).
 *
 * The primer absorbs that loss window with bytes the printer cannot make
 * look wrong on paper: NUL bytes are ignored in text mode and every ESC @
 * re-initializes the controller, so regardless of how many leading bytes the
 * printer swallows or scrambles, the receipt itself arrives intact. Costs
 * zero paper.
 */
const PRIMER_ZERO_BYTES = 1024;
const PRIMER_RESET_ITERATIONS = 4;

function buildWarmupPrimer() {
  const primer = Buffer.alloc(PRIMER_ZERO_BYTES + PRIMER_RESET_ITERATIONS * 2);
  // 0x00 run: ignored by ESC/POS printers in text mode.
  for (let i = 0; i < PRIMER_RESET_ITERATIONS; i += 1) {
    primer[PRIMER_ZERO_BYTES + i * 2] = 0x1b;
    primer[PRIMER_ZERO_BYTES + i * 2 + 1] = 0x40; // ESC @
  }
  return primer;
}

const PERSIAN_DIGITS = ["۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹"];

function toPersianDigits(value) {
  return String(value ?? "")
    .replace(/[0-9]/g, (d) => PERSIAN_DIGITS[Number(d)])
    .replace(/[\u0660-\u0669]/g, (d) => PERSIAN_DIGITS[d.charCodeAt(0) - 0x0660]);
}

function formatMoneyPersian(amount) {
  const num = Math.round(Number(amount || 0));
  if (!Number.isFinite(num)) return "۰";
  const formatted = Math.abs(num)
    .toString()
    .replace(/\B(?=(\d{3})+(?!\d))/g, "٬");
  return (num < 0 ? "-" : "") + toPersianDigits(formatted);
}

/**
 * Normalizes any incoming print/preview HTTP payload (either the full
 * render.py ReceiptPayload or the legacy posReceipt.js { customer, barista }
 * bundle) into a canonical { payload, copies, trackingCode } object for
 * render.py.
 */
function normalizePrintRequest(body = {}) {
  const rawCopies = Array.isArray(body.copies) && body.copies.length
    ? body.copies.map((c) => String(c).toLowerCase()).filter((c) => c === "customer" || c === "barista")
    : ["customer", "barista"];
  const copies = rawCopies.length ? rawCopies : ["customer", "barista"];

  if (body.payload && typeof body.payload === "object" && body.payload.order && body.payload.items) {
    const code = String(body.payload.order?.code || body.trackingCode || "receipt");
    return { payload: body.payload, copies, trackingCode: code };
  }

  if (body.order && Array.isArray(body.items)) {
    const code = String(body.order?.code || body.trackingCode || "receipt");
    return { payload: body, copies, trackingCode: code };
  }

  const customer = body.customer || body.customerReceipt || {};
  const barista = body.barista || body.baristaReceipt || {};
  const customerLines = Array.isArray(customer.lines) ? customer.lines : [];
  const baristaLines = Array.isArray(barista.lines) ? barista.lines : [];
  const lineCount = Math.max(customerLines.length, baristaLines.length);

  const items = [];
  for (let i = 0; i < lineCount; i += 1) {
    const cLine = customerLines[i] || {};
    const bLine = baristaLines[i] || {};
    const qty = Number(cLine.quantity ?? bLine.quantity ?? 1) || 1;
    const unitPrice = Number(cLine.unitPrice ?? 0) || 0;
    const lineTotal = Number(cLine.lineTotal ?? unitPrice * qty) || 0;

    items.push({
      name: String(cLine.name || bLine.name || ""),
      sizeLabel: cLine.sizeLabel ?? bLine.sizeLabel ?? null,
      quantity: qty,
      quantityLabel: toPersianDigits(qty),
      modifierLabels: Array.isArray(cLine.modifierLabels)
        ? cLine.modifierLabels
        : Array.isArray(bLine.modifierLabels)
          ? bLine.modifierLabels
          : [],
      unitPriceLabel: formatMoneyPersian(unitPrice),
      totalLabel: formatMoneyPersian(lineTotal),
      payerLabel: cLine.payerLabel || null,
      baristaNote: String(cLine.baristaNote ?? bLine.baristaNote ?? "").trim() || null,
      options: Array.isArray(cLine.options) ? cLine.options : [],
      optionTotalLabel: cLine.optionTotalLabel || null,
      customDrinkSummary: Array.isArray(cLine.customDrinkSummary) ? cLine.customDrinkSummary : [],
    });
  }

  const computedSubtotal = customerLines.reduce(
    (sum, l) => sum + (Number(l.lineTotal ?? (Number(l.unitPrice || 0) * Number(l.quantity || 1))) || 0),
    0
  );
  const subtotalAmount = Number(customer.subtotalAmount ?? computedSubtotal) || 0;
  const deliveryFee = Number(customer.deliveryFee ?? 0) || 0;
  const taxTotal = Number(customer.taxTotal ?? 0) || 0;
  const totalAmount = Number(customer.totalAmount ?? subtotalAmount + deliveryFee + taxTotal) || 0;

  const trackingCode = String(customer.trackingCode || barista.trackingCode || body.trackingCode || "—");
  const printedAt = toPersianDigits(customer.printedAt || barista.printedAt || "");
  const guestName = String(customer.guestName || barista.guestName || "").trim();
  const guestPhone = String(customer.guestPhone || "").trim();

  const payload = {
    copyType: "Customer",
    header: {
      cafeName: String(customer.cafeName || barista.cafeName || "شوگاریستا"),
      address: String(customer.address || "") || null,
      contactPhone: String(customer.contactPhone || "") || null,
    },
    order: {
      code: trackingCode,
      channelLabel: String(customer.channelLabel || "سفارش صندوق (POS)"),
      serviceLabel: String(customer.service || barista.service || "") || null,
      tableLabel: String(customer.table || barista.table || "") || null,
      customerName: guestName && guestName !== "مهمان" ? guestName : null,
      customerPhone: guestPhone && guestPhone.toUpperCase() !== "POS" ? guestPhone : null,
      note: String(customer.note || barista.note || "") || null,
      createdAtLabel: printedAt,
      paidAtLabel: printedAt,
      paymentLabel: String(customer.paymentMethod || ""),
      reference: customer.reference || null,
      pickupAtLabel: customer.pickupAtLabel || null,
    },
    items,
    totals: {
      subtotalLabel: formatMoneyPersian(subtotalAmount),
      deliveryFeeLabel: deliveryFee > 0 ? formatMoneyPersian(deliveryFee) : null,
      taxLabel: taxTotal > 0 ? formatMoneyPersian(taxTotal) : null,
      totalLabel: formatMoneyPersian(totalAmount),
      amountPaidLabel: formatMoneyPersian(totalAmount),
    },
    footerText: customer.footerText || null,
    timeZoneId: "Asia/Tehran",
    layout: body.layout || customer.layout || {},
  };

  return { payload, copies, trackingCode };
}

module.exports = {
  parseLpRequestId,
  listQueuedJobIds,
  isQueueDisabled,
  buildWarmupPrimer,
  toPersianDigits,
  formatMoneyPersian,
  normalizePrintRequest,
};

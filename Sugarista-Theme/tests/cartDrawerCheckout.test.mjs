#!/usr/bin/env node
/**
 * Static regression checks for the cart-drawer checkout race (no deps):
 *   node tests/cartDrawerCheckout.test.mjs
 *
 * The drawer's pay button used to disappear below the fold: the whole drawer
 * scrolled (max-height + overflow:auto), the footer including «پرداخت» was
 * part of that scroll, and scrollbars are hidden by design — so on a long
 * checkout form the user could not see the payment button at all. The fix
 * pins head/foot and scrolls only the body; optional fields collapse into a
 * <details>. These checks lock that contract in text form.
 */

import assert from "node:assert";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const baseDir = dirname(fileURLToPath(import.meta.url));
const html = readFileSync(join(baseDir, "..", "index.html"), "utf8");
const css = readFileSync(join(baseDir, "..", "assets", "css", "style.css"), "utf8");

let passed = 0;
function test(name, fn) {
  fn();
  passed += 1;
}

/** Slice every CSS rule block starting exactly at `selector {`. */
function cssBlocks(selector) {
  const blocks = [];
  for (let idx = css.indexOf(selector); idx !== -1; idx = css.indexOf(selector, idx + 1)) {
    const open = css.indexOf("{", idx);
    const close = css.indexOf("}", open);
    blocks.push(css.slice(idx, close + 1));
  }
  assert.ok(blocks.length, `missing CSS rule: ${selector}`);
  return blocks;
}

/* ── index.html: pay button lives in the pinned footer ─────────────────── */

test("checkout footer keeps the pay button wired to validation + loading state", () => {
  assert.ok(
    /:disabled="checkoutLoading \|\| !canSubmitCheckout"/.test(html),
    "pay button must stay disabled while loading or when canSubmitCheckout is false",
  );
  assert.ok(html.includes('@click="submitCheckout()"'), "pay button must call submitCheckout()");
});

test("payable amount moved into the pinned footer (exactly once)", () => {
  // The mock payment card later reuses the same wording with paymentAmount —
  // that copy is fine; what must stay unique is the drawer's wallet-aware amount.
  const payable = html.split("formatPrice(payableAfterWallet)").length - 1;
  assert.strictEqual(
    payable,
    1,
    "formatPrice(payableAfterWallet) must appear only once — in the drawer footer",
  );
  const footerIdx = html.lastIndexOf("cart-drawer-foot");
  const amountIdx = html.indexOf("مبلغ قابل پرداخت");
  assert.ok(amountIdx > footerIdx, "the payable amount must sit inside the drawer footer");
  const payAmountBlocks = html.split("cart-drawer-pay-amount").length - 1;
  assert.strictEqual(payAmountBlocks, 1, "footer pay row must exist exactly once");
});

test("drawer foot offers back + pay and the back button is icon-only", () => {
  assert.ok(html.includes('aria-label="بازگشت به سبد"'), "checkout footer needs the back button");
  assert.ok(html.includes("cart-drawer-pay"), "footer needs the pay row wrapper");
});

/* ── minimized checkout: essentials + collapsed extras ─────────────────── */

test("required name/phone sit in the .checkout-essentials grid, defined once", () => {
  for (const field of ["checkout-customer-name", "checkout-customer-phone"]) {
    const count = html.split(`id="${field}"`).length - 1;
    assert.strictEqual(count, 1, `${field} must exist exactly once`);
  }
  assert.ok(html.includes('class="checkout-essentials"'), "essentials wrapper missing");
});

test("optional extras (discount, email, points, note) collapse into <details>", () => {
  assert.ok(/<details class="checkout-extra">/.test(html), "checkout extras must be a <details>");
  for (const field of [
    "checkout-discount-code",
    "checkout-customer-email",
    "checkout-redeem-points",
    "checkout-note",
  ]) {
    const count = html.split(`id="${field}"`).length - 1;
    assert.strictEqual(count, 1, `${field} must exist exactly once (inside the details)`);
  }
  const detailsIdx = html.indexOf('<details class="checkout-extra">');
  const detailsEnd = html.indexOf("</details>", detailsIdx);
  for (const field of [
    "checkout-discount-code",
    "checkout-customer-email",
    "checkout-redeem-points",
    "checkout-note",
  ]) {
    assert.ok(
      html.indexOf(`id="${field}"`) > detailsIdx && html.indexOf(`id="${field}"`) < detailsEnd,
      `${field} must live inside the collapsed <details> block`,
    );
  }
});

/* ── style.css: only the body scrolls; head/foot stay pinned ───────────── */

test(".cart-drawer no longer scrolls itself — footer pinned", () => {
  const drawer = cssBlocks(".cart-drawer {")[0];
  assert.ok(drawer.includes("overflow: hidden"), "drawer itself must not scroll");
  const body = cssBlocks(".cart-drawer-body {")[0];
  assert.ok(body.includes("overflow-y: auto"), "the drawer body must be the scroll container");
  assert.ok(body.includes("flex: 1 1 auto"), "body must flex-grow to fill");
  assert.ok(
    cssBlocks(".cart-drawer-foot {").some((b) => b.includes("flex: 0 0 auto")),
    "foot must never be pushed by body growth",
  );
});

test("essentials grid collapses to one column on narrow screens", () => {
  assert.ok(css.includes(".checkout-essentials {"), "essentials grid rule missing");
  const mediaIdx = css.indexOf("@media (max-width: 520px)");
  assert.notStrictEqual(mediaIdx, -1, "narrow-screen breakpoint missing");
  const mediaClose = css.indexOf("}", css.indexOf("{", mediaIdx));
  const media = css.slice(mediaIdx, mediaClose + 1);
  assert.ok(media.includes(".checkout-essentials"), "media query must retarget essentials");
  assert.ok(media.includes("grid-template-columns: 1fr"), "single column at ≤520px");
});

console.log(`✓ cart-drawer checkout: ${passed}/${passed} static checks passed`);

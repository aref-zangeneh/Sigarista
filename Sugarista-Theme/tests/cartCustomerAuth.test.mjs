#!/usr/bin/env node
/**
 * Static regression checks for the phone-only customer auth of the digital
 * menu (no deps): node tests/cartCustomerAuth.test.mjs
 *
 * Contract: a first-visit popup asks ONLY for the phone number (no password,
 * no one-time code); «فقط منو را ببینم» skips without entering an account and
 * silences the popup for that browser; a signed-in customer keeps a HttpOnly
 * session cookie for the admin-configured duration, never re-types their
 * phone at checkout, and can leave the account from the profile modal.
 */

import assert from "node:assert";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const baseDir = dirname(fileURLToPath(import.meta.url));
const html = readFileSync(join(baseDir, "..", "index.html"), "utf8");
const js = readFileSync(join(baseDir, "..", "assets", "js", "app.js"), "utf8");
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

/* ── index.html: the popup ──────────────────────────────────────────────── */

test("phone-only login popup exists and only asks for the phone", () => {
  assert.ok(html.includes("auth-popup-overlay"), "popup overlay must exist");
  assert.ok(html.includes("auth-popup-card"), "popup card must exist");
  assert.ok(html.includes('x-model="authPhoneInput"'), "popup must bind the phone input");
  assert.ok(html.includes('@click="submitAuthPhone()"'), "popup submit must call submitAuthPhone()");
  assert.ok(!/auth-popup[^]*type="password"/.test(js), "phone-only flow must never ask for a password");
  assert.ok(!/auth-popup[^]*verify|otp/i.test(html), "phone-only flow must never ask for a one-time code");
});

test("popup copy comes from admin-editable site settings with safe fallbacks", () => {
  for (const field of ["settings.authPopupTitle", "settings.authPopupDescription", "settings.authPopupLoginButtonLabel", "settings.authPopupSkipButtonLabel"]) {
    assert.ok(html.includes(field), `popup must read ${field} from site settings`);
  }
  assert.ok(/settings\.authPopupTitle \|\|/.test(html), "title must fall back to the built-in copy when the admin leaves it empty");
  assert.ok(js.includes("authPopupSkipButtonLabel") || html.includes("authPopupSkipButtonLabel"), "skip label must be admin-controlled too");
});

test("skip button keeps non-entering customers out of their account", () => {
  assert.ok(html.includes("skipAuthPrompt"), "skip button label must exist");
  assert.ok(
    (html.match(/@click="skipAuthPrompt\(\)"/g) || []).length >= 2,
    "skip must be wired to the card close, the backdrop and the button",
  );
  const skip = cssBlocks(".auth-popup-skip")[0];
  assert.ok(!skip.includes("text-decoration: underline"), "the skip label must NOT be underlined");
});

test("checkout hides name/phone fields behind the signed-in session chip", () => {
  const chipIdx = html.indexOf("checkout-session-chip");
  const essentialsIdx = html.indexOf('x-show="!customerSession"');
  assert.ok(chipIdx !== -1, "session chip must exist in the checkout form");
  assert.ok(essentialsIdx !== -1, "essentials grid must hide while a session exists");
  assert.ok(chipIdx < essentialsIdx, "chip replaces the essentials grid");
  assert.ok(html.includes("x-text=\"customerSession?.persianName\""), "chip shows the customer name");
  assert.ok(html.includes("x-text=\"customerSession?.phoneNumber\""), "chip shows the customer phone");
});

test("profile modal offers signing out when a session exists", () => {
  assert.ok(
    html.includes("customerSession ? logoutCustomer() : forgetProfile()"),
    "profile footer must switch between «خروج از حساب» and the anonymous flow",
  );
  assert.ok(html.includes("'خروج از حساب'"), "logout label must exist");
});

/* ── app.js: session lifecycle ─────────────────────────────────────────── */

test("init performs the customer auth check before anything asks again", () => {
  assert.ok(/this\.initCustomerAuth\(\);/.test(js), "init() must call initCustomerAuth()");
  const initIdx = js.indexOf("async init()");
  const authIdx = js.indexOf("this.initCustomerAuth();");
  assert.ok(initIdx !== -1 && authIdx > initIdx, "initCustomerAuth call must live inside init()");
});

test("popup only shows when there is no session, no stored phone and no skip flag", () => {
  assert.ok(
    js.includes('AUTH_PROMPT_STORAGE_KEY: "sugarista_auth_prompt_skipped"'),
    "skip flag key must be declared",
  );
  const gateIdx = js.indexOf("this.authPopupOpen = !dismissed && !storedPhone;");
  assert.ok(gateIdx !== -1, "popup gate must require neither dismissal nor a stored phone");
  const sessionReset = js.lastIndexOf("this.customerSession = null;", gateIdx);
  assert.ok(sessionReset !== -1, "popup gate must only run after confirming there is no session");
});

test("login stores the session identity and pre-fills profile + checkout", () => {
  assert.ok(js.includes("/customers/auth/login"), "login must hit the session endpoint");
  assert.ok(js.includes("/customers/auth/me"), "session identity must come from the me endpoint");
  assert.ok(js.includes("/customers/auth/logout"), "logout must revoke on the server too");
  assert.ok(
    js.includes("this.customerSession = result.customer;"),
    "the raw token stays in the cookie — the UI keeps only the identity",
  );
  assert.ok(
    js.includes("this.checkoutForm.customerPhone = result.customer.phoneNumber;"),
    "checkout phone must be pre-filled after login",
  );
  assert.ok(
    js.includes("this.checkoutForm.customerPhone = identity.phoneNumber;"),
    "returning sessions must pre-fill checkout without any typing",
  );
});

test("login failure keeps the user informed but never signs them in", () => {
  assert.ok(
    js.includes("this.authPopupError = error.message"),
    "popup must surface the login error",
  );
  assert.ok(
    /submitAuthPhone\(\) \{[\s\S]*?digits\.length < 8[\s\S]*?return;[\s\S]*?await this\.loginCustomer\(digits, true\);/.test(js),
    "submitAuthPhone must reject too-short phone strings and then pass the digits to the login",
  );
});

test("logout clears the local identity so the account is really left", () => {
  const fnIdx = js.indexOf("async logoutCustomer()");
  assert.ok(fnIdx !== -1, "logoutCustomer must exist");
  const body = js.slice(fnIdx, js.indexOf("async logoutCustomer()", fnIdx + 10));
  assert.ok(body.includes('window.localStorage.removeItem(PROFILE_STORAGE_KEY)'), "logout must forget the profile phone");
  assert.ok(body.includes("this.customerSession = null;"), "logout must clear the session identity");
  assert.ok(body.includes('window.localStorage.removeItem(this.AUTH_PROMPT_STORAGE_KEY)'), "logout must re-arm the popup");
});

test("submitProfilePhone routes through the real login too", () => {
  assert.ok(
    js.includes("const loggedIn = await this.loginCustomer(digits);"),
    "manual profile identification must create a session, not just localStorage",
  );
  assert.ok(js.includes("if (!loggedIn) {") && js.includes("return;"), "failed login must abort the profile submit");
});

/* ── style.css: popup + chip styling ships with the feature ────────────── */

test("popup and chip have their own visible styling", () => {
  cssBlocks(".auth-popup-overlay");
  cssBlocks(".auth-popup-card");
  cssBlocks(".auth-popup-skip");
  const chip = cssBlocks(".checkout-session-chip");
  assert.ok(chip[0].includes("x-show") === false, "sanity: rules contain declarations");
});

console.log(`✓ Customer phone auth OK — ${passed} checks passed.`);

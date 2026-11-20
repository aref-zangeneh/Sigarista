#!/usr/bin/env node
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const baseDir = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
require(join(baseDir, "..", "assets", "js", "persianDigits.js"));

const digits = globalThis.SugaristaDigits;
const html = readFileSync(join(baseDir, "..", "index.html"), "utf8");
const js = readFileSync(join(baseDir, "..", "assets", "js", "app.js"), "utf8");

test("SugaristaDigits helpers convert between Persian, Arabic-Indic, and English digits", () => {
  assert.ok(digits, "SugaristaDigits global API must be registered");
  assert.equal(digits.toEnglishDigits("۰۹۱۲۳۴۵۶۷۸۹"), "09123456789");
  assert.equal(digits.toEnglishDigits("٠١٢٣٤٥٦٧٨٩"), "0123456789");
  assert.equal(digits.toPersianDigits(0), "۰");
  assert.equal(digits.toPersianDigits("09123456789"), "۰۹۱۲۳۴۵۶۷۸۹");
  assert.equal(
    digits.toPersianDisplayText("ساعت 08:30 و لینک https://example.com/v2/test و #aa3171"),
    "ساعت ۰۸:۳۰ و لینک https://example.com/v2/test و #aa3171"
  );
});

test("sanitizeNumericInput handles mixed keyboards and caret offsets cleanly", () => {
  assert.deepEqual(
    digits.sanitizeNumericInput("۱۲٫۵abc", { allowDecimal: true, allowNegative: false, caret: 4 }),
    { english: "12.5", display: "۱۲.۵", caret: 4 }
  );
  assert.deepEqual(
    digits.sanitizeNumericInput("۱٬۵۰۰", { allowDecimal: false, allowNegative: false, caret: 5 }),
    { english: "1500", display: "۱۵۰۰", caret: 4 }
  );
});

test("normalizePayloadDigits normalizes JSON payload digits to ASCII while keeping secrets intact", () => {
  const payload = digits.normalizePayloadDigits({
    customerPhone: "۰۹۱۲۳۴۵۶۷۸۹",
    redeemPoints: "۲۵۰",
    password: "Secret۱۲۳",
    token: "tok-۹۹",
  });
  assert.equal(payload.customerPhone, "09123456789");
  assert.equal(payload.redeemPoints, "250");
  assert.equal(payload.password, "Secret۱۲۳");
  assert.equal(payload.token, "tok-۹۹");
});

test("Theme index.html uses Persian-capable numeric inputs instead of native type=number", () => {
  assert.ok(!/type="number"/i.test(html), "index.html must not contain native <input type='number'>");
  assert.ok(html.includes('persianDigits.js'), "index.html must load persianDigits.js");
  assert.ok(html.includes('data-numeric-input="true"'), "numeric inputs must opt into data-numeric-input");
});

test("Theme app.js normalizes Persian/Arabic digits before stripping non-digits on phones", () => {
  const matches = js.match(/toEnglishDigits\([^)]+\)\.replace\(\/\\D\/g/g) || [];
  assert.ok(matches.length >= 8, "every phone extractor in app.js must call toEnglishDigits before /\\D/g");
});

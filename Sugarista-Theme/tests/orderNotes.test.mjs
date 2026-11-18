#!/usr/bin/env node
/**
 * Static regression checks for the customer Order Notes (یادداشت سفارش حضوری)
 * and Cross-Device Customer Menu Tooltips feature (no deps):
 *   node tests/orderNotes.test.mjs
 *
 * Contract:
 * 1. Customers can add items to Notes using the exact same customization modal
 *    (sizes, required/optional option groups, custom drink builder, modifiers,
 *    quantity, and guest/person note) as adding to the online cart.
 * 2. Notes persist purely in localStorage (zero database / backend writes).
 * 3. Notes drawer offers both a clean per-person editable list ("لیست و نفرات")
 *    with collapsible per-line person/note editor & person filter tabs, and an
 *    aggregated cashier presentation view ("نمای صندوق (درشت)").
 * 4. Customer menu tooltips (`menu.*`) are registered in both Admin Panel
 *    (`tooltipRegistry.js`) and Theme (`DEFAULT_MENU_TOOLTIPS`) and rendered
 *    through a single high-performance fixed portal (`#menu-tooltip-portal`).
 */

import assert from "node:assert";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const baseDir = dirname(fileURLToPath(import.meta.url));
const html = readFileSync(join(baseDir, "..", "index.html"), "utf8");
const js = readFileSync(join(baseDir, "..", "assets", "js", "app.js"), "utf8");
const css = readFileSync(join(baseDir, "..", "assets", "css", "style.css"), "utf8");
const adminRegistry = readFileSync(
  join(baseDir, "..", "..", "Sugarista-Admin", "src", "utils", "tooltipRegistry.js"),
  "utf8"
);

let passed = 0;
function test(name, fn) {
  fn();
  passed += 1;
}

test("localStorage keys for notes exist and no backend API calls are made for notes", () => {
  assert.ok(js.includes('NOTES_STORAGE_KEY = "sugarista_order_notes"'), "notes storage key must exist");
  assert.ok(js.includes('NOTES_MEMO_STORAGE_KEY = "sugarista_order_notes_memo"'), "notes memo storage key must exist");
  assert.ok(js.includes("this.loadNotes();"), "init() must load saved notes from localStorage");
  assert.ok(!/fetch\([^)]*notes/i.test(js), "notes must never mutate or call any backend/database endpoint");
});

test("product cards and customization modal expose Add-to-Notes alongside Add-to-Cart", () => {
  assert.ok(html.includes('@click="openAddToNoteModal(item)"'), "product card must have add-to-note trigger");
  assert.ok(html.includes('@click="confirmAddToNotes()"'), "customization modal must have confirmAddToNotes button");
  assert.ok(js.includes("buildValidatedModalLine()"), "cart and notes must share the exact option/builder validation pipeline");
  assert.ok(html.includes("note-toast"), "adding to notes must show a non-intrusive toast rather than hijacking the screen");
});

test("notes drawer supports both clean per-person list and aggregated counter view", () => {
  assert.ok(html.includes('class="notes-drawer"'), "notes drawer must exist");
  assert.ok(html.includes("aggregatedCounterNotes"), "counter view must render aggregatedCounterNotes");
  assert.ok(html.includes("filteredNotesList"), "list view must support person filtering via filteredNotesList");
  assert.ok(html.includes("toggleEditNoteLine(line.noteKey)"), "list view must collapse per-item person/note editor until tapped");
  assert.ok(html.includes('@click="moveNotesToCart()"'), "drawer must allow transferring notes to online cart when sales are open");
  assert.ok(html.includes('@click="copyNotesToClipboard()"'), "drawer must allow copying formatted notes text");
});

test("customer menu tooltips are synced between Admin Panel registry and Theme portal", () => {
  assert.ok(html.includes('id="menu-tooltip-portal"'), "single global tooltip portal must exist in index.html");
  assert.ok(js.includes("DEFAULT_MENU_TOOLTIPS"), "DEFAULT_MENU_TOOLTIPS must be defined in app.js");
  assert.ok(js.includes("syncMenuTooltips()"), "syncMenuTooltips() must resolve admin overrides");

  const adminMenuKeys = [...adminRegistry.matchAll(/key:\s*'(menu\.[a-z0-9._-]+)'/g)].map((m) => m[1]);
  assert.ok(adminMenuKeys.length >= 20, "admin tooltipRegistry must register at least 20 customer menu tooltip keys");

  for (const key of adminMenuKeys) {
    assert.ok(js.includes(`"${key}"`), `Theme DEFAULT_MENU_TOOLTIPS is missing key registered in Admin: ${key}`);
    assert.ok(html.includes(`'${key}'`), `Theme index.html is missing trigger for tooltip key: ${key}`);
  }
});

test("notes and tooltip styling rules exist in style.css", () => {
  for (const selector of [
    ".notes-drawer",
    ".notes-bar",
    ".product-note-btn",
    ".notes-counter-card",
    ".note-toast",
    ".menu-tip-btn",
    ".menu-tip-portal"
  ]) {
    assert.ok(css.includes(selector), `missing CSS rule: ${selector}`);
  }
});

console.log(`✓ Order notes & customer menu tooltips OK — ${passed} checks passed.`);

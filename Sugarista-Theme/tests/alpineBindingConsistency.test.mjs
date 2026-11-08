// Alpine binding consistency test (dependency-free — run with plain node)
//
// Catches the regression class where a component getter is accidentally
// demoted to a plain method (or vice versa). Example: commit 01ffe24 lost the
// `get` keyword on `cartTotal`, so every template binding that read it as a
// property received a FUNCTION instead of a number — the cart drawer showed
// "۰ تومان", `finalAmount` turned into NaN and checkout silently stopped
// sending `expectedTotalAmount`, disabling the server-side price-change guard.
//
// Contracts between Sugarista-Theme/assets/js/app.js (the Alpine component)
// and Sugarista-Theme/index.html (the bindings), plus the component itself:
//   1. a name bound as a PROPERTY (no call parens) must be a getter
//   2. a name bound as a CALL `name(...)` must be a plain method
//   3. `this.x` reads inside app.js follow the same rules
//
// Usage: node tests/alpineBindingConsistency.test.mjs   (exit 1 on failure)
//        node tests/alpineBindingConsistency.test.mjs --verbose

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const appJsPath = join(root, "assets", "js", "app.js");
const indexHtmlPath = join(root, "index.html");

const appJs = readFileSync(appJsPath, "utf8");
const indexHtml = readFileSync(indexHtmlPath, "utf8");

const failures = [];

/* ── extract the component definitions from app.js ────────────────────── */

// Blank out comments and string contents so the regexes only see real code.
function stripLiterals(source) {
  let out = source.replace(/\/\*[\s\S]*?\*\//g, (m) => " ".repeat(m.length));
  out = out.replace(/\/\/[^\n]*/g, (m) => " ".repeat(m.length));

  // Template literals: blank the literal text but KEEP ${...} expressions.
  out = out.replace(/`(?:[^`\\]|\\.)*`/g, (literal) => {
    const interpolations = literal.match(/\$\{[^}]*\}/g) ?? [];
    return " " + interpolations.join(" ") + " ";
  });

  // Regular strings → spaces
  out = out.replace(/'(?:[^'\\]|\\.)*'|"(?:[^"\\]|\\.)*"/g, (m) => " ".repeat(m.length));

  return out;
}

const code = stripLiterals(appJs);

const KEYWORDS = new Set([
  "if", "for", "while", "switch", "catch", "function", "return", "typeof",
  "await", "async", "else", "do", "export", "import", "constructor", "get",
  "set", "super", "this", "class", "const", "let", "var", "new", "case",
  "throw", "yield", "delete", "in", "of", "instanceof", "default"
]);

const getterNames = new Set();
const methodNames = new Set();

// `get name(...) {` — line start, allowed to span indentation 2..12 spaces
for (const match of code.matchAll(/^ {2,12}get\s+([a-zA-Z_$][\w$]*)\s*\([^;(){}]*\)\s*\{\s*$/gm)) {
  getterNames.add(match[1]);
}

// Plain object methods `name(...) {` / `async name(...) {`
for (const line of code.split("\n")) {
  const plain = line.match(/^ {2,12}([a-zA-Z_$][\w$]*)\s*\([^;(){}]*\)\s*\{\s*$/);
  if (plain && !KEYWORDS.has(plain[1])) methodNames.add(plain[1]);

  const asynced = line.match(/^ {2,12}async\s+([a-zA-Z_$][\w$]*)\s*\([^;(){}]*\)\s*\{\s*$/);
  if (asynced && !KEYWORDS.has(asynced[1])) methodNames.add(asynced[1]);
}

if (getterNames.size === 0 || methodNames.size === 0) {
  failures.push(
    `Could not extract getters/methods from ${appJsPath} — extraction regexes are too strict ` +
    `(getters=${getterNames.size}, methods=${methodNames.size}).`
  );
}

/* ── bindings inside index.html ───────────────────────────────────────── */

const alpineValues = [];
for (const match of indexHtml.matchAll(/\b((?:x-[\w:-]+)|(?::[\w-]+)|@[\w-]+)\s*=\s*"([^"]*)"/g)) {
  if (match[2].trim()) alpineValues.push(match[2]);
}

// Matcher sanity: index.html currently carries 400+ Alpine attribute values.
// If extraction ever drops near zero (e.g. quoting style changes), fail loudly
// instead of silently checking nothing.
if (alpineValues.length < 100) {
  failures.push(
    `Binding extraction from index.html looks broken: only ${alpineValues.length} values captured. ` +
    `Expected several hundred — fix the attribute regex before trusting the contracts.`
  );
}

const bindsAsProperty = new Set();
const bindsAsCall = new Set();

for (const expression of alpineValues) {
  for (const match of expression.matchAll(/(?<![\w$"'@:-])([a-zA-Z_$][\w$]*)\s*(\()?/g)) {
    const name = match[1];
    if (KEYWORDS.has(name) || name === "Alpine") continue;
    const before = expression.slice(0, match.index);
    if (before.endsWith(".")) continue; // object property, not a component read
    if (match[2]) bindsAsCall.add(name);
    else bindsAsProperty.add(name);
  }
}

/* ── `this.x` reads inside app.js ─────────────────────────────────────── */

const thisReads = new Set();
for (const match of code.matchAll(/\bthis\.([a-zA-Z_$][\w$]*)/g)) {
  thisReads.add(match[1]);
}

/* ── contract checks ──────────────────────────────────────────────────── */

for (const name of bindsAsProperty) {
  if (methodNames.has(name) && !getterNames.has(name)) {
    failures.push(
      `"${name}" is bound as a property in index.html but defined as a plain method in app.js — ` +
      `the template receives a function (this is how cartTotal ended up displaying "۰ تومان").`
    );
  }
}

for (const name of bindsAsCall) {
  if (getterNames.has(name) && !methodNames.has(name)) {
    failures.push(`"${name}" is called with () in index.html but defined as a getter in app.js.`);
  }
}

for (const name of thisReads) {
  const isCall = new RegExp(`this\\.${name}\\s*\\(`).test(code);
  if (!isCall && methodNames.has(name) && !getterNames.has(name)) {
    failures.push(
      `app.js reads "this.${name}" as a property but it is a plain method — ` +
      `evaluate the value or add "(...)" at the read site.`
    );
  }
}

/* ── report ───────────────────────────────────────────────────────────── */

if (failures.length) {
  console.error(`✗ Alpine binding consistency test FAILED (${failures.length} issue${failures.length > 1 ? "s" : ""}):\n`);
  for (const failure of failures) console.error(`  • ${failure}`);
  console.error(`\nFix the component definition in Sugarista-Theme/assets/js/app.js (usually: restore the \`get\` keyword) and re-run.`);
  process.exit(1);
}

const summary =
  `${getterNames.size} getters / ${methodNames.size} methods parsed OK; ` +
  `${bindsAsProperty.size + bindsAsCall.size} names bound in index.html checked; ` +
  `${thisReads.size} this.* reads checked.`;

if (process.argv.includes("--verbose")) {
  console.log(`getters: ${[...getterNames].sort().join(", ")}`);
  console.log(`methods: ${[...methodNames].sort().join(", ")}`);
}
console.log(`✓ Alpine binding consistency OK — ${summary}`);

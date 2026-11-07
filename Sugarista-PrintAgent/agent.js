#!/usr/bin/env node
/**
 * Sugarista print agent.
 *
 * Runs on the laptop that is USB-connected to the receipt printer.
 * Polls the Sugarista API for queued receipt jobs, renders each one to raw
 * ESC/POS bytes (raster bitmap + explicit auto-cut; render.py — Persian
 * shaping via arabic_reshaper/bidi), and sends them straight to the printer
 * queue with `lp -o raw`. Copies of the same order claimed together print
 * back-to-back as a single job with a cut between them.
 *
 * Zero npm dependencies — Node 18+ (built-in fetch, child_process).
 */

"use strict";

const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { execFile } = require("node:child_process");
const { promisify } = require("node:util");

const execFileAsync = promisify(execFile);

const BASE_DIR = __dirname;
const CONFIG_PATH = path.join(BASE_DIR, "config.json");
const RENDER_PY = path.join(BASE_DIR, "render.py");
const VENV_PYTHON = path.join(BASE_DIR, "venv", "bin", "python");
const TMP_DIR = path.join(os.tmpdir(), "sugarista-print-agent");

const MAX_ATTEMPTS = 3;

function log(...args) {
  const stamp = new Date().toISOString();
  console.log(`[${stamp}]`, ...args);
}

function loadConfig() {
  if (!fs.existsSync(CONFIG_PATH)) {
    throw new Error(`config.json not found at ${CONFIG_PATH}`);
  }
  const raw = JSON.parse(fs.readFileSync(CONFIG_PATH, "utf8"));
  const config = {
    serverUrl: String(raw.serverUrl || "http://localhost").replace(/\/+$/, ""),
    apiKey: String(raw.apiKey || ""),
    printerName: String(raw.printerName || ""),
    pollSeconds: Number(raw.pollSeconds || 4),
    maxJobsPerPoll: Number(raw.maxJobsPerPoll || 10),
    printTimeoutSeconds: Number(raw.printTimeoutSeconds || 60)
  };
  if (!config.apiKey) throw new Error("config.json is missing apiKey");
  return config;
}

function pythonPath() {
  if (fs.existsSync(VENV_PYTHON)) return VENV_PYTHON;
  return "python3";
}

async function apiFetch(config, pathName, options = {}) {
  const url = `${config.serverUrl}${pathName}`;
  const response = await fetch(url, {
    ...options,
    headers: {
      "X-Agent-Key": config.apiKey,
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(options.headers || {})
    }
  });
  if (!response.ok) {
    const body = await response.text().catch(() => "");
    throw new Error(`${options.method || "GET"} ${pathName} → ${response.status} ${body.slice(0, 200)}`);
  }
  return response.json();
}

async function renderReceipt(payload, copyType) {
  fs.mkdirSync(TMP_DIR, { recursive: true });
  const safeCode = String(payload?.order?.code || "receipt").replace(/[^A-Za-z0-9-]/g, "");
  const stamp = Date.now();
  const payloadPath = path.join(TMP_DIR, `${safeCode}-${copyType}-${stamp}.json`);
  const escposPath = path.join(TMP_DIR, `${safeCode}-${copyType}-${stamp}.escpos`);

  fs.writeFileSync(payloadPath, JSON.stringify(payload), "utf8");

  try {
    await execFileAsync(pythonPath(), [RENDER_PY, payloadPath, escposPath], { timeout: 30_000 });
  } finally {
    fs.rmSync(payloadPath, { force: true });
  }

  return escposPath;
}

async function ensureQueueReady(config) {
  // Self-heal: a backend failure (USB glitch, paper out) leaves the CUPS queue
  // disabled, and old jobs then spew as garbage when it recovers mid-stream.
  try {
    const { stdout } = await execFileAsync("lpstat", ["-p", config.printerName], { timeout: 10_000 });
    if (/disabled/i.test(stdout)) {
      log(`Queue "${config.printerName}" is disabled — re-enabling…`);
      await execFileAsync("cupsenable", [config.printerName], { timeout: 10_000 }).catch(() => {});
      await execFileAsync("cupsaccept", [config.printerName], { timeout: 10_000 }).catch(() => {});
    }
  } catch {
    // lpstat failing here is non-fatal; the print attempt will surface errors.
  }
}

async function healQueueAfterError(config) {
  try {
    // Drop stuck jobs (a partial job left mid-stream desynchronizes ESC/POS
    // printing and everything after it prints as garbage) and re-open the queue.
    await execFileAsync("cancel", ["-a", config.printerName], { timeout: 10_000 });
    await execFileAsync("cupsenable", [config.printerName], { timeout: 10_000 });
    await execFileAsync("cupsaccept", [config.printerName], { timeout: 10_000 });
    log(`Queue "${config.printerName}" healed (stale jobs dropped).`);
  } catch (error) {
    log(`Queue heal failed: ${error instanceof Error ? error.message : error}`);
  }
}

async function printEscpos(config, escposPath) {
  // Raw ESC/POS passthrough: bytes reach the printer untouched — no filters,
  // no scaling, no raster conversion. render.py already emits ready-made
  // ESC/POS (raster + explicit cut after every copy).
  const args = ["-d", config.printerName, "-o", "raw", escposPath];
  await execFileAsync("lp", args, { timeout: config.printTimeoutSeconds * 1000 });
  // Wait for the spool to drain so the next job starts on clean paper.
  await new Promise((resolve) => setTimeout(resolve, 500));
}

function copyTypeOf(job) {
  const value = job.copyType;
  if (typeof value === "number") return value === 1 ? "barista" : "customer";
  const text = String(value || "customer").toLowerCase();
  if (text === "1") return "barista";
  if (text === "0") return "customer";
  return text;
}

async function processJobGroup(config, group) {
  // Group = every pending copy of one order claimed in the same poll. They are
  // rendered and submitted as ONE raw job so they print back-to-back, grouped
  // in-order, with cut between copies, and nothing else can interleave.
  let lastError = null;

  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt += 1) {
    const escposPaths = [];
    try {
      if (attempt > 1) await healQueueAfterError(config);
      await ensureQueueReady(config);

      for (const job of group) {
        const payload = JSON.parse(job.payloadJson);
        escposPaths.push(await renderReceipt(payload, copyTypeOf(job)));
      }

      const combinedPath = escposPaths[0].replace(/\.escpos$/, "-combined.escpos");
      const stream = await Promise.all(escposPaths.map((p) => fs.promises.readFile(p)));
      fs.writeFileSync(combinedPath, Buffer.concat(stream));

      await printEscpos(config, combinedPath);

      escposPaths.forEach((p) => fs.rmSync(p, { force: true }));
      fs.rmSync(combinedPath, { force: true });
      return { ok: true };
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
      escposPaths.forEach((p) => fs.rmSync(p, { force: true }));
      log(`  attempt ${attempt}/${MAX_ATTEMPTS} failed for ${group[0].orderTrackingCode}: ${lastError}`);
      if (attempt < MAX_ATTEMPTS) {
        await new Promise((resolve) => setTimeout(resolve, 1500 * attempt));
      }
    }
  }

  return { ok: false, error: lastError };
}

async function markJobResult(config, job, ok, error) {
  try {
    if (ok) {
      await apiFetch(config, `/api/print-agent/${job.id}/printed`, { method: "POST" });
    } else {
      await apiFetch(config, `/api/print-agent/${job.id}/failed`, {
        method: "POST",
        body: JSON.stringify({ error })
      });
    }
  } catch (err) {
    log(`  ⚠ could not report ${ok ? "printed" : "failed"} for ${job.id}: ${err.message}`);
  }
}

async function pollOnce(config) {
  const jobs = await apiFetch(
    config,
    `/api/print-agent/pending?maxJobs=${config.maxJobsPerPoll}`
  );

  if (!Array.isArray(jobs) || jobs.length === 0) return 0;

  log(`Claimed ${jobs.length} print job(s).`);

  // Pending jobs are sorted by creation; contiguous copies of the same order
  // become a single physical print task (back-to-back on one paper with cuts).
  const groups = [];
  for (const job of jobs) {
    const last = groups[groups.length - 1];
    if (last && last[0].orderTrackingCode === job.orderTrackingCode) last.push(job);
    else groups.push([job]);
  }

  for (const group of groups) {
    const labels = group
      .map((j) => copyTypeOf(j))
      .join(" + ");
    log(`Printing ${labels} for order ${group[0].orderTrackingCode} as one job…`);
    const result = await processJobGroup(config, group);
    for (const job of group) {
      await markJobResult(config, job, result.ok, result.error);
    }
    if (result.ok) {
      log(`  ✓ printed ${labels} for ${group[0].orderTrackingCode} (cut between copies)`);
    } else {
      log(`  ✗ gave up on ${labels} for ${group[0].orderTrackingCode}: ${result.error}`);
    }
  }
  return jobs.length;
}

async function main() {
  const config = loadConfig();
  log("Sugarista print agent starting…");
  log(`  server  : ${config.serverUrl}`);
  log(`  printer : ${config.printerName || "(system default)"}`);
  log(`  poll    : every ${config.pollSeconds}s`);

  if (!fs.existsSync(RENDER_PY)) {
    throw new Error(`render.py not found at ${RENDER_PY}`);
  }

  let stopping = false;
  const stop = (signal) => {
    if (stopping) return;
    stopping = true;
    log(`Received ${signal}, shutting down after current job…`);
    process.exit(0);
  };
  process.on("SIGINT", () => stop("SIGINT"));
  process.on("SIGTERM", () => stop("SIGTERM"));

  // Verify the printer queue exists before polling.
  try {
    await execFileAsync("lpstat", ["-p", config.printerName], { timeout: 10_000 });
  } catch {
    log(`WARNING: printer queue "${config.printerName}" not found. Check lpstat -p.`);
  }

  // `--once` drains the queue a single time and exits (handy for testing).
  if (process.argv.includes("--once")) {
    const count = await pollOnce(config);
    log(count === 0 ? "Queue empty — nothing to print." : `Done — processed ${count} job(s).`);
    return;
  }

  while (!stopping) {
    try {
      await pollOnce(config);
    } catch (error) {
      log(`Poll failed: ${error instanceof Error ? error.message : error}`);
    }
    await new Promise((resolve) => setTimeout(resolve, Math.max(config.pollSeconds, 1) * 1000));
  }
}

main().catch((error) => {
  log("FATAL:", error instanceof Error ? error.stack || error.message : error);
  process.exit(1);
});

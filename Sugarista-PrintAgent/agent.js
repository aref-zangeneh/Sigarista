#!/usr/bin/env node
/**
 * Sugarista print agent.
 *
 * Runs on the laptop that is USB-connected to the receipt printer.
 * Polls the Sugarista API for queued receipt jobs, renders each one to a
 * 384-dot bitmap with Persian shaping (render.py), and sends it to CUPS.
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
  const imagePath = path.join(TMP_DIR, `${safeCode}-${copyType}-${stamp}.png`);

  fs.writeFileSync(payloadPath, JSON.stringify(payload), "utf8");

  try {
    await execFileAsync(pythonPath(), [RENDER_PY, payloadPath, imagePath], { timeout: 30_000 });
  } finally {
    fs.rmSync(payloadPath, { force: true });
  }

  return imagePath;
}

async function printImage(config, imagePath) {
  const args = ["-d", config.printerName, imagePath];
  await execFileAsync("lp", args, { timeout: config.printTimeoutSeconds * 1000 });
}

function copyTypeOf(job) {
  const value = job.copyType;
  if (typeof value === "number") return value === 1 ? "barista" : "customer";
  const text = String(value || "customer").toLowerCase();
  if (text === "1") return "barista";
  if (text === "0") return "customer";
  return text;
}

async function processJob(config, job) {
  const copyType = copyTypeOf(job);
  let lastError = null;

  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt += 1) {
    let imagePath = null;
    try {
      const payload = JSON.parse(job.payloadJson);
      imagePath = await renderReceipt(payload, copyType);
      await printImage(config, imagePath);
      fs.rmSync(imagePath, { force: true });
      return { ok: true };
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
      if (imagePath) fs.rmSync(imagePath, { force: true });
      log(`  attempt ${attempt}/${MAX_ATTEMPTS} failed for ${job.id}: ${lastError}`);
      if (attempt < MAX_ATTEMPTS) {
        await new Promise((resolve) => setTimeout(resolve, 1500 * attempt));
      }
    }
  }

  return { ok: false, error: lastError };
}

async function pollOnce(config) {
  const jobs = await apiFetch(
    config,
    `/api/print-agent/pending?maxJobs=${config.maxJobsPerPoll}`
  );

  if (!Array.isArray(jobs) || jobs.length === 0) return 0;

  log(`Claimed ${jobs.length} print job(s).`);
  for (const job of jobs) {
    log(`Printing ${job.copyType} copy for order ${job.orderTrackingCode} (${job.id})…`);
    const result = await processJob(config, job);
    if (result.ok) {
      await apiFetch(config, `/api/print-agent/${job.id}/printed`, { method: "POST" });
      log(`  ✓ printed ${job.copyType} copy for ${job.orderTrackingCode}`);
    } else {
      await apiFetch(config, `/api/print-agent/${job.id}/failed`, {
        method: "POST",
        body: JSON.stringify({ error: result.error })
      });
      log(`  ✗ gave up on ${job.copyType} copy for ${job.orderTrackingCode}: ${result.error}`);
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

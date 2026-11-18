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
const http = require("node:http");
const os = require("node:os");
const path = require("node:path");
const { execFile } = require("node:child_process");
const { promisify } = require("node:util");

const execFileAsync = promisify(execFile);
const {
  parseLpRequestId,
  listQueuedJobIds,
  isQueueDisabled,
  buildWarmupPrimer,
  normalizePrintRequest,
} = require("./agent-helpers.cjs");

const BASE_DIR = __dirname;
const CONFIG_PATH = path.join(BASE_DIR, "config.json");
const RENDER_PY = path.join(BASE_DIR, "render.py");
const VENV_PYTHON = path.join(BASE_DIR, "venv", "bin", "python");
const TMP_DIR = path.join(os.tmpdir(), "sugarista-print-agent");

const DEFAULT_LOCAL_PORT = Number(process.env.POS_PRINT_PORT || 9101);
const MAX_ATTEMPTS = 3;
const DRAIN_POLL_MS = 300;
// Let the printer finish cutting/feeding the previous job before streaming a
// new one into it — starting data mid-cut-cycle corrupts the leading bytes.
const GROUP_SETTLE_MS = 1500;
// After a fully failed group whose printer/queue was stuck, hold off claiming
// for this long so a dead printer does not spin the agent retry loop.
const STUCK_QUEUE_COOLDOWN_MS = 25_000;

// Set while the printer/queue is considered unavailable (stuck-queue failure);
// claim polling pauses until it elapses.
let stuckQueueCooldownUntil = 0;

// Orders printed via the local HTTP endpoint (POST /print from POS popup) or
// currently open in the POS print popup (POST /preview) so background polling
// on localhost never races or double-prints the same order.
const recentlyPrintedOrders = new Map();
const popupHeldOrders = new Map();
const PRINTED_ORDER_TTL_MS = 30 * 60 * 1000;
const POPUP_HOLD_TTL_MS = 5 * 60 * 1000;

let printQueueChain = Promise.resolve();

function withPrintLock(taskFn) {
  const next = printQueueChain.then(taskFn, taskFn);
  printQueueChain = next.catch(() => {});
  return next;
}

function rememberPrintedOrder(code) {
  if (!code || code === "—" || code === "receipt") return;
  const now = Date.now();
  recentlyPrintedOrders.set(code, now + PRINTED_ORDER_TTL_MS);
  popupHeldOrders.delete(code);
  for (const [k, exp] of recentlyPrintedOrders.entries()) {
    if (exp < now) recentlyPrintedOrders.delete(k);
  }
}

function holdOrderForPopup(code) {
  if (!code || code === "—" || code === "receipt") return;
  const now = Date.now();
  popupHeldOrders.set(code, now + POPUP_HOLD_TTL_MS);
  for (const [k, exp] of popupHeldOrders.entries()) {
    if (exp < now) popupHeldOrders.delete(k);
  }
}

function isOrderAlreadyPrinted(code) {
  if (!code) return false;
  const exp = recentlyPrintedOrders.get(code);
  if (!exp) return false;
  if (exp < Date.now()) {
    recentlyPrintedOrders.delete(code);
    return false;
  }
  return true;
}

function isOrderHeldInPopup(code) {
  if (!code) return false;
  const exp = popupHeldOrders.get(code);
  if (!exp) return false;
  if (exp < Date.now()) {
    popupHeldOrders.delete(code);
    return false;
  }
  return true;
}

/** Raised when the CUPS queue cannot play our bytes right now — retry later. */
class QueueStuckError extends Error {}

function log(...args) {
  const stamp = new Date().toISOString();
  console.log(`[${stamp}]`, ...args);
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function loadConfig() {
  if (!fs.existsSync(CONFIG_PATH)) {
    throw new Error(`config.json not found at ${CONFIG_PATH}`);
  }
  const raw = JSON.parse(fs.readFileSync(CONFIG_PATH, "utf8"));
  const config = {
    serverUrl: String(raw.serverUrl || "http://localhost").replace(/\/+$/, ""),
    apiKey: String(raw.apiKey || ""),
    printerName: String(raw.printerName || "GA-E200I"),
    pollSeconds: Number(raw.pollSeconds || 4),
    maxJobsPerPoll: Number(raw.maxJobsPerPoll || 10),
    printTimeoutSeconds: Number(raw.printTimeoutSeconds || 60),
    localPort: Number(raw.localPort || DEFAULT_LOCAL_PORT),
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

async function renderReceipt(payload, copyType, ext = "escpos") {
  fs.mkdirSync(TMP_DIR, { recursive: true });
  const normalizedCopy = String(copyType || "customer").toLowerCase() === "barista" ? "barista" : "customer";
  const payloadWithCopy = {
    ...payload,
    copyType: normalizedCopy === "barista" ? "Barista" : "Customer",
  };
  const safeCode = String(payload?.order?.code || "receipt").replace(/[^A-Za-z0-9-]/g, "") || "receipt";
  const stamp = `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
  const payloadPath = path.join(TMP_DIR, `${safeCode}-${normalizedCopy}-${stamp}.json`);
  const outPath = path.join(TMP_DIR, `${safeCode}-${normalizedCopy}-${stamp}.${ext}`);

  fs.writeFileSync(payloadPath, JSON.stringify(payloadWithCopy), "utf8");

  try {
    await execFileAsync(pythonPath(), [RENDER_PY, payloadPath, outPath], { timeout: 30_000 });
  } finally {
    fs.rmSync(payloadPath, { force: true });
  }

  return outPath;
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
  const { stdout } = await execFileAsync("lp", args, { timeout: config.printTimeoutSeconds * 1000 });

  // `lp` success only means ACCEPTED into the spool. Verify the job actually
  // drained (left the queue = physically played out) before declaring the
  // receipt printed: a disabled queue replaying stale bytes is exactly how
  // garbled paper happened while the database claimed printed status.
  const jobId = parseLpRequestId(stdout);
  if (!jobId) {
    throw new Error(`lp did not report a job id: ${String(stdout).trim().slice(0, 120)}`);
  }

  const deadline = Date.now() + config.printTimeoutSeconds * 1000;
  while (Date.now() < deadline) {
    const queuedOut = await execFileAsync("lpstat", ["-W", "not-completed", "-o", config.printerName], { timeout: 10_000 })
      .then((r) => r.stdout)
      .catch(() => "");
    const task = listQueuedJobIds(queuedOut).includes(jobId) ? "queued" : "drained";

    if (task === "drained") {
      return { jobId, verified: true };
    }

    const pOut = await execFileAsync("lpstat", ["-p", config.printerName], { timeout: 10_000 })
      .then((r) => r.stdout)
      .catch(() => "");
    if (isQueueDisabled(pOut)) {
      throw new QueueStuckError(`queue "${config.printerName}" is disabled while job ${jobId} sits unprinted (printer offline / paper out?)`);
    }

    await sleep(DRAIN_POLL_MS);
  }

  throw new QueueStuckError(`job ${jobId} still queued after ${config.printTimeoutSeconds}s — queue is stalled`);
}

/**
 * Before sending fresh bytes, make sure the queue is idle and healthy. A job
 * stuck (or dirty) in a recovered queue is how the printer replayed garbage:
 * any pre-existing non-completed job here can only be our own leftovers from
 * an interrupted attempt, so wait briefly for it to finish and otherwise drop
 * it before starting from a clean state.
 */
async function waitForIdleQueue(config, waitMs = 15_000) {
  const deadline = Date.now() + waitMs;
  for (;;) {
    const queuedOut = await execFileAsync("lpstat", ["-W", "not-completed", "-o", config.printerName], { timeout: 10_000 })
      .then((r) => r.stdout)
      .catch(() => "");

    if (!listQueuedJobIds(queuedOut).length) {
      return;
    }

    const pOut = await execFileAsync("lpstat", ["-p", config.printerName], { timeout: 10_000 })
      .then((r) => r.stdout)
      .catch(() => "");
    if (isQueueDisabled(pOut)) {
      log(`Queue "${config.printerName}" disabled with stale job(s) — draining them and re-enabling.`);
      await healQueueAfterError(config);
      return;
    }

    if (Date.now() >= deadline) {
      log(`Queue still busy after ${waitMs}ms — dropping stale job(s) to keep the stream clean.`);
      await healQueueAfterError(config);
      return;
    }

    await sleep(DRAIN_POLL_MS);
  }
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
      if (attempt === 1) {
        // Fresh submission after the previous job: give the cutter/feed a
        // moment so our leading bytes never collide with the paper finish.
        await sleep(GROUP_SETTLE_MS);
        await waitForIdleQueue(config);
      } else {
        await healQueueAfterError(config);
      }

      for (const job of group) {
        const payload = JSON.parse(job.payloadJson);
        escposPaths.push(await renderReceipt(payload, copyTypeOf(job)));
      }

      const combinedPath = escposPaths[0].replace(/\.escpos$/, "-combined.escpos");
      const stream = await Promise.all(escposPaths.map((p) => fs.promises.readFile(p)));
      const primer = buildWarmupPrimer();
      fs.writeFileSync(combinedPath, Buffer.concat([primer, ...stream]));

      const { jobId, verified } = await printEscpos(config, combinedPath);
      if (verified) {
        log(`  job ${jobId} drained — physically printed.`);
      }

      escposPaths.forEach((p) => fs.rmSync(p, { force: true }));
      fs.rmSync(combinedPath, { force: true });
      return { ok: true };
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
      const stuck = error instanceof QueueStuckError;
      escposPaths.forEach((p) => fs.rmSync(p, { force: true }));
      log(`  attempt ${attempt}/${MAX_ATTEMPTS} failed for ${group[0].orderTrackingCode}: ${lastError}`);
      if (stuck) {
        // Nothing printed; the printer/queue is unavailable. Back off instead
        // of hammering — the pool picks the job back up once healthy.
        lastError = new QueueStuckError(lastError);
        break;
      }
      if (attempt < MAX_ATTEMPTS) {
        await new Promise((resolve) => setTimeout(resolve, 1500 * attempt));
      }
    }
  }

  return { ok: false, error: lastError instanceof Error ? lastError.message : lastError, stuck: lastError instanceof QueueStuckError };
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

async function printDirectPayload(config, payload, copies, trackingCode) {
  return withPrintLock(async () => {
    let lastError = null;
    const targetCopies = Array.isArray(copies) && copies.length ? copies : ["customer", "barista"];

    for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt += 1) {
      const escposPaths = [];
      try {
        if (attempt === 1) {
          await sleep(300);
          await waitForIdleQueue(config);
        } else {
          await healQueueAfterError(config);
        }

        for (const copy of targetCopies) {
          escposPaths.push(await renderReceipt(payload, copy, "escpos"));
        }

        const combinedPath = escposPaths[0].replace(/\.escpos$/, "-combined.escpos");
        const stream = await Promise.all(escposPaths.map((p) => fs.promises.readFile(p)));
        const primer = buildWarmupPrimer();
        fs.writeFileSync(combinedPath, Buffer.concat([primer, ...stream]));

        const { jobId, verified } = await printEscpos(config, combinedPath);
        if (verified) {
          log(`  [POS HTTP] job ${jobId} drained — physically printed (${targetCopies.join(" + ")}) for ${trackingCode}.`);
        }

        escposPaths.forEach((p) => fs.rmSync(p, { force: true }));
        fs.rmSync(combinedPath, { force: true });
        rememberPrintedOrder(trackingCode);
        return {
          ok: true,
          method: "escpos-raw",
          printer: config.printerName,
          copies: targetCopies,
          trackingCode,
          jobId,
          verified,
        };
      } catch (error) {
        lastError = error instanceof Error ? error.message : String(error);
        const stuck = error instanceof QueueStuckError;
        escposPaths.forEach((p) => fs.rmSync(p, { force: true }));
        log(`  [POS HTTP] attempt ${attempt}/${MAX_ATTEMPTS} failed for ${trackingCode}: ${lastError}`);
        if (stuck) {
          break;
        }
        if (attempt < MAX_ATTEMPTS) {
          await sleep(1000 * attempt);
        }
      }
    }

    throw new Error(lastError || "Print failed");
  });
}

async function renderPreviewImages(payload) {
  const [customerPath, baristaPath] = await Promise.all([
    renderReceipt(payload, "customer", "png"),
    renderReceipt(payload, "barista", "png"),
  ]);
  try {
    const [customerBuf, baristaBuf] = await Promise.all([
      fs.promises.readFile(customerPath),
      fs.promises.readFile(baristaPath),
    ]);
    return {
      customerPng: `data:image/png;base64,${customerBuf.toString("base64")}`,
      baristaPng: `data:image/png;base64,${baristaBuf.toString("base64")}`,
    };
  } finally {
    fs.rmSync(customerPath, { force: true });
    fs.rmSync(baristaPath, { force: true });
  }
}

function setCorsAndPnaHeaders(req, res) {
  const origin = req.headers.origin || "*";
  res.setHeader("Access-Control-Allow-Origin", origin);
  res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Agent-Key");
  res.setHeader("Access-Control-Allow-Private-Network", "true");
  res.setHeader("Vary", "Origin");
}

function sendJson(req, res, statusCode, data) {
  setCorsAndPnaHeaders(req, res);
  res.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8" });
  res.end(JSON.stringify(data));
}

async function readRequestJson(req) {
  const chunks = [];
  for await (const chunk of req) {
    chunks.push(chunk);
  }
  if (!chunks.length) return {};
  return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

async function getPrinterHealth(config) {
  try {
    const { stdout } = await execFileAsync("lpstat", ["-p", config.printerName], { timeout: 5_000 });
    const disabled = isQueueDisabled(stdout);
    return {
      ok: true,
      printerName: config.printerName,
      customerPrinter: config.printerName,
      baristaPrinter: config.printerName,
      printerReady: !disabled,
      printerStatus: disabled ? "disabled" : "ready",
      paperWidthMm: 80,
      printableWidthMm: 72,
      dotsPerRow: 576,
      dpi: 203,
      format: "escpos-raw",
      autoCut: true,
    };
  } catch (err) {
    return {
      ok: true,
      printerName: config.printerName,
      customerPrinter: config.printerName,
      baristaPrinter: config.printerName,
      printerReady: false,
      printerStatus: "offline",
      paperWidthMm: 80,
      printableWidthMm: 72,
      dotsPerRow: 576,
      dpi: 203,
      format: "escpos-raw",
      autoCut: true,
      warning: err instanceof Error ? err.message : String(err),
    };
  }
}

function startLocalHttpServer(config) {
  const port = config.localPort || DEFAULT_LOCAL_PORT;
  const server = http.createServer(async (req, res) => {
    setCorsAndPnaHeaders(req, res);

    if (req.method === "OPTIONS") {
      res.writeHead(204);
      res.end();
      return;
    }

    const urlPath = (req.url || "/").split("?")[0];

    try {
      if (req.method === "GET" && urlPath === "/health") {
        const health = await getPrinterHealth(config);
        sendJson(req, res, 200, { ...health, port });
        return;
      }

      if (req.method === "POST" && urlPath === "/preview") {
        const body = await readRequestJson(req);
        const { payload, trackingCode } = normalizePrintRequest(body);
        if (body.holdAutoPrint !== false && trackingCode) {
          holdOrderForPopup(trackingCode);
        }
        const previews = await renderPreviewImages(payload);
        sendJson(req, res, 200, {
          ok: true,
          trackingCode,
          ...previews,
        });
        return;
      }

      if (req.method === "POST" && urlPath === "/print") {
        const body = await readRequestJson(req);
        const { payload, copies, trackingCode } = normalizePrintRequest(body);
        log(`[POS HTTP] Printing ${copies.join(" + ")} for order ${trackingCode}…`);
        const result = await printDirectPayload(config, payload, copies, trackingCode);
        sendJson(req, res, 200, result);
        return;
      }

      if (req.method === "POST" && urlPath === "/test") {
        const { payload, copies, trackingCode } = normalizePrintRequest({
          customer: {
            cafeName: "شوگاریستا",
            address: "تهران",
            contactPhone: "021-00000000",
            trackingCode: "TEST-001",
            printedAt: new Date().toLocaleString("fa-IR", { timeZone: "Asia/Tehran" }),
            guestName: "مهمان",
            service: "سرو در محل",
            table: "۱",
            paymentMethod: "نقدی (صندوق)",
            totalAmount: 180000,
            lines: [
              {
                name: "لاته",
                sizeLabel: "متوسط",
                quantity: 1,
                unitPrice: 180000,
                lineTotal: 180000,
                modifierLabels: ["شیر بادام"],
                baristaNote: "شکر کم",
              },
            ],
          },
        });
        const result = await printDirectPayload(config, payload, copies, trackingCode);
        sendJson(req, res, 200, result);
        return;
      }

      sendJson(req, res, 404, { ok: false, error: "Not found" });
    } catch (error) {
      log(`[POS HTTP] Error on ${req.method} ${urlPath}:`, error instanceof Error ? error.message : error);
      sendJson(req, res, 500, {
        ok: false,
        error: error instanceof Error ? error.message : "Print agent error",
      });
    }
  });

  server.on("error", (err) => {
    log(`WARNING: Local HTTP print server on 127.0.0.1:${port} error: ${err.message}`);
  });

  server.listen(port, "127.0.0.1", () => {
    log(`  local HTTP : http://127.0.0.1:${port} (CORS + Private Network Access enabled for POS popup)`);
  });

  return server;
}

async function pollOnce(config) {
  if (stuckQueueCooldownUntil > Date.now()) {
    return 0;
  }
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
    const orderCode = group[0].orderTrackingCode;
    const labels = group
      .map((j) => copyTypeOf(j))
      .join(" + ");

    // If this order was already printed directly via the POS print popup (POST /print),
    // mark the DB jobs as printed immediately so it never prints twice.
    if (isOrderAlreadyPrinted(orderCode)) {
      log(`Order ${orderCode} already printed via POS popup — marking ${labels} as printed.`);
      for (const job of group) {
        await markJobResult(config, job, true, null);
      }
      continue;
    }

    // If the order is currently open in the POS print popup on localhost, skip
    // background auto-printing so the user controls printing from the popup.
    if (isOrderHeldInPopup(orderCode)) {
      log(`Order ${orderCode} is open in POS print popup — deferring background print.`);
      continue;
    }

    log(`Printing ${labels} for order ${orderCode} as one job…`);
    const result = await withPrintLock(() => processJobGroup(config, group));
    if (result.stuck) {
      // Printer/queue unavailable — leave the DB jobs pending so the next
      // polls retry from scratch once the queue is healthy again (the bytes
      // sitting in a recovered queue are exactly what prints as garbage).
      stuckQueueCooldownUntil = Date.now() + STUCK_QUEUE_COOLDOWN_MS;
      log(
        `  ⏸ printer/queue stuck for ${orderCode} (${result.error}) — ` +
        `jobs stay pending; will retry in ~${STUCK_QUEUE_COOLDOWN_MS / 1000}s.`
      );
      break;
    }
    for (const job of group) {
      await markJobResult(config, job, result.ok, result.error);
    }
    if (result.ok) {
      rememberPrintedOrder(orderCode);
      log(`  ✓ printed ${labels} for ${orderCode} (cut between copies)`);
    } else {
      log(`  ✗ gave up on ${labels} for ${orderCode}: ${result.error}`);
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

  startLocalHttpServer(config);

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

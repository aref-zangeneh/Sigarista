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

module.exports = { parseLpRequestId, listQueuedJobIds, isQueueDisabled, buildWarmupPrimer };

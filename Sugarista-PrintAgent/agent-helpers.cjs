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

module.exports = { parseLpRequestId, listQueuedJobIds, isQueueDisabled };

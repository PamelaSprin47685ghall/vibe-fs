/**
 * spawn-ledger.js — Append-only spawn registry, survives parent death.
 *
 * Every suite entry generates/inherits WANXIANG_RUN_ID; every spawn is
 * recorded with a /proc startTime fingerprint and an expiry. The reaper
 * (reaper.mjs) kills anything from dead runs at next startup.
 */

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { procStartTime } from "./process-lifecycle.js";

const SAFE_ID_RE = /^[A-Za-z0-9_.-]+$/;
function sanitizeRunId(raw) {
  if (typeof raw === "string" && SAFE_ID_RE.test(raw) && raw.length <= 128) return raw;
  return `${Date.now()}-${process.pid}-${Math.random().toString(36).slice(2, 8)}`;
}

export const LEDGER_DIR = path.join(os.tmpdir(), "wanxiang-ledger");
export const RUN_ID = sanitizeRunId(process.env.WANXIANG_RUN_ID);

const LEDGER_FILE = path.join(LEDGER_DIR, `${RUN_ID}.jsonl`);

function ensureDir() {
  try { fs.mkdirSync(LEDGER_DIR, { recursive: true }); } catch {}
}

/** Call immediately after every spawn (worker, canary, opencode serve, sh -lc). */
export function recordSpawn(pid, cmd, ttlMs = 30 * 60 * 1000) {
  if (!pid) return;
  ensureDir();
  const entry = {
    pid,
    pgid: pid, // all our spawns are detached => pid is group leader
    startTime: process.platform === "linux" ? procStartTime(pid) : null,
    cmd: String(cmd).slice(0, 300),
    runId: RUN_ID,
    expiresAt: Date.now() + ttlMs,
  };
  try { fs.appendFileSync(LEDGER_FILE, JSON.stringify(entry) + "\n"); } catch {}
}

/** Call when a child is confirmed reaped; keeps ledgers small. */
export function recordExit(pid) {
  if (!pid) return;
  ensureDir();
  try { fs.appendFileSync(LEDGER_FILE, JSON.stringify({ pid, runId: RUN_ID, exited: true }) + "\n"); } catch {}
}

export function ledgerFileFor(runId) {
  return path.join(LEDGER_DIR, `${runId}.jsonl`);
}

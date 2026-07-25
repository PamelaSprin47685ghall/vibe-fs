#!/usr/bin/env node
/**
 * reaper.mjs — Cross-run reaper. Run BEFORE every test suite.
 *
 * 1. Kill every ledger entry from non-current runs (startTime-verified).
 * 2. Sweep /proc for un-ledgered orphans matching test markers (covers the
 *    crash-before-ledger-write window), older than ORPHAN_MIN_AGE_MS.
 * 3. Refuse to start if free memory is below MIN_FREE_MB (override: --force).
 */

import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { LEDGER_DIR, RUN_ID } from "./spawn-ledger.js";
import { pidIsAlive, procStartTime, groupMembers } from "./process-lifecycle.js";

const FORCE = process.argv.includes("--force");
const MIN_FREE_MB = Number(process.env.REAPER_MIN_FREE_MB || 2048);
const ORPHAN_MIN_AGE_MS = Number(process.env.REAPER_ORPHAN_MIN_AGE_MS || 60000);
// Must be specific enough to never match a human-run process.
const ORPHAN_MARKERS = ["oc-e2e-", "tests-next/worker.js", "wanxiang-ledger", ".wanxiangshu-next"];

let reaped = 0;
const errors = [];

function killGroupVerified(pid, startTime) {
  try {
    if (startTime && procStartTime(pid) !== startTime) return false; // recycled pid
    process.kill(-pid, "SIGKILL");
    process.kill(pid, "SIGKILL");
    reaped++;
    return true;
  } catch {
    return false;
  }
}

function reapLedger() {
  let files = [];
  try { files = fs.readdirSync(LEDGER_DIR).filter((f) => f.endsWith(".jsonl")); } catch { return; }
  for (const file of files) {
    const full = path.join(LEDGER_DIR, file);
    if (file === `${RUN_ID}.jsonl`) continue; // never reap the current run
    const exitedPids = new Set();
    const entries = [];
    try {
      for (const line of fs.readFileSync(full, "utf8").split("\n")) {
        if (!line.trim()) continue;
        try {
          const e = JSON.parse(line);
          if (e.exited) exitedPids.add(e.pid);
          else entries.push(e);
        } catch {}
      }
    } catch { continue; }
    for (const e of entries) {
      if (exitedPids.has(e.pid)) continue;
      if (!pidIsAlive(e.pid)) continue;
      if (e.startTime && procStartTime(e.pid) !== e.startTime) continue; // pid recycled
      if (e.expiresAt && Date.now() > e.expiresAt) { killGroupVerified(e.pid, e.startTime); continue; }
      // Not expired but owner run is gone (its ledger is not the current one): still reap.
      killGroupVerified(e.pid, e.startTime);
    }
    try { fs.unlinkSync(full); } catch {}
  }
}

function sweepOrphans() {
  if (process.platform === "win32") return;
  let ps;
  try {
    ps = execSync(`ps -eo pid=,etimes=,args= 2>/dev/null`, { encoding: "utf8", maxBuffer: 16 * 1024 * 1024 });
  } catch { return; }
  for (const line of ps.split("\n")) {
    const m = line.trim().match(/^(\d+)\s+(\d+)\s+(.*)$/);
    if (!m) continue;
    const [, pidStr, etimesStr, cmd] = m;
    const pid = Number(pidStr);
    if (pid === process.pid || pid === process.ppid) continue;
    if (!ORPHAN_MARKERS.some((mark) => cmd.includes(mark))) continue;
    if (Number(etimesStr) * 1000 < ORPHAN_MIN_AGE_MS) continue; // young: likely a live run
    killGroupVerified(pid, procStartTime(pid));
  }
}

function checkMemory() {
  try {
    const meminfo = fs.readFileSync("/proc/meminfo", "utf8");
    const avail = Number(meminfo.match(/MemAvailable:\s+(\d+)/)?.[1] || 0) / 1024;
    if (avail < MIN_FREE_MB) {
      const msg = `REAPER: only ${Math.round(avail)}MB available (< ${MIN_FREE_MB}MB). ` +
        `Reaped ${reaped} leftovers; free memory or re-run with --force.`;
      if (!FORCE) { console.error(msg); process.exit(2); }
      console.error(msg + " (continuing due to --force)");
    }
  } catch {}
}

reapLedger();
sweepOrphans();
checkMemory();
if (reaped > 0) console.log(`REAPER: killed ${reaped} leftover process(es) from previous runs.`);
if (errors.length) { console.error(errors.join("\n")); process.exit(2); }

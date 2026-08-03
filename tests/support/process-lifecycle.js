/**
 * process-lifecycle.js — Unified process group termination and leak verification.
 *
 * Hardened:
 *  - zombie-aware liveness (kill(pid,0) succeeds on zombies; /proc state does not lie)
 *  - PID-reuse guard: startTime fingerprint checked before every signal
 *  - group-emptiness verification after kill: leader death is not enough
 *  - Windows taskkill without shell-isms
 */

import { execSync } from "node:child_process";
import fs from "node:fs";
import { SIGKILL_GRACE_MS } from "../e2e/time-budget.js";

// Sub-threshold and therefore not a budget: SIGTERM is a request, and this is only how long a
// well-behaved process gets to honour it before SIGKILL. It stays local because it bounds this
// function's own escalation rather than any test's progress.
const TERM_GRACE_MS = 500;

function procStat(pid) {
  try {
    const raw = fs.readFileSync(`/proc/${pid}/stat`, "utf8");
    // comm may contain spaces/parens; fields after the last ')' are stable.
    const rest = raw.slice(raw.lastIndexOf(")") + 2).split(" ");
    return { state: rest[0], pgrp: Number(rest[2]), startTime: rest[19] };
  } catch {
    return null; // process gone (or /proc unavailable race)
  }
}

export function procStartTime(pid) {
  return procStat(pid)?.startTime || null;
}

export function pidIsAlive(pid) {
  if (!pid) return false;
  if (process.platform === "linux") {
    const st = procStat(pid);
    if (!st) return false;
    return st.state !== "Z" && st.state !== "X"; // zombies are dead
  }
  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    return err.code === "EPERM";
  }
}

/** Pids still belonging to process group `pgid` (Linux/macOS). */
export function groupMembers(pgid) {
  if (!pgid || process.platform === "win32") return [];
  try {
    const out = execSync(`ps -eo pid=,pgid=,stat= 2>/dev/null`, { encoding: "utf8" });
    return out
      .split("\n")
      .map((l) => l.trim().split(/\s+/))
      .filter(([pid, g, stat]) => Number(g) === pgid && stat && !stat.startsWith("Z") && !stat.startsWith("X"))
      .map(([pid]) => Number(pid));
  } catch {
    return [];
  }
}

function waitFor(cond, timeoutMs, intervalMs = 20) {
  const start = Date.now();
  return new Promise((resolve) => {
    const tick = () => {
      if (cond()) return resolve(true);
      if (Date.now() - start >= timeoutMs) return resolve(cond());
      setTimeout(tick, intervalMs);
    };
    tick();
  });
}

/**
 * Kill the whole process group rooted at `child` (ChildProcess or bare pid).
 * Resolves when the leader is dead AND the group is empty.
 * Throws (loud, never silent) listing survivors if anything escapes.
 */
export async function terminateTree(child, { termGraceMs = TERM_GRACE_MS, killGraceMs = SIGKILL_GRACE_MS } = {}) {
  const pid = typeof child === "number" ? child : child?.pid;
  if (!pid) return;

  // Fingerprint: refuse to signal if the pid was recycled between registration and now.
  const fingerprint = process.platform === "linux" ? procStartTime(pid) : null;

  const signal = (sig) => {
    if (fingerprint && procStartTime(pid) !== fingerprint) return; // stale pid: hands off
    try {
      if (process.platform === "win32") {
        execSync(`taskkill /pid ${pid} /T /F`, { stdio: "ignore" });
      } else {
        process.kill(-pid, sig);
      }
    } catch {}
    if (typeof child === "object" && typeof child?.kill === "function") {
      try { child.kill(sig); } catch {}
    }
  };

  const fullyDead = () => {
    const leaderGone = !pidIsAlive(pid);
    if (!leaderGone) return false;
    const members = process.platform === "win32" ? [] : groupMembers(pid);
    return members.length === 0;
  };

  if (fullyDead()) return;

  signal("SIGTERM");
  if (await waitFor(fullyDead, termGraceMs)) return;

  signal("SIGKILL");
  if (await waitFor(fullyDead, killGraceMs)) return;

  const survivors = process.platform === "win32" ? [pid] : groupMembers(pid);
  throw new Error(
    `Process tree ${pid} failed to terminate within ${termGraceMs + killGraceMs}ms; ` +
    `surviving pids: ${survivors.length ? survivors.join(",") : "leader"}`
  );
}

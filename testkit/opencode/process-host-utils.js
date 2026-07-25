/**
 * process-host-utils.js — Pure helpers for ProcessHost: child lifecycle,
 * socket/PID/process-tree checks, listen-port parsing.
 *
 * Side-effect-free functions live here so the main class file stays under
 * the 200-line Kolmogorov line budget.
 */

import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { getDescendantPids } from "./process-host-checks.js";
import { terminateTree } from "../process-lifecycle.js";
import { recordSpawn, recordExit } from "../spawn-ledger.js";

export const OPENCODE_BIN = process.env.OPENCODE_BIN || "opencode";

const STDOUT_RING_MAX = 100;
const activeChildPids = new Set();

export const SIGTERM_GRACE_MS = 5000;
export const SIGKILL_GRACE_MS = 1000;
export const READY_POLL_INTERVAL_MS = 100;
export const READY_POLL_MAX_TRIES = 50;
export const PROCESS_TREE_TIMEOUT_MS = 2000;

function cleanupAllActiveChildren() {
  for (const pid of activeChildPids) {
    try {
      if (process.platform !== "win32") {
        process.kill(-pid, "SIGKILL");
      }
    } catch {}
    try { process.kill(pid, "SIGKILL"); } catch {}
  }
  activeChildPids.clear();
}

process.on("exit", cleanupAllActiveChildren);
process.on("SIGINT", () => { cleanupAllActiveChildren(); process.exit(130); });
process.on("SIGTERM", () => { cleanupAllActiveChildren(); process.exit(143); });

export function parseListenPort(listenLine) {
  const m = listenLine.match(/http:\/\/127\.0\.0\.1:(\d+)/)
    || listenLine.match(/http:\/\/localhost:(\d+)/)
    || listenLine.match(/:(\d+)/);
  return m ? Number(m[1]) : 0;
}

export function pidIsAlive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    if (err.code === "ESRCH") return false;
    if (err.code === "EPERM") return true;
    return false;
  }
}

export function ringPush(buffer, s) {
  buffer.push(s);
  if (buffer.length > STDOUT_RING_MAX) buffer.shift();
}

export async function terminateChild(child, termMs = SIGTERM_GRACE_MS, killMs = SIGKILL_GRACE_MS) {
  const pid = child?.pid;
  if (!pid) return;
  activeChildPids.delete(pid);

  try {
    const descendants = await getDescendantPids(pid);
    for (const dpid of descendants) {
      try { process.kill(dpid, "SIGKILL"); } catch {}
    }
  } catch {}

  try {
    await terminateTree(child, { termGraceMs: termMs || 500, killGraceMs: killMs || 1000 });
  } catch (err) {
    console.error(`[ProcessHost] terminateTree error: ${err.message}`);
  }
}

export async function initGitWorkspace(workDir) {
  const gitDir = path.join(workDir, ".git");
  if (fs.existsSync(gitDir)) return;
  try {
    const { execSync } = await import("node:child_process");
    execSync("git init", { cwd: workDir, stdio: "ignore" });
    execSync("git config user.email test@example.com", { cwd: workDir, stdio: "ignore" });
    execSync("git config user.name test", { cwd: workDir, stdio: "ignore" });
    fs.writeFileSync(path.join(workDir, "AGENTS.md"), "- e2e workspace\n");
    execSync("git add -A", { cwd: workDir, stdio: "ignore" });
    execSync("git commit -m init", { cwd: workDir, stdio: "ignore" });
  } catch {
    // Non-fatal.
  }
}

export function spawnOpencodeServe(workDir, env, hooks) {
  const child = spawn(
    OPENCODE_BIN,
    ["serve", "--port", "0", "--hostname", "127.0.0.1"],
    {
      cwd: workDir,
      env,
      stdio: ["pipe", "pipe", "pipe"],
      windowsHide: true,
      detached: process.platform !== "win32",
    },
  );
  if (child.pid) {
    activeChildPids.add(child.pid);
    recordSpawn(child.pid, `opencode serve ${workDir}`);
  }
  child.stdout.on("data", (chunk) => hooks.onStdoutChunk(chunk.toString()));
  child.stderr.on("data", (chunk) => hooks.onStderrChunk(chunk.toString()));
  child.on("exit", (code, signal) => {
    if (child.pid) {
      activeChildPids.delete(child.pid);
      recordExit(child.pid);
    }
    hooks.onExit(code, signal);
  });
  return child;
}

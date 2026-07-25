/**
 * process-lifecycle.js — Unified process group termination and leak verification.
 */

import { execSync } from "node:child_process";

export function pidIsAlive(pid) {
  if (!pid) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    if (err.code === "ESRCH") return false;
    if (err.code === "EPERM") return true;
    return false;
  }
}

export async function terminateTree(child, { termGraceMs = 500, killGraceMs = 1000 } = {}) {
  const pid = typeof child === "number" ? child : child?.pid;
  if (!pid) return;

  try {
    if (process.platform === "win32") {
      execSync(`taskkill /pid ${pid} /T /F 2>NUL || true`, { stdio: "ignore" });
    } else {
      process.kill(-pid, "SIGTERM");
    }
  } catch {}
  try {
    if (typeof child === "object" && typeof child?.kill === "function") {
      child.kill("SIGTERM");
    }
  } catch {}

  const termExited = await waitForExit(child, pid, termGraceMs);
  if (termExited) return;

  try {
    if (process.platform === "win32") {
      execSync(`taskkill /pid ${pid} /T /F 2>NUL || true`, { stdio: "ignore" });
    } else {
      process.kill(-pid, "SIGKILL");
    }
  } catch {}
  try {
    if (typeof child === "object" && typeof child?.kill === "function") {
      child.kill("SIGKILL");
    }
  } catch {}

  const killExited = await waitForExit(child, pid, killGraceMs);
  if (!killExited) {
    throw new Error(`Process tree ${pid} failed to terminate within ${termGraceMs + killGraceMs}ms`);
  }
}

async function waitForExit(child, pid, timeoutMs) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (typeof child === "object" && (child.exitCode !== null || child.signalCode !== null)) {
      return true;
    }
    if (!pidIsAlive(pid)) return true;
    await new Promise((r) => setTimeout(r, 20));
  }
  return !pidIsAlive(pid);
}

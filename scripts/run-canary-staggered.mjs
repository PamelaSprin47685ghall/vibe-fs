/**
 * run-canary-staggered.mjs — Runs P0 canary tests in staggered parallel mode.
 * Spawns one test per 0.5s to prevent startup process storms while allowing
 * execution to proceed in parallel.
 */

import { spawn } from "node:child_process";
import path from "node:path";
import { terminateTree } from "../testkit/process-lifecycle.js";
import { recordSpawn, recordExit, RUN_ID } from "../testkit/spawn-ledger.js";

function parsePositiveInt(value, fallback, name) {
  if (value === undefined || value === null || value === '') return fallback;
  const n = Number(value);
  if (!Number.isFinite(n) || Number.isNaN(n) || n <= 0 || n !== Math.floor(n)) {
    console.error(`CANARY: invalid ${name}=${value}; using fallback ${fallback}`);
    return fallback;
  }
  return n;
}

const CANARY_TIMEOUT_MS = parsePositiveInt(process.env.CANARY_TIMEOUT_MS, 30000, "CANARY_TIMEOUT_MS");
const CANARY_TESTS = [
  "testkit/opencode/tests/agent-dsl-canary.mjs",
  "testkit/opencode/tests/companion-canary.mjs",
  "testkit/opencode/tests/reviewer-verdict-canary.mjs",
  "testkit/opencode/tests/executor-canary.mjs",
  "testkit/opencode/tests/process-stress-canary.mjs",
  "testkit/opencode/tests/host-nudge-canary.mjs",
  "testkit/opencode/tests/host-restart-canary.mjs",
  "testkit/opencode/tests/host-abort-canary.mjs",
  "testkit/opencode/tests/companion-replacement-canary.mjs",
  "testkit/opencode/tests/fallback-canary.mjs",
  "testkit/opencode/tests/orchestrator-canary.mjs",
  "testkit/opencode/tests/orchestrator-publish-canary.mjs",
  "testkit/opencode/tests/pty-stress-canary.mjs",
  "testkit/opencode/tests/reviewer-restart-canary.mjs",
];

// Full-suite parallel isolation is the standard gate: every canary starts under
// one pool sized to the suite. A small stagger only reduces startup jitter.
const MAX_PARALLEL = parsePositiveInt(
  process.env.MAX_PARALLEL_CANARIES,
  CANARY_TESTS.length,
  "MAX_PARALLEL_CANARIES",
);
const STAGGER_DELAY_MS = parsePositiveInt(process.env.STAGGER_DELAY_MS, 50, "STAGGER_DELAY_MS");
const activeCanaryPids = new Set();

function cleanupCanaries() {
  for (const pid of activeCanaryPids) {
    try {
      if (process.platform !== "win32") {
        process.kill(-pid, "SIGKILL");
      }
    } catch {}
    try { process.kill(pid, "SIGKILL"); } catch {}
  }
  activeCanaryPids.clear();
}

process.on("exit", cleanupCanaries);
process.on("SIGINT", () => { cleanupCanaries(); process.exit(130); });
process.on("SIGTERM", () => { cleanupCanaries(); process.exit(143); });

async function runPool(items, limit, worker) {
  const results = new Array(items.length);
  let active = 0;
  const waiters = [];
  const acquire = () => {
    if (active < limit) {
      active += 1;
      return Promise.resolve();
    }
    return new Promise((resolve) => waiters.push(resolve));
  };
  const release = () => {
    const waiter = waiters.shift();
    if (waiter) waiter();
    else active -= 1;
  };

  await Promise.all(items.map(async (item, index) => {
    if (index > 0) await new Promise((resolve) => setTimeout(resolve, index * STAGGER_DELAY_MS));
    await acquire();
    try {
      results[index] = await worker(item);
    } finally {
      release();
    }
  }));
  return results;
}

function runCanary(file) {
  return new Promise((resolve) => {
    const name = path.basename(file);
    const child = spawn(process.execPath, [file], {
      stdio: ["ignore", "pipe", "pipe"],
      env: { ...process.env, CANARY_REPEAT: "1", WANXIANG_RUN_ID: RUN_ID },
      detached: process.platform !== "win32",
    });

    if (child.pid) {
      activeCanaryPids.add(child.pid);
      recordSpawn(child.pid, file);
    }

    let stdout = "";
    let stderr = "";
    let settled = false;

    const timer = setTimeout(async () => {
      if (settled) return;
      settled = true;
      try {
        await terminateTree(child, { termGraceMs: 500, killGraceMs: 1000 });
        recordExit(child.pid);
      } catch (err) {
        console.error("  ⚠ canary " + name + " cleanup: " + err.message);
      }
      if (child.pid) activeCanaryPids.delete(child.pid);
      resolve({
        file,
        name,
        code: -1,
        signal: "TIMEOUT",
        stdout,
        stderr: stderr + "\n[CANARY TIMEOUT] Process exceeded " + CANARY_TIMEOUT_MS + "ms limit",
      });
    }, CANARY_TIMEOUT_MS);

    child.stdout.on("data", (chunk) => { stdout += chunk.toString(); });
    child.stderr.on("data", (chunk) => { stderr += chunk.toString(); });

    child.on("exit", (code, signal) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      if (child.pid) {
        recordExit(child.pid);
        activeCanaryPids.delete(child.pid);
      }
      resolve({ file, name, code, signal, stdout, stderr });
    });
  });
}

async function main() {
  const repeats = Number(process.env.CANARY_REPEAT || 1);
  if (!Number.isInteger(repeats) || repeats < 1 || repeats > 3) {
    throw new Error(`CANARY_REPEAT must be an integer from 1 through 3, got ${repeats}`);
  }
  console.log(
    "Starting " + CANARY_TESTS.length + " canary tests in full-parallel isolation mode (" +
      repeats + " iteration(s), max=" + MAX_PARALLEL + ", stagger=" + STAGGER_DELAY_MS + "ms)...\n",
  );

  for (let rep = 1; rep <= repeats; rep++) {
    if (repeats > 1) console.log("--- Canary Iteration " + rep + "/" + repeats + " ---");
    console.log("\nConcurrency: " + MAX_PARALLEL + " / " + CANARY_TESTS.length + "\n");

    const results = await runPool(CANARY_TESTS, MAX_PARALLEL, (file) => {
      console.log("[Launch] " + path.basename(file));
      return runCanary(file);
    });

    let failed = false;
    for (const r of results) {
      if (r.code === 0) {
        console.log("  ✓ " + r.name + " passed");
      } else {
        failed = true;
        console.error("  ✗ " + r.name + " FAILED (code " + r.code + ", signal " + r.signal + ")");
        if (r.stdout) console.error("── stdout ──\n" + r.stdout);
        if (r.stderr) console.error("── stderr ──\n" + r.stderr);
      }
    }

    if (failed) {
      console.error("\nStaggered parallel canary suite failed on iteration " + rep + ".");
      process.exit(1);
    }
  }

  console.log("\nAll staggered parallel canary tests passed cleanly across " + repeats + " iteration(s).");
  process.exit(0);
}

main().catch((err) => {
  cleanupCanaries();
  console.error("Runner failed:", err);
  process.exit(1);
});

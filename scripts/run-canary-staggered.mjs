/**
 * run-canary-staggered.mjs — Dynamic event-staggered full-parallel canary test runner.
 *
 * All canaries run concurrently in one full-parallel pool. Each canary N launches
 * canary N+1 as soon as canary N emits its first host-ready "bark" (host listening
 * or events.connect). No fixed sleep timers; pure causal event-driven launch stagger.
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

function shuffle(array) {
  const arr = [...array];
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

const CANARY_TIMEOUT_MS = parsePositiveInt(process.env.CANARY_TIMEOUT_MS, 30000, "CANARY_TIMEOUT_MS");
const CANARY_COUNT = 16;
const CANARY_TESTS = [
  "testkit/opencode/tests/agent-dsl-canary.mjs",
  "testkit/opencode/tests/companion-canary.mjs",
  "testkit/opencode/tests/reviewer-verdict-canary.mjs",
  "testkit/opencode/tests/executor-canary.mjs",
  "testkit/opencode/tests/process-stress-canary.mjs",
  "testkit/opencode/tests/host-nudge-canary.mjs",
  "testkit/opencode/tests/host-restart-canary.mjs",
  "testkit/opencode/tests/companion-replacement-canary.mjs",
  "testkit/opencode/tests/companion-cache-canary.mjs",
  "testkit/opencode/tests/fallback-canary.mjs",
  "testkit/opencode/tests/orchestrator-canary.mjs",
  "testkit/opencode/tests/orchestrator-publish-canary.mjs",
  "testkit/opencode/tests/orchestrator-restart-publish-canary.mjs",
  "testkit/opencode/tests/pty-stress-canary.mjs",
  "testkit/opencode/tests/reviewer-restart-canary.mjs",
  "testkit/opencode/tests/inspector-oneshot-canary.mjs",
];

// Full-suite parallel isolation: all canaries share one pool. Launch order is
// shuffled each iteration. Canary N starts only after canary N-1 emits
// `[setupScenario] ready` (event-driven bark). No index-based fixed sleep.
const MAX_PARALLEL = parsePositiveInt(
  process.env.MAX_PARALLEL_CANARIES,
  CANARY_TESTS.length,
  "MAX_PARALLEL_CANARIES",
);
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

function runCanary(file, onBark) {
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
    let barked = false;

    const emitBark = () => {
      if (!barked) {
        barked = true;
        onBark?.();
      }
    };

    const timer = setTimeout(async () => {
      if (settled) return;
      settled = true;
      emitBark();
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

    const checkBark = (chunk) => {
      const str = chunk.toString();
      if (!barked && /\[setupScenario\] ready/i.test(str)) {
        emitBark();
      }
    };

    child.stdout.on("data", (chunk) => {
      stdout += chunk.toString();
      checkBark(chunk);
    });

    child.stderr.on("data", (chunk) => {
      stderr += chunk.toString();
      checkBark(chunk);
    });

    child.on("exit", (code, signal) => {
      emitBark();
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
    "Starting " + CANARY_TESTS.length + " canary tests in dynamic event-staggered full-parallel mode (" +
      repeats + " iteration(s), max=" + MAX_PARALLEL + ")...\n",
  );

  for (let rep = 1; rep <= repeats; rep++) {
    if (repeats > 1) console.log("--- Canary Iteration " + rep + "/" + repeats + " ---");
    console.log("\nConcurrency: " + MAX_PARALLEL + " / " + CANARY_TESTS.length + " (expected ~" + CANARY_COUNT + ")\n");

    const testsToRun = shuffle(CANARY_TESTS);
    const canaryPromises = [];
    let previousBarkPromise = Promise.resolve();

    for (let index = 0; index < testsToRun.length; index++) {
      const file = testsToRun[index];
      const currentPrevBark = previousBarkPromise;

      let triggerBark;
      const currentBarkPromise = new Promise((resolve) => {
        triggerBark = resolve;
      });

      // Safety fallback: if host bark is not seen within 10s, release launch gate only.
      // Does NOT mark the canary as passed — only unblocks canary N+1 spawn.
      const barkTimer = setTimeout(triggerBark, 10000);
      const onBark = () => {
        clearTimeout(barkTimer);
        triggerBark();
      };

      previousBarkPromise = currentBarkPromise;

      const p = (async () => {
        if (index > 0) {
          await currentPrevBark;
        }
        console.log("[Launch] " + path.basename(file));
        return runCanary(file, onBark);
      })();

      canaryPromises.push(p);
    }

    const results = await Promise.all(canaryPromises);

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

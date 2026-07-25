/**
 * run-canary-staggered.mjs — Runs P0 canary tests in staggered parallel mode.
 * Spawns one test per 0.5s to prevent startup process storms while allowing
 * execution to proceed in parallel.
 */

import { spawn } from "node:child_process";
import path from "node:path";
import { terminateTree } from "../testkit/process-lifecycle.js";

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
  "testkit/opencode/tests/pty-stress-canary.mjs",
  "testkit/opencode/tests/reviewer-restart-canary.mjs",
];

const STAGGER_DELAY_MS = 500;
const CANARY_TIMEOUT_MS = Number(process.env.CANARY_TIMEOUT_MS || 180000);
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

function runCanary(file) {
  return new Promise((resolve) => {
    const name = path.basename(file);
    const child = spawn(process.execPath, [file], {
      stdio: ["ignore", "pipe", "pipe"],
      env: process.env,
      detached: process.platform !== "win32",
    });

    if (child.pid) activeCanaryPids.add(child.pid);

    let stdout = "";
    let stderr = "";
    let settled = false;

    const timer = setTimeout(async () => {
      if (settled) return;
      settled = true;
      if (child.pid) activeCanaryPids.delete(child.pid);
      try {
        await terminateTree(child, { termGraceMs: 500, killGraceMs: 1000 });
      } catch {}
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
      if (child.pid) activeCanaryPids.delete(child.pid);
      resolve({ file, name, code, signal, stdout, stderr });
    });
  });
}

async function main() {
  const repeats = Number(process.env.CANARY_REPEAT || 1);
  console.log("Starting " + CANARY_TESTS.length + " canary tests in staggered parallel mode (" + repeats + " iteration(s))...\n");

  for (let rep = 1; rep <= repeats; rep++) {
    if (repeats > 1) console.log("--- Canary Iteration " + rep + "/" + repeats + " ---");
    const promises = [];

    for (let i = 0; i < CANARY_TESTS.length; i++) {
      if (i > 0) {
        await new Promise((r) => setTimeout(r, STAGGER_DELAY_MS));
      }
      const file = CANARY_TESTS[i];
      const offset = (i * 0.5).toFixed(1);
      console.log("[Launch +" + offset + "s] " + path.basename(file));
      promises.push(runCanary(file));
    }

    console.log("\nAll canary tests launched. Awaiting completions...\n");
    const results = await Promise.all(promises);

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

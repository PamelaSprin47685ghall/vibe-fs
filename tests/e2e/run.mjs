/**
 * tests/e2e/run.mjs — Dynamic event-staggered full-parallel e2e scenario runner.
 *
 * A bounded worker pool runs the scenarios. Each admitted scenario N launches the next
 * admitted scenario as soon as N emits its first host-ready "bark" (host listening or
 * events.connect). No fixed sleep timers; pure causal event-driven launch stagger.
 *
 * Repeat: `node tests/e2e/run.mjs --repeat 3` (default 1, legal 1–3).
 */

import { spawn, spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { terminateTree } from "./support/process-lifecycle.js";
import { recordSpawn, recordExit, RUN_ID } from "./support/spawn-ledger.js";
import {
  CANARY_READY_MS,
  CANARY_TIMEOUT_MS,
  READINESS_STAGE_MS,
} from "./support/time-budget.js";
import { ReadinessLadder, READINESS_STAGES } from "./support/readiness.js";
import {
  CANARY_TESTS,
  CANARY_SUFFIX,
  CANARY_MAX_PARALLEL,
  CANARY_STARTUP_WIDTH,
} from "./support/manifest.mjs";

const E2E_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

function parsePositiveInt(value, fallback, name) {
  if (value === undefined || value === null || value === '') return fallback;
  const n = Number(value);
  if (!Number.isFinite(n) || Number.isNaN(n) || n <= 0 || n !== Math.floor(n)) {
    console.error(`e2e: invalid ${name}=${value}; using fallback ${fallback}`);
    return fallback;
  }
  return n;
}

function parseRepeat(argv) {
  const idx = argv.indexOf('--repeat');
  if (idx === -1) return 1;
  const raw = argv[idx + 1];
  const n = Number(raw);
  if (!Number.isInteger(n) || n < 1 || n > 3) {
    throw new Error(`--repeat must be an integer from 1 through 3, got ${raw}`);
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

// The wall-clock fallback; the scenario-local watchdog is the real hang criterion. The env
// override stays here rather than in the budget module so that module reads no process state.
// Named distinctly from the imported default because the two are different facts: one is the
// budget, one is what this run resolved it to.
const canaryProcessTimeoutMs = parsePositiveInt(process.env.CANARY_TIMEOUT_MS, CANARY_TIMEOUT_MS, "CANARY_TIMEOUT_MS");

// Full-suite parallel isolation: all scenarios share one pool. Launch order is
// shuffled each iteration. Scenario N starts only after scenario N-1 emits
// `[setupScenario] ready` (event-driven bark). No index-based fixed sleep.
// Bounded, not the whole suite. `Promise.all` over every scenario is not an ARCH-009 violation — that
// clause scopes to the business layer and this is harness tooling — but the clause's reason is
// attributed to VERIFY-004, and an unbounded fan-out over fifteen OpenCode processes manufactures
// exactly the resource contention that makes 「慢」 indistinguishable from 「死」. The startup
// ladder's per-stage budgets only mean something under a bound.
const MAX_PARALLEL = parsePositiveInt(
  process.env.MAX_PARALLEL_CANARIES,
  CANARY_MAX_PARALLEL,
  "MAX_PARALLEL_CANARIES",
);
/** Launch-stagger width: how many scenarios may be inside startup at once (manifest-owned). */
const STARTUP_WIDTH = parsePositiveInt(
  process.env.CANARY_STARTUP_WIDTH,
  CANARY_STARTUP_WIDTH,
  "CANARY_STARTUP_WIDTH",
);
const activeCanaryPids = new Set();
const readyGateFailures = new Set();

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

function runScenarioChild(file, onBarkSignal) {
  return new Promise((resolve) => {
    const name = path.basename(file);
    const startedAt = Date.now();
    let readyAt = null;
    const child = spawn(process.execPath, [file], {
      stdio: ["ignore", "pipe", "pipe"],
      env: { ...process.env, WANXIANG_RUN_ID: RUN_ID, CANARY_VERBOSE: "1" },
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
    let barkTimeout = false;

    const emitBark = (isTimeout = false) => {
      if (!barked) {
        barked = true;
        if (isTimeout) barkTimeout = true;
        if (readyAt === null) readyAt = Date.now();
        onBarkSignal?.();
      }
    };

    // The startup ladder. Each stage the child reports re-arms this; silence inside a stage is what
    // fails, not total startup time (W5). `CANARY_READY_MS` survives below as the total 兜底, which
    // is what the clause permits a wall-clock value to be.
    const ladder = new ReadinessLadder();
    let readyTimer;
    let stageStall = null;

    const armStage = () => {
      clearTimeout(readyTimer);
      readyTimer = setTimeout(() => {
        if (settled || barked) return;
        stageStall = ladder.describe();
        barkTimeout = true;
        readyGateFailures.add(file);
        emitBark(true);
      }, READINESS_STAGE_MS);
    };
    armStage();

    // Total ceiling for the climb, so a child that trickles one stage per budget forever is still
    // bounded. Distinct from the per-stage criterion and never the primary one.
    const readyCeiling = setTimeout(() => {
      if (settled || barked) return;
      stageStall = `${ladder.describe()} (total startup ceiling)`;
      barkTimeout = true;
      readyGateFailures.add(file);
      emitBark(true);
    }, CANARY_READY_MS);

    const timer = setTimeout(async () => {
      if (settled) return;
      settled = true;
      // Process timeout is not a ready-gate failure if bark already arrived.
      // Only force-release the launch gate when the child never barked.
      const hadBark = barked;
      if (!hadBark) emitBark(true);
      try {
        await terminateTree(child);
        recordExit(child.pid);
      } catch (err) {
        console.error("  ⚠ scenario " + name + " cleanup: " + err.message);
      }
      if (child.pid) activeCanaryPids.delete(child.pid);
      resolve({
        file,
        name,
        code: -1,
        signal: "TIMEOUT",
        stdout,
        stderr: stderr + "\n[SCENARIO TIMEOUT] Process exceeded " + canaryProcessTimeoutMs + "ms limit",
        barked: hadBark,
        barkTimeout: !hadBark,
        processTimeout: true,
        exitedBeforeBark: !hadBark,
        wallMs: Date.now() - startedAt,
        startupMs: readyAt === null ? null : readyAt - startedAt,
      });
    }, canaryProcessTimeoutMs);

    // Fed the ACCUMULATED buffer, not the chunk. Chunk boundaries fall wherever the pipe buffer
    // happens to break, so a marker split across two reads appears in neither one — and the symptom
    // is the stage budget expiring on a healthy startup, which reads as a hang. `observe` is
    // monotonic and re-reading a marker it already consumed advances nothing, so replaying the whole
    // buffer each time is free and removes the boundary as a variable.
    const checkBark = (accumulated) => {
      if (!barked && ladder.observe(accumulated).length > 0) armStage();

      if (!barked && /(?:^|\r?\n)\[setupScenario\] ready(?:\r?\n|$)/.test(accumulated)) {
        emitBark(false);
      }
    };

    child.stdout.on("data", (chunk) => {
      stdout += chunk.toString();
      checkBark(stdout);
    });

    child.stderr.on("data", (chunk) => {
      stderr += chunk.toString();
      checkBark(stderr);
    });

    child.on("exit", (code, signal) => {
      const exitedBeforeBark = !barked;
      emitBark(false);
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      clearTimeout(readyTimer);
      clearTimeout(readyCeiling);
      if (child.pid) {
        recordExit(child.pid);
        activeCanaryPids.delete(child.pid);
      }
      resolve({
        file,
        name,
        code,
        signal,
        stdout,
        stderr,
        barked: !exitedBeforeBark && !barkTimeout,
        barkTimeout,
        processTimeout: false,
        exitedBeforeBark,
        stageStall,
        wallMs: Date.now() - startedAt,
        startupMs: readyAt === null ? null : readyAt - startedAt,
      });
    });
  });
}

const seconds = (ms) => (ms / 1000).toFixed(1) + "s";

/**
 * Per-case wall time, split at the ready bark.
 *
 * `startup` is the ladder's cost (cold binary, host listen, health, event source) and `work` is the
 * scenario itself. One number for both would make "the suite got slower" unattributable: a slower
 * startup is machine/bootstrap work, a slower body is scenario logic, and they are fixed in
 * different places.
 */
function timing(result) {
  if (result.startupMs === null) return `(${seconds(result.wallMs)} total, never ready)`;
  return `(${seconds(result.wallMs)} total = ${seconds(result.startupMs)} startup + ${seconds(
    result.wallMs - result.startupMs,
  )} work)`;
}

/**
 * The iteration's timing distribution, printed every run.
 *
 * Critical path, not average: with a bounded pool the iteration cannot finish before its longest
 * case, so a wall-time budget is spent by a handful of scenarios. Percentiles say whether the
 * suite is uniformly slow or has a tail; occupancy (sum of case wall time over iteration wall
 * time) says whether the remedy is a faster case or a fuller pool — under MAX_PARALLEL means slots
 * sat idle waiting on the launch stagger, at MAX_PARALLEL means the pool is the bound.
 *
 * Always on. A performance number behind a flag is a number nobody reads, and the state this
 * replaces — no per-case timing at all — is why a 50s case stayed 50s.
 */
function reportDistribution(results, iterationMs) {
  const ranked = [...results].sort((left, right) => right.wallMs - left.wallMs);
  const totalCaseMs = results.reduce((sum, r) => sum + r.wallMs, 0);
  const startupMs = results.reduce((sum, r) => sum + (r.startupMs ?? 0), 0);
  const at = (fraction) => ranked[Math.min(ranked.length - 1, Math.floor(ranked.length * fraction))].wallMs;

  console.log(
    `\n  iteration ${seconds(iterationMs)} wall; ${results.length} cases, ${seconds(totalCaseMs)} case time` +
      ` (${seconds(startupMs)} of it startup); occupancy ${(totalCaseMs / iterationMs).toFixed(1)}` +
      ` of ${MAX_PARALLEL} slots`,
  );
  console.log(
    `  case wall: max ${seconds(ranked[0].wallMs)}, p90 ${seconds(at(0.1))}, median ${seconds(at(0.5))}, min ${seconds(
      ranked[ranked.length - 1].wallMs,
    )}`,
  );
  console.log('  slowest:');
  for (const r of ranked.slice(0, 5)) console.log('    ' + r.name + ' ' + timing(r));
}

async function main() {  const repeats = parseRepeat(process.argv.slice(2));
  // Pay cold-binary cost once before any ProcessHost spawn competes for timeouts.
  {
    const warm = spawnSync(process.execPath, [path.join(E2E_ROOT, "scripts/warmup-opencode.mjs")], {
      cwd: E2E_ROOT,
      stdio: "inherit",
      env: process.env,
    });
    if (warm.status !== 0) {
      throw new Error(`e2e: opencode warmup failed (exit ${warm.status ?? 1})`);
    }
  }
  console.log(
    "Starting " + CANARY_TESTS.length + " e2e scenarios in dynamic event-staggered bounded-parallel mode (" +
      repeats + " iteration(s), max=" + MAX_PARALLEL + ")...\n",
  );

  for (let rep = 1; rep <= repeats; rep++) {
    if (repeats > 1) console.log("--- Scenario Iteration " + rep + "/" + repeats + " ---");
    // One number for the suite size, and it is the manifest's length. The line this replaces
    // printed the SAME fact twice — once derived, once from a hand-maintained `CANARY_COUNT = 17` —
    // so the live output read `Concurrency: 16 / 16 (expected ~17)`: the degradation announcing
    // itself in the log it was meant to annotate. `MAX_PARALLEL` stays because it is a different
    // fact, what `MAX_PARALLEL_CANARIES` resolved to, and it is labelled as one.
    console.log(
      "\nSuite: " + CANARY_TESTS.length + " scenarios (every " + CANARY_SUFFIX + " under the manifest directory)" +
        ", MAX_PARALLEL_CANARIES=" + MAX_PARALLEL + ", startup width=" + STARTUP_WIDTH + "\n",
    );

    const testsToRun = shuffle(CANARY_TESTS);
    const results = Array(testsToRun.length);
    let nextIndex = 0;
    // Bark promises in launch order. A launch waits on the bark `CANARY_STARTUP_WIDTH` positions
    // back, so at most that many scenarios are inside startup at once: still causal (a readiness
    // bark, never a timer), just not single-file. Width 1 is the strict serialization this
    // replaces, which made the suite's wall clock the SUM of every startup.
    const barkPromises = [];
    const iterationStartedAt = Date.now();

    const runWorker = async () => {
      while (nextIndex < testsToRun.length) {
        const index = nextIndex++;
        const file = testsToRun[index];
        const currentPrevBark = barkPromises[index - STARTUP_WIDTH] ?? Promise.resolve();

        let triggerBark;
        const currentBarkPromise = new Promise((resolve) => {
          triggerBark = resolve;
        });
        const onBark = () => triggerBark();
        barkPromises[index] = currentBarkPromise;

        if (index >= STARTUP_WIDTH) {
          await currentPrevBark;
        }
        console.log("[Launch] " + path.basename(file));
        results[index] = await runScenarioChild(file, onBark);
      }
    };

    await Promise.all(Array.from({ length: MAX_PARALLEL }, runWorker));

    let failed = false;
    for (const r of results) {
      if (r.code === 0 && r.barked && !r.barkTimeout && !r.processTimeout && !r.exitedBeforeBark && !readyGateFailures.has(r.file)) {
        console.log("  ✓ " + r.name + " passed " + timing(r));
      } else {
        failed = true;
        let failReason = `code ${r.code}, signal ${r.signal}`;
        if (r.processTimeout) failReason = `process timeout (>${canaryProcessTimeoutMs}ms)` + (r.barked ? " after ready" : " before ready");
        else if (r.exitedBeforeBark) failReason = "exited before [setupScenario] ready";
        // Names the stage, not just the outcome. 「诊断必须包含「最后一次进展是什么」」 — the old
        // message said only that ready never arrived, which is true of every startup failure and
        // therefore points at none of them.
        else if (readyGateFailures.has(r.file) || r.barkTimeout) {
          failReason = `startup stalled (${r.stageStall ?? `no stage reached, ${READINESS_STAGES.length} expected`})`;
        }
        console.error("  ✗ " + r.name + " FAILED (" + failReason + ") " + timing(r));
        if (r.stdout) console.error("── stdout ──\n" + r.stdout);
        if (r.stderr) console.error("── stderr ──\n" + r.stderr);
      }
    }

    reportDistribution(results, Date.now() - iterationStartedAt);

    if (failed) {
      console.error("\nStaggered parallel e2e suite failed on iteration " + rep + ".");
      process.exit(1);
    }
  }

  console.log("\nAll staggered parallel e2e scenarios passed cleanly across " + repeats + " iteration(s).");
  process.exit(0);
}

main().catch((err) => {
  cleanupCanaries();
  console.error("Runner failed:", err);
  process.exit(1);
});

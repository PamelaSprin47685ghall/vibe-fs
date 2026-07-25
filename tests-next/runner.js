import fs from "node:fs";
import path from "node:path";
import { fork } from "node:child_process";
import { fileURLToPath } from "node:url";
import { terminateTree } from "../testkit/process-lifecycle.js";
import { recordSpawn, recordExit, RUN_ID } from "../testkit/spawn-ledger.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.join(__dirname, "..");
const workerPath = path.join(__dirname, "worker.js");
const activeWorkers = new Set();

async function stopWorker(worker) {
  if (!worker || !worker.pid) return;
  activeWorkers.delete(worker.pid);
  try {
    await terminateTree(worker, { termGraceMs: 300, killGraceMs: 500 });
    recordExit(worker.pid);
  } catch (err) {
    console.error("  ⚠ worker " + worker.pid + " cleanup: " + err.message);
  }
}

function cleanupAllWorkers() {
  for (const pid of activeWorkers) {
    try {
      if (process.platform === "win32") {
        process.kill(pid, "SIGKILL");
      } else {
        process.kill(-pid, "SIGKILL");
      }
    } catch {}
  }
  activeWorkers.clear();
}

process.on("exit", cleanupAllWorkers);
process.on("SIGINT", () => { cleanupAllWorkers(); process.exit(130); });
process.on("SIGTERM", () => { cleanupAllWorkers(); process.exit(143); });

export function discoverTestExports(file) {
  return new Promise((resolve, reject) => {
    const worker = fork(workerPath, ["--discover", file], {
      cwd: repoRoot,
      detached: true,
      stdio: ["ignore", "inherit", "inherit", "ipc"],
      env: { ...process.env, WANXIANG_RUN_ID: RUN_ID }
    });
    if (worker.pid) {
      activeWorkers.add(worker.pid);
      recordSpawn(worker.pid, `worker discover ${path.basename(file)}`);
    }

    let finished = false;
    const finish = (err, result) => {
      if (finished) return;
      finished = true;
      stopWorker(worker);
      if (err) reject(err);
      else resolve(result);
    };

    worker.on("message", (msg) => {
      if (msg.status === "discovered") finish(null, msg.exports);
      else finish(new Error(msg.message || "Discovery failed"));
    });
    worker.on("error", (err) => finish(err));
    worker.on("exit", (code) => {
      if (!finished) finish(new Error("Discovery worker stopped (code " + code + ")"));
    });
  });
}

export function runTestInWorker(file, exportName, timeoutMs = 1000) {
  return new Promise((resolve, reject) => {
    const worker = fork(workerPath, [file, exportName], {
      cwd: repoRoot,
      detached: true,
      stdio: ["ignore", "inherit", "inherit", "ipc"],
      env: { ...process.env, WANXIANG_RUN_ID: RUN_ID }
    });

    if (worker.pid) {
      activeWorkers.add(worker.pid);
      recordSpawn(worker.pid, `worker ${exportName}`);
    }

    let finished = false;
    let silenceTimer;
    let absoluteTimer;
    let assertionCount = 0;
    let lastAssertionAt = Date.now();

    const silenceMs = Number(process.env.TEST_SILENCE_MS || timeoutMs);
    const absoluteMs = Number(process.env.TEST_ABSOLUTE_MS || 10000);

    const finish = (settle, value, signal) => {
      if (finished) return;
      finished = true;
      clearTimeout(silenceTimer);
      clearTimeout(absoluteTimer);
      stopWorker(worker);
      settle(value);
    };

    const resetSilenceTimer = () => {
      clearTimeout(silenceTimer);
      silenceTimer = setTimeout(() => {
        finish(
          reject,
          new Error(
            "TIMEOUT: Assertion step in '" + exportName + "' (" + path.basename(file) + ") exceeded " + silenceMs + "ms limit; " +
            "last assertion " + (Date.now() - lastAssertionAt) + "ms ago (" + assertionCount + " total)"
          )
        );
      }, silenceMs);
    };

    absoluteTimer = setTimeout(() => {
      finish(
        reject,
        new Error(
          "TIMEOUT: Absolute cap of " + absoluteMs + "ms exceeded for '" + exportName + "' (" + path.basename(file) + "); " +
          "last assertion " + (Date.now() - lastAssertionAt) + "ms ago (" + assertionCount + " total)"
        )
      );
    }, absoluteMs);

    resetSilenceTimer();

    worker.on("message", (msg) => {
      if (msg.status === "heartbeat") {
        if (!finished) {
          assertionCount++;
          lastAssertionAt = Date.now();
          resetSilenceTimer();
        }
      } else if (msg.status === "ok") {
        finish(resolve, msg.result);
      } else {
        finish(reject, new Error(msg.message));
      }
    });

    worker.on("error", (err) => {
      finish(reject, err);
    });

    worker.on("exit", (code, signal) => {
      if (worker.pid) activeWorkers.delete(worker.pid);
      finish(
        reject,
        new Error("Worker stopped before reporting a result (exit code " + code + ", signal " + signal + ")")
      );
    });
  });
}

function compiledTestDir(args = process.argv.slice(2)) {
  const option = args.indexOf("--build-dir");
  if (option < 0) return path.join(repoRoot, "build/tests-next");

  const value = args[option + 1];
  if (!value || value.startsWith("--")) throw new Error("--build-dir requires a directory");
  return path.resolve(repoRoot, value);
}

async function runTests() {
  let passed = 0;
  let failed = 0;
  let skipped = 0;
  const errors = [];
  const suiteStart = Date.now();
  const suiteBudgetMs = Number(process.env.TESTS_NEXT_BUDGET_MS || 300000);

  function findJsFiles(dir) {
    let results = [];
    const list = fs.readdirSync(dir);
    for (const file of list) {
      const fullPath = path.join(dir, file);
      const stat = fs.statSync(fullPath);
      if (stat.isDirectory()) {
        if (file !== "fable_modules" && file !== "node_modules" && file !== "fixtures") {
          results = results.concat(findJsFiles(fullPath));
        }
      } else if (file.endsWith(".js") && !file.endsWith("TestSupport.js") && !file.endsWith("GateSupport.js") && !file.endsWith("Signatures.js") && !file.endsWith("Assert.js") && !file.endsWith("EventDrivenHarness.js") && !file.endsWith("worker.js") && !file.includes(".nuget")) {
        results.push(fullPath);
      }
    }
    return results;
  }

  const buildDir = compiledTestDir();
  if (!fs.existsSync(buildDir)) {
    throw new Error("Compiled Fable test directory not found: " + buildDir);
  }

  const testFiles = findJsFiles(buildDir).filter((file) => {
    const sourcePath = path.join(repoRoot, "tests-next", path.relative(buildDir, file).replace(/\.js$/, ".fs"));
    return fs.existsSync(sourcePath);
  });
  if (testFiles.length === 0) {
    throw new Error("No compiled Fable test files found in " + buildDir);
  }
  console.log("Found " + testFiles.length + " compiled Fable test files in " + buildDir);

  for (const file of testFiles) {
    const rel = path.relative(__dirname, file);
    if (Date.now() - suiteStart > suiteBudgetMs) {
      console.warn("  ⚠ Suite budget of " + suiteBudgetMs + "ms reached. Skipping remaining tests in " + rel);
      skipped++;
      continue;
    }
    try {
      const exportKeys = await discoverTestExports(file);
      for (const key of exportKeys) {
        if (Date.now() - suiteStart > suiteBudgetMs) {
          console.warn("  ⚠ Suite budget reached. Skipping " + key);
          skipped++;
          continue;
        }
        const start = Date.now();
        try {
          await runTestInWorker(file, key, 1000);
          const elapsed = Date.now() - start;
          passed++;
          console.log("  ✓ " + rel + " > " + key + " (" + elapsed + "ms)");
        } catch (err) {
          failed++;
          errors.push({ file: rel, test: key, error: err });
          console.error("  ✗ " + rel + " > " + key + ":", err.message || err);
        }
      }
    } catch (discoveryErr) {
      failed++;
      console.error("Failed to discover exports in " + rel + ":", discoveryErr.message || discoveryErr);
    }
  }

  console.log("\n========================================");
  console.log("tests-next Results: " + passed + " passed, " + failed + " failed, " + skipped + " skipped, Total " + (passed + failed + skipped));
  console.log("========================================\n");

  if (failed > 0) {
    process.exit(1);
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  runTests();
}

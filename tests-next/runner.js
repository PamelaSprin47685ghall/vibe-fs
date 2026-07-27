import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.join(__dirname, "..");

export async function discoverTestExports(file) {
  const mod = await import(pathToFileURL(file).href);
  const exports = [];
  for (const [key, value] of Object.entries(mod)) {
    if (
      typeof value === "function" &&
      !key.startsWith("_") &&
      !value.toString().startsWith("class ") &&
      !key.endsWith("_$ctor") &&
      !key.endsWith("_$reflection") &&
      !key.startsWith("check") &&
      !key.startsWith("contains")
    ) {
      exports.push(key);
    }
  }
  return exports;
}

export async function runTest(file, exportName, timeoutMs = 1000) {
  const mod = await import(pathToFileURL(file).href);
  const fn = mod[exportName];
  if (typeof fn !== "function") {
    throw new Error(`'${exportName}' is not a function in ${file}`);
  }

  let heartbeatTimer;
  let finished = false;

  const failPromise = new Promise((_, reject) => {
    const setTimer = () => {
      clearTimeout(heartbeatTimer);
      heartbeatTimer = setTimeout(() => {
        if (!finished) {
          finished = true;
          reject(new Error(`TIMEOUT: '${exportName}' (${path.basename(file)}) exceeded ${timeoutMs}ms limit`));
        }
      }, timeoutMs);
    };

    globalThis.__resetAssertionTimeout = () => {
      if (!finished) setTimer();
    };
    setTimer();
  });

  try {
    await Promise.race([fn(), failPromise]);
    finished = true;
    clearTimeout(heartbeatTimer);
  } finally {
    delete globalThis.__resetAssertionTimeout;
  }
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
      } else if (file.endsWith(".js") && !file.endsWith("TestSupport.js") && !file.endsWith("GateSupport.js") && !file.endsWith("Signatures.js") && !file.endsWith("Assert.js") && !file.endsWith("EventDrivenHarness.js") && !file.includes(".nuget")) {
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
      console.warn("  \u26a0 Suite budget of " + suiteBudgetMs + "ms reached. Skipping remaining tests in " + rel);
      skipped++;
      continue;
    }
    try {
      const exportKeys = await discoverTestExports(file);
      for (const key of exportKeys) {
        if (Date.now() - suiteStart > suiteBudgetMs) {
          console.warn("  \u26a0 Suite budget reached. Skipping " + key);
          skipped++;
          continue;
        }
        const start = Date.now();
        try {
          await runTest(file, key, 1000);
          const elapsed = Date.now() - start;
          passed++;
          console.log("  \u2713 " + rel + " > " + key + " (" + elapsed + "ms)");
        } catch (err) {
          failed++;
          errors.push({ file: rel, test: key, error: err });
          console.error("  \u2717 " + rel + " > " + key + ":", err.message || err);
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

  if (failed > 0 || skipped > 0) {
    process.exit(1);
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  runTests();
}

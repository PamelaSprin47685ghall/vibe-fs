import fs from 'node:fs';
import path from 'node:path';
import { fork } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.join(__dirname, '..');
const workerPath = path.join(__dirname, 'worker.js');

function stopWorker(worker, signal) {
  if (!worker.pid) return;

  try {
    if (process.platform === 'win32') {
      worker.kill(signal);
    } else {
      process.kill(-worker.pid, signal);
    }
  } catch (error) {
    if (error.code !== 'ESRCH') throw error;
  }
}

export function runTestInWorker(file, exportName, timeoutMs) {
  return new Promise((resolve, reject) => {
    const worker = fork(workerPath, [file, exportName], {
      cwd: repoRoot,
      detached: true,
      stdio: ['ignore', 'inherit', 'inherit', 'ipc']
    });

    let finished = false;
    let timer;
    let assertionCount = 0;
    let lastAssertionAt = Date.now();

    const finish = (settle, value, signal) => {
      if (finished) return;

      finished = true;
      clearTimeout(timer);
      stopWorker(worker, signal);
      settle(value);
    };

    const resetTimer = () => {
      clearTimeout(timer);
      timer = setTimeout(() => {
        finish(
          reject,
          new Error(
            `TIMEOUT: Assertion step in '${exportName}' (${path.basename(file)}) exceeded ${timeoutMs}ms limit; ` +
            `last assertion ${Date.now() - lastAssertionAt}ms ago (${assertionCount} total)`
          ),
          'SIGKILL'
        );
      }, timeoutMs);
    };

    resetTimer();

    worker.on('message', (msg) => {
      if (msg.status === 'heartbeat') {
        if (!finished) {
          assertionCount++;
          lastAssertionAt = Date.now();
          resetTimer();
        }
      } else if (msg.status === 'ok') {
        finish(resolve, msg.result, 'SIGTERM');
      } else {
        finish(reject, new Error(msg.message), 'SIGTERM');
      }
    });

    worker.on('error', (err) => {
      finish(reject, err, 'SIGTERM');
    });

    worker.on('exit', (code, signal) => {
      finish(
        reject,
        new Error(`Worker stopped before reporting a result (exit code ${code}, signal ${signal})`),
        'SIGTERM'
      );
    });
  });
}

async function runTests() {
  let passed = 0;
  let failed = 0;
  const errors = [];

  function findJsFiles(dir) {
    let results = [];
    const list = fs.readdirSync(dir);
    for (const file of list) {
      const fullPath = path.join(dir, file);
      const stat = fs.statSync(fullPath);
      if (stat.isDirectory()) {
        if (file !== 'fable_modules' && file !== 'node_modules' && file !== 'fixtures') {
          results = results.concat(findJsFiles(fullPath));
        }
      } else if (file.endsWith('.js') && !file.endsWith('TestSupport.js') && !file.endsWith('GateSupport.js') && !file.endsWith('JournalSupport.js') && !file.endsWith('Waiters.js') && !file.endsWith('Signatures.js') && !file.endsWith('Assert.js') && !file.endsWith('EventDrivenHarness.js') && !file.endsWith('worker.js') && !file.includes('.nuget')) {
        results.push(fullPath);
      }
    }
    return results;
  }

  const buildDir = path.join(__dirname, '../build/tests-next');
  const targetDir = fs.existsSync(buildDir) ? buildDir : __dirname;
  const categoryArg = process.argv[2];
  let filterFn = () => true;
  if (categoryArg) {
    const cat = categoryArg.toLowerCase();
    if (cat === 'unit' || cat === 'l0') {
      filterFn = (rel) => !rel.includes('Integration') && !rel.includes('E2E');
    } else if (cat === 'integration' || cat === 'l2') {
      filterFn = (rel) => rel.includes('Integration');
    } else if (cat === 'e2e' || cat === 'l4') {
      filterFn = (rel) => rel.includes('E2E');
    } else if (cat !== 'all') {
      filterFn = (rel) => rel.toLowerCase().includes(cat);
    }
  }

  const testFiles = findJsFiles(targetDir).filter((file) => {
    if (targetDir !== buildDir) return true;

    const sourcePath = path.join(__dirname, path.relative(buildDir, file).replace(/\.js$/, '.fs'));
    if (!fs.existsSync(sourcePath)) return false;

    const rel = path.relative(buildDir, file);
    return filterFn(rel);
  });
  console.log(`Found ${testFiles.length} test files in ${targetDir}`);

  for (const file of testFiles) {
    const rel = path.relative(__dirname, file);
    try {
      const module = await import(pathToFileURL(file).href);
      for (const [key, value] of Object.entries(module)) {
        if (typeof value === 'function' && !key.startsWith('_') && !value.toString().startsWith('class ') && !key.endsWith('_$ctor') && !key.endsWith('_$reflection') && !key.startsWith('check') && !key.startsWith('contains')) {
          const start = Date.now();
          try {
            await runTestInWorker(file, key, 1000);
            const elapsed = Date.now() - start;
            passed++;
            console.log(`  ✓ ${rel} > ${key} (${elapsed}ms)`);
          } catch (err) {
            failed++;
            errors.push({ file: rel, test: key, error: err });
            console.error(`  ✗ ${rel} > ${key}:`, err.message || err);
          }
        }
      }
    } catch (importErr) {
      console.error(`Failed to import ${rel}:`, importErr);
    }
  }

  console.log(`\n========================================`);
  console.log(`tests-next Results: ${passed} passed, ${failed} failed, Total ${passed + failed}`);
  console.log(`========================================\n`);

  if (failed > 0) {
    process.exit(1);
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  runTests();
}

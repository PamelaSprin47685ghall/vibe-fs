import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function withTimeout(promise, ms, label) {
  let timer;
  const timeoutP = new Promise((_, reject) => {
    timer = setTimeout(() => {
      reject(new Error(`TIMEOUT: Test '${label}' exceeded ${ms}ms limit`));
    }, ms);
  });
  return Promise.race([promise, timeoutP]).finally(() => clearTimeout(timer));
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
        if (file !== 'fable_modules' && file !== 'node_modules') {
          results = results.concat(findJsFiles(fullPath));
        }
      } else if (file.endsWith('.js') && !file.endsWith('TestSupport.js') && !file.endsWith('Signatures.js') && !file.endsWith('Assert.js') && !file.endsWith('EventDrivenHarness.js') && !file.includes('.nuget')) {
        results.push(fullPath);
      }
    }
    return results;
  }

  const buildDir = path.join(__dirname, '../build/tests-next');
  const targetDir = fs.existsSync(buildDir) ? buildDir : __dirname;
  const testFiles = findJsFiles(targetDir);
  console.log(`Found ${testFiles.length} test files in ${targetDir}`);

  for (const file of testFiles) {
    const rel = path.relative(__dirname, file);
    try {
      const module = await import(fileURLToPath(new URL(`file://${file}`)));
      for (const [key, value] of Object.entries(module)) {
        if (typeof value === 'function' && !key.startsWith('_') && !value.toString().startsWith('class ') && !key.endsWith('_$ctor') && !key.endsWith('_$reflection') && !key.startsWith('check') && !key.startsWith('contains')) {
          try {
            const start = Date.now();
            const res = value();
            if (res && typeof res.then === 'function') {
              await withTimeout(res, 10000, key);
            }
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

runTests();

/**
 * run-canary-staggered.mjs — Runs P0 canary tests in staggered parallel mode.
 * Spawns one test per 0.5s to prevent startup process storms while allowing
 * execution to proceed in parallel.
 */

import { spawn } from 'node:child_process';
import path from 'node:path';

const CANARY_TESTS = [
  'testkit/opencode/tests/agent-dsl-canary.mjs',
  'testkit/opencode/tests/companion-canary.mjs',
  'testkit/opencode/tests/reviewer-verdict-canary.mjs',
  'testkit/opencode/tests/executor-canary.mjs',
  'testkit/opencode/tests/process-stress-canary.mjs',
  'testkit/opencode/tests/host-nudge-canary.mjs',
  'testkit/opencode/tests/host-restart-canary.mjs',
  'testkit/opencode/tests/host-abort-canary.mjs',
  'testkit/opencode/tests/companion-replacement-canary.mjs',
  'testkit/opencode/tests/fallback-canary.mjs',
  'testkit/opencode/tests/orchestrator-canary.mjs',
  'testkit/opencode/tests/pty-stress-canary.mjs',
  'testkit/opencode/tests/reviewer-restart-canary.mjs',
];

const STAGGER_DELAY_MS = 500;

function runCanary(file) {
  return new Promise((resolve) => {
    const name = path.basename(file);
    const child = spawn(process.execPath, [file], {
      stdio: ['ignore', 'pipe', 'pipe'],
      env: process.env,
    });

    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (chunk) => { stdout += chunk.toString(); });
    child.stderr.on('data', (chunk) => { stderr += chunk.toString(); });

    child.on('exit', (code, signal) => {
      resolve({ file, name, code, signal, stdout, stderr });
    });
  });
}

async function main() {
  console.log(`Starting ${CANARY_TESTS.length} canary tests in staggered parallel mode (0.5s launch interval)...\n`);
  const promises = [];

  for (let i = 0; i < CANARY_TESTS.length; i++) {
    if (i > 0) {
      await new Promise((r) => setTimeout(r, STAGGER_DELAY_MS));
    }
    const file = CANARY_TESTS[i];
    const offset = (i * 0.5).toFixed(1);
    console.log(`[Launch +${offset}s] ${path.basename(file)}`);
    promises.push(runCanary(file));
  }

  console.log('\nAll canary tests launched. Awaiting completions...\n');
  const results = await Promise.all(promises);

  let failed = false;
  for (const r of results) {
    if (r.code === 0) {
      console.log(`  ✓ ${r.name} passed`);
    } else {
      failed = true;
      console.error(`  ✗ ${r.name} FAILED (code ${r.code}, signal ${r.signal})`);
      if (r.stdout) console.error(`── stdout ──\n${r.stdout}`);
      if (r.stderr) console.error(`── stderr ──\n${r.stderr}`);
    }
  }

  if (failed) {
    console.error('\nStaggered parallel canary suite failed.');
    process.exit(1);
  } else {
    console.log('\nAll staggered parallel canary tests passed cleanly.');
    process.exit(0);
  }
}

main().catch((err) => {
  console.error('Runner failed:', err);
  process.exit(1);
});

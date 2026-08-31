// requirements/verification-system/tests/integration/run.mjs — sequential integration suite orchestrator.
//
//   node requirements/verification-system/tests/integration/run.mjs
// Order: resources ×2 → capability plugin contracts → file-mutation-tools
//        → strength/lifecycle → persist (durable-events,
//        durable-convergence) → package/run.mjs → harness/run.mjs
// Any non-zero exit stops with exit 1.
// package suite remains independently invocable via
// node requirements/distribution/tests/integration/package/run.mjs.
//
// Silence per step: the integration default is PER_TEST bound + 5s (20s), the same verdict-fed dog
// the unit tier uses on a tighter window. A step whose test is a real F# project check declares its
// own bound in integration-node-test-steps.mjs; nothing widens the window for the other steps.
// package/harness own their own silence criterion inside their entrypoints.

import { spawnSync } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { WATCHDOG_TIMEOUT_MS } from '../e2e/support/time-budget.js'
import { superviseNodeTest } from '../e2e/support/supervise-node-test.mjs'
import { assessIntegrationEntryCoverage } from '../support/integration-entry-coverage.mjs'
import { discoverSuiteTests } from '../support/discover-suite-tests.mjs'
import { integrationNodeTestSteps } from '../support/integration-node-test-steps.mjs'
import { walk } from '../../../../scripts/lib/walk.mjs'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../../..')

// Cold opencode binary on a fresh machine / GHA pays multi-second first-launch cost.
// Warm once before any step that may spawn Host or load Host-adjacent paths.
{
  const warm = spawnSync(process.execPath, [path.join(root, 'scripts/warmup-opencode.mjs')], {
    cwd: root,
    stdio: 'inherit',
    env: process.env,
  })
  if (warm.status !== 0) {
    console.error(`integration: opencode warmup failed (exit ${warm.status ?? 1})`)
    process.exit(warm.status === null ? 1 : warm.status)
  }
}

// Plugin suite imports the full plugin graph; first import after warm can still
// exceed a tight unit-bound on cold runners. Keep silence renewals on verdicts.
const INTEGRATION_PER_TEST_TIMEOUT_MS = Math.max(
  Number(process.env.PER_TEST_TIMEOUT_MS) || 0,
  15_000,
)

/** node:test files supervised via the shared verdict-silence helper. */
const nodeTestSteps = integrationNodeTestSteps(root)

/** Child entrypoints that already own their silence criterion. */
const childSteps = [
  {
    label: 'package/run.mjs (distribution)',
    args: [path.join(root, 'requirements/distribution/tests/integration/package/run.mjs')],
  },
  {
    label: 'harness/run.mjs (verification-system)',
    args: [path.join(root, 'requirements/verification-system/tests/integration/harness/run.mjs')],
  },
]

// Integration tests are excluded from the unit runner. Child suites own only
// the tests discovered by the shared helper in their directory; every other
// *.test.mjs under tests/integration must be wired into nodeTestSteps or the
// integration entry fails closed. The discovered child set is the same set the
// child run.mjs executes, so parent delegation and child execution cannot drift.
const packageIntegrationDir = path.join(root, 'requirements/distribution/tests/integration/package')
const normalize = (file) => path.relative(root, file).split(path.sep).join('/')
const childOwnedIntegrationTests = discoverSuiteTests(packageIntegrationDir).map((name) =>
  normalize(path.join(packageIntegrationDir, name)),
)
const discoveredIntegrationTests = walk(path.join(root, 'requirements'), ['.test.mjs'])
  .map(normalize)
  .filter((file) => file.includes('/tests/integration/'))
const wiredIntegrationTests = nodeTestSteps.flatMap((step) => step.files.map(normalize))
const entryCoverage = assessIntegrationEntryCoverage({
  discoveredTests: discoveredIntegrationTests,
  wiredTests: wiredIntegrationTests,
  childOwnedTests: childOwnedIntegrationTests,
})

if (!entryCoverage.ok) {
  console.error('integration: entry coverage mismatch')
  for (const file of entryCoverage.missingFromEntry) console.error(`  unwired integration test: ${file}`)
  for (const file of entryCoverage.staleEntry) console.error(`  wired path is not a discovered integration test: ${file}`)
  for (const file of entryCoverage.duplicateWiring) console.error(`  integration test is wired more than once: ${file}`)
  process.exit(1)
}

for (const step of nodeTestSteps) {
  console.log(`\n=== integration: ${step.label} ===`)
  // One knob per step: the per-test bound feeds node:test and the verdict-silence window alike.
  // Steps that are real compiler invocations declare their own (see integration-node-test-steps.mjs);
  // everyone else keeps the tight default, so a genuine hang anywhere else still barks in ~20s.
  const perTestTimeoutMs = step.perTestTimeoutMs ?? INTEGRATION_PER_TEST_TIMEOUT_MS
  await superviseNodeTest({
    files: step.files,
    label: `tests/integration/${step.label}`,
    silenceMs: Math.max(WATCHDOG_TIMEOUT_MS, perTestTimeoutMs + 5_000),
    logPrefix: `integration:${step.label}`,
    env: {
      ...process.env,
      PER_TEST_TIMEOUT_MS: String(perTestTimeoutMs),
    },
  })
}

for (const step of childSteps) {
  console.log(`\n=== integration: ${step.label} ===`)
  const result = spawnSync(process.execPath, step.args, {
    cwd: root,
    stdio: 'inherit',
    env: process.env,
  })
  if (result.status !== 0) {
    const failed = result.status === null ? 1 : result.status
    console.error(`integration suite failed: ${step.label} (exit ${failed})`)
    process.exit(failed)
  }
}

console.log('\nintegration: all suites passed')

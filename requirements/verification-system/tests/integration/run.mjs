// requirements/verification-system/tests/integration/run.mjs — sequential integration suite orchestrator.
//
//   node requirements/verification-system/tests/integration/run.mjs
// Order: resources ×2 → plugin/manager-tool-contract → magic-todo-sink
//        → file-mutation-tools → strength/lifecycle → persist (durable-events,
//        durable-convergence) → package/run.mjs → harness/run.mjs
// Any non-zero exit stops with exit 1.
// package suite remains independently invocable via
// node requirements/distribution/tests/integration/package/run.mjs.
//
// Silence = WATCHDOG_TIMEOUT_MS (3s), same dog as e2e canary. package/harness own
// the same 3s criterion inside their entrypoints.

import { spawnSync } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { WATCHDOG_TIMEOUT_MS } from '../e2e/support/time-budget.js'
import { superviseNodeTest } from '../e2e/support/supervise-node-test.mjs'

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
const nodeTestSteps = [
  {
    label: 'resources/prompts.test.mjs (cognitive-environment)',
    files: [path.join(root, 'requirements/cognitive-environment/tests/integration/resources/prompts.test.mjs')],
  },
  {
    label: 'resources/enforcer-rulebook.test.mjs (behavior-diagnosis)',
    files: [path.join(root, 'requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs')],
  },
  {
    label: 'plugin/manager-tool-contract.test.mjs (capability-enforcement)',
    files: [path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs')],
  },
  {
    // Phase 0 Host canaries D/I — reviewing sink strategy freeze (no Magic membrane).
    label: 'plugin/magic-todo-sink-canary.test.mjs (obligation-ledger)',
    files: [path.join(root, 'requirements/obligation-ledger/tests/integration/plugin/magic-todo-sink-canary.test.mjs')],
  },
  {
    label: 'plugin/file-mutation-tools.test.mjs (repository-programming)',
    files: [path.join(root, 'requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs')],
  },
  {
    label: 'strength/lifecycle.test.mjs (speculative-investigation)',
    files: [path.join(root, 'requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs')],
  },
  // Persist owns the only durable substrate, and these three files were reachable only by
  // running them by hand — a self-test outside the gate is not a gate. `object-identity` in
  // particular pins our in-process Git object writer against the real binary.
  {
    label: 'persist (durable-events)',
    files: [
      path.join(root, 'requirements/durable-events/tests/integration/persist/object-identity.test.mjs'),
      path.join(root, 'requirements/durable-events/tests/integration/persist/leave-unread.test.mjs'),
    ],
  },
  {
    label: 'persist (durable-convergence)',
    files: [
      path.join(root, 'requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs'),
    ],
  },
]

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

for (const step of nodeTestSteps) {
  console.log(`\n=== integration: ${step.label} ===`)
  await superviseNodeTest({
    files: step.files,
    label: `tests/integration/${step.label}`,
    silenceMs: Math.max(WATCHDOG_TIMEOUT_MS, INTEGRATION_PER_TEST_TIMEOUT_MS + 5_000),
    logPrefix: `integration:${step.label}`,
    env: {
      ...process.env,
      PER_TEST_TIMEOUT_MS: String(INTEGRATION_PER_TEST_TIMEOUT_MS),
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

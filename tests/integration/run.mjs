// tests/integration/run.mjs — sequential integration suite orchestrator.
//
//   node tests/integration/run.mjs
// Order: resources ×2 → journal/boot → plugin/manager-tool-contract
//        → package/run.mjs → harness/run.mjs
// Any non-zero exit stops with exit 1.
// package suite remains independently invocable via test:package.

import { spawnSync } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../..')

/** @type {{ label: string, args: string[] }[]} */
const steps = [
  {
    label: 'resources/prompts.test.mjs',
    args: ['--test', path.join(here, 'resources/prompts.test.mjs')],
  },
  {
    label: 'resources/enforcer-catalog.test.mjs',
    args: ['--test', path.join(here, 'resources/enforcer-catalog.test.mjs')],
  },
  {
    label: 'journal/boot.test.mjs',
    args: ['--test', path.join(here, 'journal/boot.test.mjs')],
  },
  {
    label: 'plugin/manager-tool-contract.test.mjs',
    args: ['--test', path.join(here, 'plugin/manager-tool-contract.test.mjs')],
  },
  {
    label: 'package/run.mjs',
    args: [path.join(here, 'package/run.mjs')],
  },
  {
    label: 'harness/run.mjs',
    args: [path.join(here, 'harness/run.mjs')],
  },
]

let failed = 0
for (const step of steps) {
  console.log(`\n=== integration: ${step.label} ===`)
  const result = spawnSync(process.execPath, step.args, {
    cwd: root,
    stdio: 'inherit',
    env: process.env,
  })
  if (result.status !== 0) {
    failed = result.status === null ? 1 : result.status
    console.error(`integration suite failed: ${step.label} (exit ${failed})`)
    process.exit(failed)
  }
}

console.log('\nintegration: all suites passed')

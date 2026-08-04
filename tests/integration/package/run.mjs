// tests/integration/package/run.mjs — sequential package integration under 3s silence.
//
//   node tests/integration/package/run.mjs
// Requires dist/ built (npm run build) before pack/install/import checks.
//
// Silence = WATCHDOG_TIMEOUT_MS, same dog as e2e canary. Sequential: pack/install share
// npm cache; concurrent npm pack is not under test.

import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { WATCHDOG_TIMEOUT_MS } from '../../e2e/support/time-budget.js'
import { superviseNodeTest } from '../../e2e/support/supervise-node-test.mjs'

const here = path.dirname(fileURLToPath(import.meta.url))

const suites = [
  'contents.test.mjs',
  'install.test.mjs',
  'import.test.mjs',
  'resources.test.mjs',
]

for (const name of suites) {
  const file = path.join(here, name)
  console.log(`\n=== package: ${name} ===`)
  await superviseNodeTest({
    files: [file],
    label: `tests/integration/package/${name}`,
    silenceMs: WATCHDOG_TIMEOUT_MS,
    logPrefix: `package:${name}`,
  })
}

console.log('\npackage integration: all suites passed')

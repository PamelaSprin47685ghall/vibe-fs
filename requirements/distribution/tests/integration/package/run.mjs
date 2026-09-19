// requirements/distribution/tests/integration/package/run.mjs — package integration under 3s silence.
//
//   node tests/integration/package/run.mjs
// Requires dist/ built (node scripts/build.mjs) before pack/install/import checks.
//
// Silence = WATCHDOG_TIMEOUT_MS, same dog as e2e canary.
// Workspace layout and distribution checks: merged into a single supervision call.

import { readdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { HARNESS_CASE_SILENCE_MS } from '../../../../verification-system/tests/e2e/support/time-budget.js'
import { superviseNodeTest } from '../../../../verification-system/tests/e2e/support/supervise-node-test.mjs'

const here = path.dirname(fileURLToPath(import.meta.url))
const testsDir = path.resolve(here, '../..')

// Suites are discovered, not hardcoded, so an added or renamed *.test.mjs
// with integration tests is supervised automatically and cannot drift.
const suites = readdirSync(testsDir)
  .filter((name) => name.endsWith('.test.mjs'))
  .sort()
  .map((name) => path.join(testsDir, name))
  .filter((file) => {
    const text = readFileSync(file, 'utf8')
    return text.includes('integrationTest') || text.includes('WXS_TIER_INTEGRATION')
  })

if (suites.length === 0) {
  console.error(`package integration: no integration suites discovered in ${testsDir}`)
  process.exit(1)
}

console.log(`\n=== package integration (${suites.length} suites) ===`)
await superviseNodeTest({
  files: suites,
  label: 'requirements/distribution/tests/integration/package',
  silenceMs: 60000,
  logPrefix: 'package',
  env: {
    ...process.env,
    WXS_TIER_INTEGRATION: '1',
  },
})

console.log('\npackage integration: all suites passed')

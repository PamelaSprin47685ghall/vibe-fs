// tests/integration/package/run.mjs — sequential package integration suite.
//
//   node tests/integration/package/run.mjs
// Requires dist/ built (npm run build) before pack/install/import checks.

import { spawnSync } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))

const suites = [
  'contents.test.mjs',
  'install.test.mjs',
  'import.test.mjs',
  'resources.test.mjs',
]

let failed = 0
for (const name of suites) {
  const file = path.join(here, name)
  console.log(`\n=== package: ${name} ===`)
  const result = spawnSync(process.execPath, ['--test', file], {
    cwd: path.resolve(here, '../../..'),
    stdio: 'inherit',
    env: process.env,
  })
  if (result.status !== 0) {
    failed = result.status === null ? 1 : result.status
    console.error(`package suite failed: ${name} (exit ${failed})`)
  }
}

if (failed !== 0) {
  process.exit(failed)
}
console.log('\npackage integration: all suites passed')

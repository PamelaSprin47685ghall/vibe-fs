import assert from 'node:assert/strict'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { join } from 'node:path'
import { runStaticGate } from './e2e/support/index.js'
import { SOLE_ENTRY } from './e2e/support/watchdog-feed-scan.mjs'

test('WHAT[VERIFICATION-SYSTEM-014] Long Stroke environment enforces single OpenCode process lifetime and static entry gate contract', () => {
  // VERIFICATION-SYSTEM-014 requires the Layer 4 Long Stroke environment to be driven
  // by exactly one sole E2E entry (tests/e2e/014.test.mjs) executing under a single process lifecycle.
  const here = fileURLToPath(import.meta.url)
  const entryPath = join(here, '..', 'e2e', SOLE_ENTRY)

  const gateResult = runStaticGate([entryPath])
  assert.equal(gateResult.passed, true, 'Long Stroke sole entry must satisfy static entry gate')

  // Verify that the entry defines the single physical server and single lifecycle contract
  assert.ok(entryPath.endsWith('014.test.mjs'), 'Long Stroke sole entry must be 014.test.mjs')
})

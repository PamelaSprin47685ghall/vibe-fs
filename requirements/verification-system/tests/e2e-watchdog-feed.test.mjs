/**
 * VERIFY-004 permanent gate: top-level e2e tests must NOT call
 * `watchdog.advance(` / `watchdog?.advance(` directly. Only
 * tests/e2e/support/* causal primitives may feed the watchdog.
 *
 * One World scope: sole top-level entry (tests/e2e/*.test.mjs). Does not
 * require tests/e2e/cases/; missing or empty cases/ must not throw.
 */
import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import test from 'node:test'
import {
  e2eTestCaseFiles,
  scanE2EWatchdogFeed,
} from '../../../scripts/checks/e2e-watchdog-feed.mjs'

test('E2E_WATCHDOG_FEED_case_files_do_not_feed_watchdog_directly', () => {
  // Top-level only — walk must not recurse into cases/ or support/.
  const files = e2eTestCaseFiles()

  // Sole entry path is the required-exactly-one-when-present cutover target.
  assert.ok(
    files.some((file) => file.endsWith('/tests/e2e/entry.test.mjs') || file.endsWith('tests/e2e/entry.test.mjs')),
    'expected top-level sole entry e2e/entry.test.mjs (verification-system package) in scope',
  )

  // Missing/empty cases/ must be tolerated: do not walk or require that directory.
  // (If it happens to exist, it is simply out of scope.)

  for (const file of files) {
    assert.ok(existsSync(file), `e2e top-level test file missing: ${file}`)
  }

  const violations = []
  for (const file of files) {
    violations.push(...scanE2EWatchdogFeed([file]))
  }

  assert.equal(
    violations.length,
    0,
    'e2e top-level tests must not call watchdog.advance directly (VERIFY-004); they must use support causal primitives only. Violations: ' +
      JSON.stringify(violations),
  )
})

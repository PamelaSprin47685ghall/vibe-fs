/**
 * VERIFY-004 permanent gate: e2e case files and top-level e2e tests must NOT
 * call `watchdog.advance(` / `watchdog?.advance(` directly. Only
 * tests/e2e/support/* causal primitives may feed the watchdog.
 */
import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import test from 'node:test'
import { walk } from '../../../scripts/lib/walk.mjs'
import { scanE2EWatchdogFeed } from '../../../scripts/checks/e2e-watchdog-feed.mjs'

test('E2E_WATCHDOG_FEED_case_files_do_not_feed_watchdog_directly', () => {
  // VERIFY-004 scope: tests/e2e/cases/** plus top-level tests/e2e/*.test.mjs.
  // Filter out tests/e2e/support/* — the allowed causal feeders.
  const caseFiles = walk('tests/e2e/cases', ['.test.mjs'])
  const e2eFiles = walk('tests/e2e', ['.test.mjs']).filter(
    (p) => !p.includes('/support/') && !caseFiles.includes(p),
  )
  const files = [...new Set([...caseFiles, ...e2eFiles])]

  // Every listed file must exist before we read it — fail loudly on a wrong path.
  for (const file of files) {
    assert.ok(existsSync(file), `e2e case file missing: ${file}`)
  }

  const violations = []
  for (const file of files) {
    violations.push(...scanE2EWatchdogFeed([file]))
  }

  assert.equal(
    violations.length,
    0,
    'e2e case files must not call watchdog.advance directly (VERIFY-004); they must use support causal primitives only. Violations: ' +
      JSON.stringify(violations),
  )
})

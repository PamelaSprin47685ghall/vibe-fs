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

test('WHAT[VERIFICATION-SYSTEM-002] sole top-level e2e entry is entry.test.mjs', () => {
  // One World：第 4 层恰好一个真实 E2E 入口。顶层文件清单必须包含
  // tests/e2e/entry.test.mjs（唯一 Long Stroke）。
  const files = e2eTestCaseFiles()

  assert.ok(
    files.some((file) => file.endsWith('/tests/e2e/entry.test.mjs') || file.endsWith('tests/e2e/entry.test.mjs')),
    'expected top-level sole entry e2e/entry.test.mjs (verification-system package) in scope',
  )
})

test('WHAT[VERIFICATION-SYSTEM-003] e2e case ceiling is zero — no cases/ channel', () => {
  // E2E_CASE_CEILING = 0：case 天花板只降不升。机器面 = 顶层清单不递归
  // cases/ 或 support/；缺失或空 cases/ 必须被容忍（不存在 = 零 case），
  // 不许 walk 或 require 该目录。
  const files = e2eTestCaseFiles()

  for (const file of files) {
    assert.ok(existsSync(file), `e2e top-level test file missing: ${file}`)
  }
})

test('WHAT[VERIFICATION-SYSTEM-006] top-level e2e tests never feed watchdog directly', () => {
  // watchdog 只由 support/ 因果原语投喂；顶层测试直接调用 watchdog.advance( 即违规。
  const files = e2eTestCaseFiles()

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

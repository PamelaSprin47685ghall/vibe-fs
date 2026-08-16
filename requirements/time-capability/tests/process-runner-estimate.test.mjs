// Split from tests/unit/process/process-runner.test.mjs (cutover Wave 2a); owner: time-capability
//
// EXEC-011 estimate 输入校验：invalid runtime/output estimate 在 spawn 前被拒绝
// （run/spawn/kill 生命周期断言 → process-execution；large gate → output-distillation）。

import assert from 'node:assert/strict'
import test from 'node:test'

import { cancelledToken, caseOf, liveToken, payloadOf, processRequest } from '../../verification-system/tests/support/domain.mjs'
import { lib } from '../../verification-system/tests/support/domain.mjs'

const { runWithLauncher } = await import('../../../dist/Process/ProcessRunner.js')
const { fromSeconds } = await lib('TimeSpan.js')

const CTX = {
  WorkingDirectory: undefined,
  HardLimit: fromSeconds(3600),
  Environment: undefined,
}

const cmd = processRequest.command({ fileName: 'sh', args: ['-c', 'echo hi'] })
const estimate = (runtimeSeconds = 10, outputBytes = 1024, memory = 'Medium') =>
  processRequest.estimate({ runtimeSeconds, outputBytes, memory })

const okLauncher = (exitCode = 0, out = 'hello', err = '') => async (_cmd, _ct) => [
  exitCode,
  new TextEncoder().encode(out),
  new TextEncoder().encode(err),
]

// ── estimate validation ──────────────────────────────────────────────────────

test('WHAT[TIME-002] EXEC_011_rejects_nan_runtime_estimate', async () => {
  const result = await runWithLauncher(okLauncher(), cmd, estimate(NaN), CTX, liveToken())
  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(payloadOf(result)), 'ExecutionFailed')
  assert.match(String(payloadOf(payloadOf(result))), /finite positive number/)
})

test('WHAT[TIME-002] EXEC_011_rejects_zero_and_negative_runtime_estimate', async () => {
  for (const bad of [0, -5, -Infinity, Infinity]) {
    const result = await runWithLauncher(okLauncher(), cmd, estimate(bad), CTX, liveToken())
    assert.equal(caseOf(result), 'Error', String(bad))
    assert.equal(caseOf(payloadOf(result)), 'ExecutionFailed')
  }
})

test('WHAT[TIME-002] EXEC_011_rejects_negative_output_estimate', async () => {
  const result = await runWithLauncher(okLauncher(), cmd, estimate(10, -1), CTX, liveToken())
  assert.equal(caseOf(result), 'Error')
  assert.match(String(payloadOf(payloadOf(result))), /non-negative/)
})

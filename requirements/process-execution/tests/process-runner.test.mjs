// Split from tests/unit/process/process-runner.test.mjs (cutover Wave 2a); owner: process-execution
//
// EXEC-011 runWithLauncher/runWithHost 生命周期：run/spawn/kill/cancel 语义。
// runWithLauncher turns a pure launcher (cmd -> (exit, stdout, stderr)) into a host,
// so the full lifecycle — spawn, wait, timeout kill, cancellation — is exercised
// without spawning a real process.
// (estimate 拒绝 → time-capability；large gate → output-distillation。)

import assert from 'node:assert/strict'
import test from 'node:test'

import { cancelledToken, caseOf, liveToken, payloadOf, processRequest } from '../../verification-system/tests/support/domain.mjs'
import { lib } from '../../verification-system/tests/support/domain.mjs'

const { runWithLauncher, runWithHost } = await import('../../../dist/Process/ProcessRunner.js')
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

// ── happy path ───────────────────────────────────────────────────────────────

test('WHAT[PROC-010] EXEC_011_successful_run_collects_stdout_and_exit_code', async () => {
  const result = await runWithLauncher(okLauncher(0, 'the output', ''), cmd, estimate(), CTX, liveToken())
  assert.equal(caseOf(result), 'Ok')
  const outcome = payloadOf(result)
  assert.equal(caseOf(outcome), 'Completed')
  const [exitCode, stdout] = payloadOf(outcome)
  assert.equal(exitCode, 0)
  assert.equal(stdout, 'the output')
})

test('WHAT[PROC-010] EXEC_011_nonzero_exit_is_still_an_ok_outcome', async () => {
  const result = await runWithLauncher(okLauncher(3, '', 'boom'), cmd, estimate(), CTX, liveToken())
  assert.equal(caseOf(result), 'Ok')
  const outcome = payloadOf(result)
  assert.equal(caseOf(outcome), 'Completed')
  assert.equal(payloadOf(outcome)[0], 3)
})

// ── timeout ──────────────────────────────────────────────────────────────────

test('WHAT[PROC-004] EXEC_011_slow_process_is_killed_and_reports_timeout', async () => {
  // The launcher never finishes until its token is cancelled (the runner kills it).
  const hangingLauncher = (_cmd, ct) =>
    new Promise((resolve) => {
      ct.register(() => resolve([-1, new Uint8Array(0), new Uint8Array(0)]))
    })

  // 1-second runtime estimate → budget = 3s, but we shrink the applied budget
  // by giving a tiny hard limit: effective deadline = min(3s, 100ms).
  const tightCtx = { ...CTX, HardLimit: 100 }
  const result = await runWithLauncher(hangingLauncher, cmd, estimate(1, 1024), tightCtx, liveToken())

  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(payloadOf(result)), 'TimeoutExceeded')
})

// ── spawn failure / cancellation ─────────────────────────────────────────────

test('WHAT[PROC-004] EXEC_011_spawn_failure_maps_to_spawn_failed', async () => {
  const failingHost = async () => ({ tag: 1, fields: ['ENOENT: no such binary'] })
  const result = await runWithHost(failingHost, cmd, estimate(), CTX, liveToken())

  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(payloadOf(result)), 'SpawnFailed')
  assert.match(String(payloadOf(payloadOf(result))), /ENOENT/)
})

test('WHAT[PROC-004] EXEC_011_throwing_host_maps_to_execution_failed', async () => {
  const explodingHost = async () => {
    throw new Error('host exploded')
  }
  const result = await runWithHost(explodingHost, cmd, estimate(), CTX, liveToken())

  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(payloadOf(result)), 'ExecutionFailed')
  assert.match(String(payloadOf(payloadOf(result))), /host exploded/)
})

test('WHAT[PROC-006] EXEC_011_throwing_host_under_cancellation_maps_to_process_cancelled', async () => {
  const explodingHost = async () => {
    throw new Error('host exploded')
  }
  const result = await runWithHost(explodingHost, cmd, estimate(), CTX, cancelledToken())

  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(payloadOf(result)), 'ProcessCancelled')
})

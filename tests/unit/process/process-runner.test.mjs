// tests/unit/process/process-runner.test.mjs — VERIFY-009 coverage: EXEC-011 runner.
//
// runWithLauncher turns a pure launcher (cmd -> (exit, stdout, stderr)) into a host,
// so the full lifecycle — estimate validation, LargeGate, spawn, wait, timeout kill —
// is exercised without spawning a real process.

import assert from 'node:assert/strict'
import test from 'node:test'

import { cancelledToken, caseOf, liveToken, payloadOf, processRequest } from '../support/domain.mjs'

const { runWithLauncher, runWithHost } = await import('../../../dist/Process/ProcessRunner.js')
const { fromSeconds } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/TimeSpan.js')

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

test('EXEC_011_rejects_nan_runtime_estimate', async () => {
  const result = await runWithLauncher(okLauncher(), cmd, estimate(NaN), CTX, liveToken())
  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(payloadOf(result)), 'ExecutionFailed')
  assert.match(String(payloadOf(payloadOf(result))), /finite positive number/)
})

test('EXEC_011_rejects_zero_and_negative_runtime_estimate', async () => {
  for (const bad of [0, -5, -Infinity, Infinity]) {
    const result = await runWithLauncher(okLauncher(), cmd, estimate(bad), CTX, liveToken())
    assert.equal(caseOf(result), 'Error', String(bad))
    assert.equal(caseOf(payloadOf(result)), 'ExecutionFailed')
  }
})

test('EXEC_011_rejects_negative_output_estimate', async () => {
  const result = await runWithLauncher(okLauncher(), cmd, estimate(10, -1), CTX, liveToken())
  assert.equal(caseOf(result), 'Error')
  assert.match(String(payloadOf(payloadOf(result))), /non-negative/)
})

// ── happy path ───────────────────────────────────────────────────────────────

test('EXEC_011_successful_run_collects_stdout_and_exit_code', async () => {
  const result = await runWithLauncher(okLauncher(0, 'the output', ''), cmd, estimate(), CTX, liveToken())
  assert.equal(caseOf(result), 'Ok')
  const outcome = payloadOf(result)
  assert.equal(caseOf(outcome), 'Completed')
  const [exitCode, stdout] = payloadOf(outcome)
  assert.equal(exitCode, 0)
  assert.equal(stdout, 'the output')
})

test('EXEC_011_nonzero_exit_is_still_an_ok_outcome', async () => {
  const result = await runWithLauncher(okLauncher(3, '', 'boom'), cmd, estimate(), CTX, liveToken())
  assert.equal(caseOf(result), 'Ok')
  const outcome = payloadOf(result)
  assert.equal(caseOf(outcome), 'Completed')
  assert.equal(payloadOf(outcome)[0], 3)
})

// ── timeout ──────────────────────────────────────────────────────────────────

test('EXEC_011_slow_process_is_killed_and_reports_timeout', async () => {
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

test('EXEC_011_spawn_failure_maps_to_spawn_failed', async () => {
  const failingHost = async () => ({ tag: 1, fields: ['ENOENT: no such binary'] })
  const result = await runWithHost(failingHost, cmd, estimate(), CTX, liveToken())

  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(payloadOf(result)), 'SpawnFailed')
  assert.match(String(payloadOf(payloadOf(result))), /ENOENT/)
})

test('EXEC_011_throwing_host_maps_to_execution_failed', async () => {
  const explodingHost = async () => {
    throw new Error('host exploded')
  }
  const result = await runWithHost(explodingHost, cmd, estimate(), CTX, liveToken())

  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(payloadOf(result)), 'ExecutionFailed')
  assert.match(String(payloadOf(payloadOf(result))), /host exploded/)
})

test('EXEC_011_throwing_host_under_cancellation_maps_to_process_cancelled', async () => {
  const explodingHost = async () => {
    throw new Error('host exploded')
  }
  const result = await runWithHost(explodingHost, cmd, estimate(), CTX, cancelledToken())

  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(payloadOf(result)), 'ProcessCancelled')
})

// ── Large gate ───────────────────────────────────────────────────────────────

test('EXEC_011_large_estimate_acquires_and_releases_the_gate', async () => {
  const { acquire, release, getCount } = await import('../../../dist/Process/LargeGate.js')
  // Drain to a known state.
  while (getCount() === 0) release()

  let gateCountDuringRun = undefined
  const observingLauncher = async (_cmd, _ct) => {
    gateCountDuringRun = getCount()
    return [0, new Uint8Array(0), new Uint8Array(0)]
  }

  const result = await runWithLauncher(observingLauncher, cmd, estimate(10, 1024, 'Large'), CTX, liveToken())

  assert.equal(caseOf(result), 'Ok')
  assert.equal(gateCountDuringRun, 0, 'the gate is held while the large process runs')
  assert.equal(getCount(), 1, 'the gate is released after the run')
})

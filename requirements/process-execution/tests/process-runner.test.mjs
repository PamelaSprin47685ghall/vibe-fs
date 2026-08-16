// Process owner API: launcher/host lifecycle, timeout and cancellation.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  command,
  context,
  createCancellationToken,
  estimate,
  runWithLauncher,
  runWithHostLauncher,
} = await import('../../../dist/Process/Surface.js')

const CTX = context(undefined, 3_600_000)
const cmd = command('sh', ['-c', 'echo hi'], undefined, undefined)
const makeEstimate = (runtimeSeconds = 10, outputBytes = 1024, memory = 'medium') =>
  estimate(runtimeSeconds, outputBytes, memory)
const live = () => createCancellationToken(false)
const cancelled = () => createCancellationToken(true)

const okLauncher = (exitCode = 0, out = 'hello', err = '') => async (_command, _token) => [
  exitCode,
  new TextEncoder().encode(out),
  new TextEncoder().encode(err),
]

// ── happy path ───────────────────────────────────────────────────────────────

test('WHAT[PROC-010] EXEC_011_successful_run_collects_stdout_and_exit_code', async () => {
  const result = await runWithLauncher(okLauncher(0, 'the output', ''), cmd, makeEstimate(), CTX, live())
  assert.equal(result.ok, true)
  assert.deepEqual(result.value, {
    kind: 'Completed',
    exitCode: 0,
    stdout: 'the output',
    stderr: '',
    spooled: false,
  })
})

test('WHAT[PROC-010] EXEC_011_nonzero_exit_is_still_an_ok_outcome', async () => {
  const result = await runWithLauncher(okLauncher(3, '', 'boom'), cmd, makeEstimate(), CTX, live())
  assert.equal(result.ok, true)
  assert.equal(result.value.kind, 'Completed')
  assert.equal(result.value.exitCode, 3)
})

// ── timeout ──────────────────────────────────────────────────────────────────

test('WHAT[PROC-004] EXEC_011_slow_process_is_killed_and_reports_timeout', async () => {
  const hangingLauncher = (_command, token) =>
    new Promise((resolve) => {
      token.register(() => resolve([-1, new Uint8Array(0), new Uint8Array(0)]))
    })

  const tightContext = context(undefined, 100)
  const result = await runWithLauncher(
    hangingLauncher,
    cmd,
    makeEstimate(1, 1024),
    tightContext,
    live(),
  )

  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'TimeoutExceeded')
})

// ── spawn failure / cancellation ─────────────────────────────────────────────

test('WHAT[PROC-004] EXEC_011_spawn_failure_maps_to_spawn_failed', async () => {
  const failingHost = async () => ({ ok: false, error: 'ENOENT: no such binary' })
  const result = await runWithHostLauncher(failingHost, cmd, makeEstimate(), CTX, live())

  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'SpawnFailed')
  assert.match(result.error.reason, /ENOENT/)
})

test('WHAT[PROC-004] EXEC_011_throwing_host_maps_to_execution_failed', async () => {
  const explodingHost = async () => {
    throw new Error('host exploded')
  }
  const result = await runWithHostLauncher(explodingHost, cmd, makeEstimate(), CTX, live())

  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'ExecutionFailed')
  assert.match(result.error.reason, /host exploded/)
})

test('WHAT[PROC-006] EXEC_011_throwing_host_under_cancellation_maps_to_process_cancelled', async () => {
  const explodingHost = async () => {
    throw new Error('host exploded')
  }
  const result = await runWithHostLauncher(explodingHost, cmd, makeEstimate(), CTX, cancelled())

  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'ProcessCancelled')
})

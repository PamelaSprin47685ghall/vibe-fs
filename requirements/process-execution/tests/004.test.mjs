import assert from 'node:assert/strict'
import test from 'node:test'
import * as deadline from '../../../dist/Process/DeadlineSurface.js'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

// Deadline surface: instant offset semantics and timezone independence.

// Process ownership checks: bounded kill acknowledgement and typed request data.


const {
  command: ownedCommand,
  commandView,
  estimate: ownedEstimate,
  estimateView,
} = await import('../../../dist/Process/Surface.js')

// Process owner API: launcher/host lifecycle, timeout and cancellation.


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

// Process owner API: wait-for-exit deadline, kill acknowledgement and cancellation.


const { create: createDeadline } = await import('../../../dist/Process/DeadlineSurface.js')
const {
  killAckGraceMs,
  childCreate,
  childExit,
  childOnExit,
  childView,
  waitForExit,
  createCancellationToken: createWaitCancellationToken,
  cancel,
} = await import('../../../dist/Process/Surface.js')

const nowIso = () => new Date().toISOString()
const within = (promise, ms, label) =>
  Promise.race([
    promise,
    new Promise((_, reject) => {
      const timer = setTimeout(() => reject(new Error(`${label}: did not settle within ${ms}ms`)), ms)
      timer.unref?.()
    }),
  ])
const expired = () => createDeadline('2000-01-01T00:00:00Z', 1)

const killCount = (child) => childView(child).killCount

test('WHAT[PROC-004] Process_deadline_uses_explicit_offset_semantics', () => {
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', value), false)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:05Z', value), 0)
  assert.equal(deadline.isExpired('2026-01-01T00:00:05Z', value), true)
  assert.equal(deadline.isExpired('2026-01-01T00:00:06Z', value), true)

  // Same instant expressed with a non-zero offset must produce the same answer.
  assert.equal(deadline.remainingMs('2026-01-01T08:00:02+08:00', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T08:00:02+08:00', value), false)
})

test('WHAT[PROC-004] Process_deadline_is_independent_of_ambient_timezone', () => {
  const original = process.env.TZ
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  try {
    for (const zone of ['UTC', 'Asia/Shanghai', 'America/Los_Angeles']) {
      process.env.TZ = zone
      assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', value), false, `expired under TZ=${zone}`)
      assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000, `remaining under TZ=${zone}`)
    }
  } finally {
    if (original === undefined) delete process.env.TZ
    else process.env.TZ = original
  }
})

test('WHAT[PROC-004] EXEC_oneshot_completion_wait_is_bounded_by_management_deadline', () => {
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const oneshot = readFileSync(
    join(root, 'src/Wanxiangshu/Execution/Delegation/Handle/OpenCode/OneShotTool.fs'),
    'utf8',
  )

  assert.match(oneshot, /CompletionTimeoutMs\s*=\s*600_000/, 'named completion deadline')
  assert.match(oneshot, /NodeTiming\.raceExit/, 'completion races a timer, not bare Task')
  assert.doesNotMatch(
    oneshot,
    /let! output = completion\.Task\s*$/m,
    'must not bare-await completion.Task without race',
  )
  assert.match(oneshot, /AbortSession childId/, 'timeout path aborts the child session')
  assert.match(oneshot, /timed out after/, 'timeout returns Error with timeout message, not hang')
})

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

test('WHAT[PROC-004] EXEC_011_B_deadline_kills_once_then_real_exit_is_timed_out', async () => {
  let child
  child = childCreate(() => {
    setTimeout(() => childExit(child, 137), 15)
  })
  const wait = waitForExit(child, expired(), createCancellationToken(false))

  const outcome = await within(wait, 2_000, 'deadline + kill-ack')
  assert.deepEqual(outcome, { exitCode: 137, timedOut: true })
  assert.equal(killCount(child), 1, 'deadline path Kill exactly once')
})

test(
  'WHAT[PROC-004] EXEC_011_C_kill_never_acked_ends_with_minus_one_timed_out',
  { timeout: 15_000 },
  async () => {
    assert.equal(killAckGraceMs, 1_000)
    const child = childCreate(undefined)
    const wait = waitForExit(child, expired(), createCancellationToken(false))

    const outcome = await within(wait, 5_000, 'kill-ack timeout')
    assert.deepEqual(outcome, { exitCode: -1, timedOut: true })
    assert.equal(killCount(child), 1, 'deadline still Kill once before grace')
  },
)

test('WHAT[PROC-006] EXEC_011_D_mid_wait_cancellation_kills_once_and_rejects_without_hanging_on_exit', async () => {
  const child = childCreate(undefined)
  const deadline = createDeadline(nowIso(), 60_000)
  const token = createCancellationToken(false)
  const wait = waitForExit(child, deadline, token)

  setTimeout(() => cancel(token), 30)
  await assert.rejects(() => within(wait, 2_000, 'mid-wait cancel'))
  assert.equal(killCount(child), 1, 'mid-wait cancel must Kill exactly once')
})

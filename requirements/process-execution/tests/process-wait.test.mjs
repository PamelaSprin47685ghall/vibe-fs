// Process owner API: wait-for-exit deadline, kill acknowledgement and cancellation.

import assert from 'node:assert/strict'
import test from 'node:test'

const { create: createDeadline } = await import('../../../dist/Process/DeadlineSurface.js')
const { killAckGraceMs } = await import('../../../dist/Process/Surface.js')
const {
  mockWaitChild,
  childExit,
  childView,
  waitForExit,
  createCancellationToken,
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

test('WHAT[PROC-003] EXEC_011_A_natural_exit_before_deadline_returns_code_without_kill', async () => {
  const child = mockWaitChild(undefined)
  const deadline = createDeadline(nowIso(), 5_000)
  const wait = waitForExit(child, deadline, createCancellationToken(false))

  setTimeout(() => childExit(child, 42), 20)

  const outcome = await within(wait, 2_000, 'natural exit')
  assert.deepEqual(outcome, { exitCode: 42, timedOut: false })
  assert.equal(killCount(child), 0, 'natural exit must not Kill')
})

test('WHAT[PROC-004] EXEC_011_B_deadline_kills_once_then_real_exit_is_timed_out', async () => {
  let child
  child = mockWaitChild(() => {
    setTimeout(() => childExit(child, 137), 15)
  })
  const wait = waitForExit(child, expired(), createCancellationToken(false))

  const outcome = await within(wait, 2_000, 'deadline + kill-ack')
  assert.deepEqual(outcome, { exitCode: 137, timedOut: true })
  assert.equal(killCount(child), 1, 'deadline path Kill exactly once')
})

test(
  'EXEC_011_C_kill_never_acked_ends_with_minus_one_timed_out',
  { timeout: 15_000 },
  async () => {
    assert.equal(killAckGraceMs, 1_000)
    const child = mockWaitChild(undefined)
    const wait = waitForExit(child, expired(), createCancellationToken(false))

    const outcome = await within(wait, 5_000, 'kill-ack timeout')
    assert.deepEqual(outcome, { exitCode: -1, timedOut: true })
    assert.equal(killCount(child), 1, 'deadline still Kill once before grace')
  },
)

test('WHAT[PROC-006] EXEC_011_D_mid_wait_cancellation_kills_once_and_rejects_without_hanging_on_exit', async () => {
  const child = mockWaitChild(undefined)
  const deadline = createDeadline(nowIso(), 60_000)
  const token = createCancellationToken(false)
  const wait = waitForExit(child, deadline, token)

  setTimeout(() => cancel(token), 30)
  await assert.rejects(() => within(wait, 2_000, 'mid-wait cancel'))
  assert.equal(killCount(child), 1, 'mid-wait cancel must Kill exactly once')
})

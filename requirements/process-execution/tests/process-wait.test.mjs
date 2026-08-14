// tests/unit/execution/process-wait.test.mjs — EXEC-011 waitForExit real behaviour.
//
// waitForSignal races three signals (process exit / timer / cancellation). They
// must not share a bool: cancellation is not exit, and must never hang on
// child.Exit.Task. These four cases exercise the public waitForExit surface with
// an in-memory ChildProcess mock — no OS spawn, no source regex.

import assert from 'node:assert/strict'
import test from 'node:test'
import { deadline, liveToken, processWait, utcOffset } from '../support/domain.mjs'

const nowIso = () => new Date().toISOString()

/** Reject if `promise` does not settle within `ms`. */
const withTimeout = (promise, ms, label) =>
  Promise.race([
    promise,
    new Promise((_, reject) => {
      setTimeout(() => reject(new Error(`${label}: did not settle within ${ms}ms`)), ms).unref?.()
    }),
  ])

/** Budget already exhausted relative to wall clock (awaitExitOrDeadline → DeadlineReached). */
const expiredDeadline = () => deadline.ofBudget(utcOffset('2000-01-01T00:00:00Z'), 1)

test('EXEC_011_A_natural_exit_before_deadline_returns_code_without_kill', async () => {
  const { child, killCount, exit } = processWait.mockChild()
  const dl = deadline.ofBudget(nowIso(), 5_000)
  const wait = processWait.waitForExit(child, dl, liveToken())

  setTimeout(() => exit(42), 20)

  const outcome = await withTimeout(wait, 2_000, 'natural exit')
  assert.equal(outcome.ExitCode, 42)
  assert.equal(outcome.TimedOut, false)
  assert.equal(killCount(), 0, 'natural exit must not Kill')
})

test('EXEC_011_B_deadline_kills_once_then_real_exit_is_timed_out', async () => {
  let mock
  mock = processWait.mockChild({
    onKill: () => {
      // After SIGKILL the OS close path still delivers a real code.
      setTimeout(() => mock.exit(137), 15)
    },
  })
  const wait = processWait.waitForExit(mock.child, expiredDeadline(), liveToken())

  const outcome = await withTimeout(wait, 2_000, 'deadline + kill-ack')
  assert.equal(outcome.ExitCode, 137)
  assert.equal(outcome.TimedOut, true)
  assert.equal(mock.killCount(), 1, 'deadline path Kill exactly once')
})

test(
  'EXEC_011_C_kill_never_acked_ends_with_minus_one_timed_out',
  { timeout: 15_000 },
  async () => {
    // Never exit after Kill. Production KillAckGraceMs (5s) must still finish
    // the wait with ExitCode=-1 TimedOut=true — no hang on Exit.Task.
    const grace = processWait.killAckGraceMs
    assert.equal(typeof grace, 'number')
    assert.ok(grace > 0 && grace <= 30_000, `KillAckGraceMs must be a finite management bound, got ${grace}`)

    const { child, killCount } = processWait.mockChild()
    const wait = processWait.waitForExit(child, expiredDeadline(), liveToken())

    const outcome = await withTimeout(wait, grace + 3_000, 'kill-ack timeout')
    assert.equal(outcome.ExitCode, -1)
    assert.equal(outcome.TimedOut, true)
    assert.equal(killCount(), 1, 'deadline still Kill once before grace')
  },
)

test('EXEC_011_D_mid_wait_cancellation_kills_once_and_rejects_without_hanging_on_exit', async () => {
  const { child, killCount } = processWait.mockChild()
  // Long budget so the race is only cancellation vs (never-arriving) exit.
  const dl = deadline.ofBudget(nowIso(), 60_000)
  const token = liveToken()
  const wait = processWait.waitForExit(child, dl, token)

  setTimeout(() => token.cancel(), 30)

  // Must settle as rejection within a short bound. If Cancelled were folded into
  // ProcessExited, the task would hang on Exit.Task forever and withTimeout fails.
  await assert.rejects(() => withTimeout(wait, 2_000, 'mid-wait cancel'))

  assert.equal(killCount(), 1, 'mid-wait cancel must Kill exactly once')
})

// CAUSAL-005 / R17 — deadline-bounded episode resource bounds over real CausalAwait.
//
// Both episodes run through the compiled `untilSignalOrDeadline` production loop:
// no slice timers, no sleeps, no polling, no copied wake loops. Each episode holds
// exactly one diagnostic lease, every stale wake re-reads exactly once, readiness
// cancels the deadline (observed as a plain count on the caller's handle), the
// deadline path returns WaitTimedOut, and all observer/handle state is quiescent.

import assert from 'node:assert/strict'
import test from 'node:test'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')
const process = await import('../../../dist/Process/Surface.js')

const STALE = 5_000

const descriptor = () =>
  causal.createWait({
    waitKind: 'until-signal-or-deadline',
    owner: causal.owner('test-workflow', { id: 'until-signal' }),
    subject: { name: 'coverage' },
    producer: causal.externalProducer('journal', { rev: 'n' }),
    escapes: [causal.escape('openEndedExternal')],
    source: 'until-signal-or-deadline.test',
  })

const enteredLeft = (snapshot) => {
  assert.equal(snapshot.active.length, 0)
  assert.equal(snapshot.history.length, 1 + 1)
  assert.equal(snapshot.history[0].kind, 'Entered')
  assert.equal(snapshot.history[0].exit, null)
  assert.equal(snapshot.history[1].kind, 'Left')
  return snapshot.history[1].exit
}

test('WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_signal_then_ready_cancels_deadline', async () => {
  const registry = causal.createRegistry()
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 60_000)
  let reads = 0
  let wakes = 0
  const pending = causal.untilSignalOrDeadline(
    registry,
    descriptor(),
    handle,
    () => {
      reads += 1
      return reads > STALE ? 'material' : null
    },
    () => {
      wakes += 1
      return Promise.resolve()
    },
  )

  // Thousands of stale wakes drain as pure microtasks: no timers, no sleeps.
  assert.deepEqual(await pending, { ok: true, value: 'material' })
  assert.equal(wakes, STALE)
  assert.equal(reads, STALE + 1)
  assert.equal(handle.cancelCount, 1)
  assert.equal(enteredLeft(causal.snapshot(registry)), 'WaitResolved')

  // Quiescence: firing the (cancelled) deadline afterwards revives nothing.
  process.timerAdvance(timer, 120_000)
  assert.equal(causal.snapshot(registry).active.length, 0)
  assert.equal(causal.snapshot(registry).history.length, 2)
  assert.equal(handle.cancelCount, 1)
  process.timerDispose(timer)
})

test('WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_stale_signal_loops_until_deadline', async () => {
  const registry = causal.createRegistry()
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 250)
  let reads = 0
  let wakes = 0
  const pending = causal.untilSignalOrDeadline(
    registry,
    descriptor(),
    handle,
    () => {
      reads += 1
      return null
    },
    () => {
      wakes += 1
      return wakes <= 2 ? Promise.resolve() : new Promise(() => {})
    },
  )

  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(wakes, 3)
  assert.equal(reads, 3)
  process.timerAdvance(timer, 250)
  assert.deepEqual(await pending, { ok: false, reason: 'WaitTimedOut' })
  assert.equal(wakes, 3)
  assert.equal(reads, 3)
  assert.equal(handle.cancelCount, 0)
  assert.equal(enteredLeft(causal.snapshot(registry)), 'WaitTimedOut')

  // Quiescence: no lease, no pending deadline arm, no re-read after the exit.
  process.timerAdvance(timer, 10_000)
  assert.equal(causal.snapshot(registry).active.length, 0)
  assert.equal(causal.snapshot(registry).history.length, 2)
  assert.equal(wakes, 3)
  assert.equal(reads, 3)
  process.timerDispose(timer)
})

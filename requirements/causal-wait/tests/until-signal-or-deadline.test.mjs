// CAUSAL-005 — signal-first wait re-reads through one causal wait.

import assert from 'node:assert/strict'
import test from 'node:test'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')
const process = await import('../../../dist/Process/Surface.js')

const descriptor = () =>
  causal.createWait({
    waitKind: 'until-signal-or-deadline',
    owner: causal.owner('test-workflow', { id: 'until-signal' }),
    subject: { name: 'coverage' },
    producer: causal.externalProducer('journal', { rev: 'n' }),
    escapes: [causal.escape('openEndedExternal')],
    source: 'until-signal-or-deadline.test',
  })

test('WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_signal_then_ready_cancels_deadline', async () => {
  const registry = causal.createRegistry()
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 5_000)
  let ready = false
  const waiters = []
  const pending = causal.untilSignalOrDeadline(
    registry,
    descriptor(),
    handle,
    () => (ready ? 'material' : null),
    () => new Promise((resolve) => waiters.push(resolve)),
  )

  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(waiters.length, 1)
  ready = true
  waiters[0]()
  assert.deepEqual(await pending, { ok: true, value: 'material' })
  process.timerAdvance(timer, 10_000)
  assert.equal(causal.snapshot(registry).active.length, 0)
  process.timerDispose(timer)
})

test('WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_stale_signal_loops_until_deadline', async () => {
  const registry = causal.createRegistry()
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 250)
  const waiters = []
  const pending = causal.untilSignalOrDeadline(
    registry,
    descriptor(),
    handle,
    () => null,
    () => new Promise((resolve) => waiters.push(resolve)),
  )

  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(waiters.length, 1)
  waiters[0]()
  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(waiters.length, 2)
  waiters[1]()
  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(waiters.length, 3)
  process.timerAdvance(timer, 250)
  assert.deepEqual(await pending, { ok: false, reason: 'WaitTimedOut' })
  assert.ok(waiters.length >= 2, `expected ≥2 signal arms, got ${waiters.length}`)
  process.timerDispose(timer)
})

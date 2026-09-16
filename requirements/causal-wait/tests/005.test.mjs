import assert from 'node:assert/strict'
import test from 'node:test'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')
const process = await import('../../../dist/Process/Surface.js')

const owner = (id) => causal.owner('flow', { id })
const external = (id) => causal.externalProducer('capability', { id })
const waitFor = (ownerId, producerId, waitKind = 'capability') =>
  causal.createWait({
    waitKind,
    owner: owner(ownerId),
    subject: { target: producerId },
    producer: external(producerId),
    escapes: [causal.escape('processLifetime')],
    source: 'causal-wait.test',
  })

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

const activeCount = (registry) => causal.snapshot(registry).active.length

test('WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_returns_immediately_when_tryRead_ready', async () => {
  const registry = causal.createRegistry()
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 10_000)
  const result = await causal.untilSignalOrDeadline(
    registry,
    waitFor('A', 'X', 'until-signal-or-deadline'),
    handle,
    () => 42,
    () => new Promise(() => {}),
  )

  assert.deepEqual(result, { ok: true, value: 42 })
  assert.equal(activeCount(registry), 0)
  process.timerDispose(timer)
})

test('WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_signal_then_ready_cancels_deadline', async () => {
  const registry = causal.createRegistry()
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 5_000)
  let ready = false
  const waiters = []
  const pending = causal.untilSignalOrDeadline(
    registry,
    waitFor('A', 'X', 'until-signal-or-deadline'),
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
  assert.equal(activeCount(registry), 0)
  process.timerDispose(timer)
})

test('WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_stale_signal_loops_until_deadline', async () => {
  const registry = causal.createRegistry()
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 250)
  const waiters = []
  const pending = causal.untilSignalOrDeadline(
    registry,
    waitFor('A', 'X', 'until-signal-or-deadline'),
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

test('WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_signal_then_ready_cancels_deadline_loop', async () => {
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

  assert.deepEqual(await pending, { ok: true, value: 'material' })
  assert.equal(wakes, STALE)
  assert.equal(reads, STALE + 1)
  assert.equal(handle.cancelCount, 1)
  assert.equal(enteredLeft(causal.snapshot(registry)), 'WaitResolved')

  process.timerAdvance(timer, 120_000)
  assert.equal(causal.snapshot(registry).active.length, 0)
  assert.equal(causal.snapshot(registry).history.length, 2)
  assert.equal(handle.cancelCount, 1)
  process.timerDispose(timer)
})

test('WHAT[CAUSAL-005] THEOREM_untilSignalOrDeadline_stale_signal_loops_until_deadline_bounded', async () => {
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

  process.timerAdvance(timer, 10_000)
  assert.equal(causal.snapshot(registry).active.length, 0)
  assert.equal(causal.snapshot(registry).history.length, 2)
  assert.equal(wakes, 3)
  assert.equal(reads, 3)
  process.timerDispose(timer)
})

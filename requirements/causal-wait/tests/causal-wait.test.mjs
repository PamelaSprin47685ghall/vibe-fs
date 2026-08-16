// CAUSAL-002/005/006/008 — process-local causal-wait lifecycle and event-driven waits.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

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

const deferred = () => {
  let resolve
  let reject
  const promise = new Promise((resolveValue, rejectValue) => {
    resolve = resolveValue
    reject = rejectValue
  })
  return {
    promise,
    resolve,
    reject,
    cancel: () => reject(new Error('Operation Cancelled')),
  }
}

const lastExit = (registry) => {
  const history = causal.snapshot(registry).history
  assert.ok(history.length > 0, 'expected history')
  assert.ok(history.at(-1).exit, 'expected leave exit')
  return history.at(-1).exit
}

const activeCount = (registry) => causal.snapshot(registry).active.length

// ── lifecycle ────────────────────────────────────────────────────────────────

test('WHAT[CAUSAL-002] RED_1_active_wait_visible_after_enter', () => {
  const registry = causal.createRegistry()
  const descriptor = waitFor('A', 'X')
  const lease = causal.enter(registry, descriptor)
  assertOpaque(registry, 'causal registry')
  assertOpaque(lease, 'wait lease')
  const snap = causal.snapshot(registry)

  assert.equal(snap.active.length, 1)
  assert.equal(causal.ownerKey(snap.active[0].owner), 'flow:id=A')
  assert.equal(causal.producerKey(snap.active[0].producer), 'external:capability:id=X')
  assert.equal(snap.history.length, 1)
  assert.equal(snap.history[0].kind, 'Entered')

  causal.dispose(lease)
})

test('WHAT[CAUSAL-006] RED_2_resolve_clears_active_and_records_resolved', async () => {
  const registry = causal.createRegistry()
  const pending = deferred()
  const awaited = causal.awaitTask(registry, waitFor('A', 'X'), pending.promise)

  assert.equal(activeCount(registry), 1, 'visible while pending')
  pending.resolve('ok')
  assert.equal(await awaited, 'ok')
  assert.equal(activeCount(registry), 0)
  assert.equal(lastExit(registry), 'WaitResolved')
})

test('WHAT[CAUSAL-006] RED_3_fail_clears_active_and_records_failed', async () => {
  const registry = causal.createRegistry()
  const pending = deferred()
  const awaited = causal.awaitTask(registry, waitFor('A', 'X'), pending.promise)

  pending.reject(new Error('boom'))
  await assert.rejects(() => awaited, /boom/)
  assert.equal(activeCount(registry), 0)
  assert.equal(lastExit(registry), 'WaitFailed')
})

test('WHAT[CAUSAL-006] RED_4_cancel_clears_active_and_records_cancelled', async () => {
  const registry = causal.createRegistry()
  const pending = deferred()
  const awaited = causal.awaitTask(registry, waitFor('A', 'X'), pending.promise)

  pending.cancel()
  await assert.rejects(() => awaited)
  assert.equal(activeCount(registry), 0)
  assert.equal(lastExit(registry), 'WaitCancelled')
})

test('WHAT[CAUSAL-006] RED_4_cancel_message_also_classifies_as_cancelled', async () => {
  const registry = causal.createRegistry()
  const pending = deferred()
  const awaited = causal.awaitTask(registry, waitFor('A', 'X'), pending.promise)

  pending.reject(new Error('Operation Cancelled'))
  await assert.rejects(() => awaited, /Cancel/)
  assert.equal(lastExit(registry), 'WaitCancelled')
  assert.equal(activeCount(registry), 0)
})

test('WHAT[CAUSAL-006] history_capacity_bounds_ring_buffer', () => {
  const registry = causal.createRegistry(2)
  for (let i = 0; i < 3; i += 1) {
    const lease = causal.enter(registry, waitFor('A', `X${i}`))
    causal.markExit(lease, 'WaitResolved')
    causal.dispose(lease)
  }

  const history = causal.snapshot(registry).history
  assert.equal(history.length, 2)
  assert.ok(history.length <= 2)
})

test('WHAT[CAUSAL-001] RED_8_application_observer_enter_only_snapshot_via_reader', () => {
  assert.equal(causal.observerHasSnapshot(), false)
  assert.equal(causal.readerHasSnapshot(), true)

  const lease = causal.hubEnter(waitFor('hub', 'ext'))
  const viaSnapshotFn = causal.hubSnapshot()
  assert.ok(viaSnapshotFn.active.length >= 1)
  causal.markExit(lease, 'WaitResolved')
  causal.dispose(lease)
})

// ── event-driven causal wait ─────────────────────────────────────────────────

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

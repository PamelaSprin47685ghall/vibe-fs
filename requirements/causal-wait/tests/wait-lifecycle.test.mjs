// CAUSAL-002/004/006/008 — descriptor, lease and reader/writer boundaries.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')

const owner = (id) => causal.owner('flow', { id })
const descriptor = (id) =>
  causal.createWait({
    waitKind: 'lifecycle-wait',
    owner: owner(id),
    subject: { target: id },
    producer: causal.externalProducer('capability', { id }),
    escapes: [causal.escape('processLifetime')],
    source: 'wait-lifecycle.test',
  })

const lastTransition = (registry) => {
  const history = causal.snapshot(registry).history
  assert.ok(history.length > 0, 'expected history')
  return history.at(-1)
}

test('WHAT[CAUSAL-002] CAUSAL_002_descriptor_carries_typed_owner_producer_subject', () => {
  const wait = descriptor('A')
  assert.equal(causal.ownerKey(wait.owner), 'flow:id=A')
  assert.equal(causal.producerKey(wait.producer), 'external:capability:id=A')
  assert.equal(wait.waitKind, 'lifecycle-wait')
})

test('WHAT[CAUSAL-006] CAUSAL_006_dispose_defaults_to_wait_disposed', () => {
  const registry = causal.createRegistry()
  const lease = causal.enter(registry, descriptor('A'))
  assertOpaque(lease, 'wait lease')
  causal.dispose(lease)

  const transition = lastTransition(registry)
  assert.equal(transition.kind, 'Left')
  assert.equal(transition.exit, 'WaitDisposed')
  assert.equal(causal.snapshot(registry).active.length, 0)
})

test('WHAT[CAUSAL-006] CAUSAL_006_mark_exit_then_dispose_preserves_exit', () => {
  const registry = causal.createRegistry()
  const lease = causal.enter(registry, descriptor('A'))
  causal.markExit(lease, 'WaitCancelled')
  causal.dispose(lease)
  assert.equal(lastTransition(registry).exit, 'WaitCancelled')
})

test('WHAT[CAUSAL-006] CAUSAL_006_repeated_mark_exit_last_one_wins', () => {
  const registry = causal.createRegistry()
  const lease = causal.enter(registry, descriptor('A'))
  causal.markExit(lease, 'WaitResolved')
  causal.markExit(lease, 'WaitFailed')
  causal.dispose(lease)
  assert.equal(lastTransition(registry).exit, 'WaitFailed')
})

test('WHAT[CAUSAL-006] CAUSAL_006_dispose_is_idempotent_single_leave', () => {
  const registry = causal.createRegistry()
  const lease = causal.enter(registry, descriptor('A'))
  causal.dispose(lease)
  causal.dispose(lease)

  const leaves = causal.snapshot(registry).history.filter((transition) => transition.kind === 'Left')
  assert.equal(leaves.length, 1)
  assert.equal(causal.snapshot(registry).active.length, 0)
})

test('WHAT[CAUSAL-006] CAUSAL_006_reenter_is_fresh_observation_not_revival', () => {
  const registry = causal.createRegistry()
  const first = causal.enter(registry, descriptor('A'))
  const sequenceAfterEnter = causal.snapshot(registry).sequence
  causal.dispose(first)
  const sequenceAfterLeave = causal.snapshot(registry).sequence
  assert.ok(sequenceAfterLeave > sequenceAfterEnter, 'leave must advance the observation sequence')

  const second = causal.enter(registry, descriptor('A'))
  assert.ok(causal.snapshot(registry).sequence > sequenceAfterLeave)
  assert.equal(causal.snapshot(registry).active.length, 1)
  causal.dispose(second)
  assert.equal(causal.snapshot(registry).active.length, 0)
})

test('WHAT[CAUSAL-006] CAUSAL_006_history_default_capacity_is_256', () => {
  assert.equal(causal.historyCapacity(causal.createRegistry()), 256)
})

test('WHAT[CAUSAL-008] CAUSAL_008_fresh_registry_starts_empty_no_durable_state', () => {
  const snapshot = causal.snapshot(causal.createRegistry())
  assert.equal(snapshot.active.length, 0)
  assert.equal(snapshot.history.length, 0)
  assert.equal(snapshot.sequence, 0)
})

test('WHAT[CAUSAL-004] CAUSAL_004_observer_surface_has_no_snapshot', () => {
  assert.equal(causal.observerHasSnapshot(), false)
  assert.equal(causal.readerHasSnapshot(), true)
})

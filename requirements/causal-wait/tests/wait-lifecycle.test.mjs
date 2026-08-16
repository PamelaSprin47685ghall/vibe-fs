// requirements/causal-wait/tests/wait-lifecycle.test.mjs
// CAUSAL-006 / CAUSAL-004 / CAUSAL-008 — observation lifecycle termination:
// dispose defaults, MarkExit semantics, idempotent leave, no revival,
// bounded history, and observer/reader surface separation.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  causalWait,
  causalWaitHub,
  CausalWaitRegistry,
  listItems,
} from '../../verification-system/tests/support/domain.mjs'
import { CausalWaitRegistry__get_HistoryCapacity } from '../../../dist/Execution/Session/Wait/Registry.js'

const owner = (id) => causalWait.owner('flow', [['id', id]])
const external = (id) => causalWait.externalProducer('capability', [['id', id]])

const descriptor = (id) =>
  causalWait.create({
    waitKind: 'lifecycle-wait',
    owner: owner(id),
    subject: [['target', id]],
    producer: external(id),
    source: 'wait-lifecycle.test',
  })

const lastTransition = (registry) => {
  const history = listItems(registry.Snapshot().History)
  assert.ok(history.length > 0, 'expected history')
  return history.at(-1)
}

test('WHAT[CAUSAL-002] CAUSAL_002_descriptor_carries_typed_owner_producer_subject', () => {
  const wait = descriptor('A')
  assert.equal(causalWait.ownerKey(wait.Owner), 'flow:id=A')
  assert.equal(causalWait.producerKey(wait.Producer), 'external:capability:id=A')
  assert.equal(wait.WaitKind, 'lifecycle-wait')
})

test('WHAT[CAUSAL-006] CAUSAL_006_dispose_defaults_to_wait_disposed', () => {
  const registry = new CausalWaitRegistry()
  const lease = registry.Enter(descriptor('A'))
  lease.Dispose() // no MarkExit — the lease must still leave with a diagnostic exit

  const transition = lastTransition(registry)
  assert.equal(transition.Kind.name, 'Left')
  assert.equal(transition.Exit.name, 'WaitDisposed')
  assert.equal(listItems(registry.Snapshot().Active).length, 0)
})

test('WHAT[CAUSAL-006] CAUSAL_006_mark_exit_then_dispose_preserves_exit', () => {
  const registry = new CausalWaitRegistry()
  const lease = registry.Enter(descriptor('A'))
  lease.MarkExit(causalWait.exit.cancelled())
  lease.Dispose()

  assert.equal(lastTransition(registry).Exit.name, 'WaitCancelled')
})

test('WHAT[CAUSAL-006] CAUSAL_006_repeated_mark_exit_last_one_wins', () => {
  const registry = new CausalWaitRegistry()
  const lease = registry.Enter(descriptor('A'))
  lease.MarkExit(causalWait.exit.resolved())
  lease.MarkExit(causalWait.exit.failed())
  lease.Dispose()

  assert.equal(lastTransition(registry).Exit.name, 'WaitFailed')
})

test('WHAT[CAUSAL-006] CAUSAL_006_dispose_is_idempotent_single_leave', () => {
  const registry = new CausalWaitRegistry()
  const lease = registry.Enter(descriptor('A'))
  lease.Dispose()
  lease.Dispose() // second dispose must not double-leave

  const leaves = listItems(registry.Snapshot().History).filter((t) => t.Kind.name === 'Left')
  assert.equal(leaves.length, 1)
  assert.equal(listItems(registry.Snapshot().Active).length, 0)
})

test('WHAT[CAUSAL-006] CAUSAL_006_reenter_is_fresh_observation_not_revival', () => {
  const registry = new CausalWaitRegistry()
  const first = registry.Enter(descriptor('A'))
  const seqAfterEnter = registry.Snapshot().Sequence
  first.Dispose()
  const seqAfterLeave = registry.Snapshot().Sequence
  assert.ok(seqAfterLeave > seqAfterEnter, 'leave must advance the observation sequence')

  // Re-entering the same descriptor is a NEW observation, not a resurrection
  // of the terminated one: the old wait is gone, one fresh wait is active.
  const second = registry.Enter(descriptor('A'))
  assert.ok(registry.Snapshot().Sequence > seqAfterLeave)
  assert.equal(listItems(registry.Snapshot().Active).length, 1)
  second.Dispose()
  assert.equal(listItems(registry.Snapshot().Active).length, 0)
})

test('WHAT[CAUSAL-006] CAUSAL_006_history_default_capacity_is_256', () => {
  const registry = new CausalWaitRegistry()
  assert.equal(CausalWaitRegistry__get_HistoryCapacity(registry), 256)
})

test('WHAT[CAUSAL-008] CAUSAL_008_fresh_registry_starts_empty_no_durable_state', () => {
  const registry = new CausalWaitRegistry()
  const snapshot = registry.Snapshot()
  assert.equal(listItems(snapshot.Active).length, 0)
  assert.equal(listItems(snapshot.History).length, 0)
  assert.equal(snapshot.Sequence, 0n, 'nothing persisted at construction — process-local only')
})

test('WHAT[CAUSAL-004] CAUSAL_004_observer_surface_has_no_snapshot', () => {
  // Application holds IWaitObserver (Enter only); reading requires the
  // separate IWaitSnapshotReader surface — a business workflow cannot observe.
  assert.equal(typeof causalWaitHub.observer.Enter, 'function')
  assert.equal(typeof causalWaitHub.observer.Snapshot, 'undefined')
  assert.equal(typeof causalWaitHub.reader.Snapshot, 'function')
})

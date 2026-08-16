// DSL-012 causal wait registry / await bracket (RED-1..RED-4, history, RED-8).
// Process-local diagnostic observation only — never Journal / recovery / branch input.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  causalAwait,
  causalWait,
  causalWaitHub,
  CausalWaitRegistry,
  listItems,
  taskSource,
} from '../../verification-system/tests/support/domain.mjs'

const owner = (id) => causalWait.owner('flow', [['id', id]])
const external = (id) => causalWait.externalProducer('capability', [['id', id]])

const waitFor = (ownerId, producerId, waitKind = 'capability') =>
  causalWait.create({
    waitKind,
    owner: owner(ownerId),
    subject: [['target', producerId]],
    producer: external(producerId),
    source: 'causal-wait.test',
  })

const viewSnapshot = (snapshot) => {
  const history = listItems(snapshot.History).map((t) => ({
    kind: t.Kind.name,
    exit: t.Exit == null ? undefined : t.Exit.name,
    ownerKey: causalWait.ownerKey(t.Wait.Owner),
    producerKey: causalWait.producerKey(t.Wait.Producer),
  }))
  return {
    active: listItems(snapshot.Active),
    history,
    sequence: snapshot.Sequence,
  }
}

const lastExit = (snapshot) => {
  const history = listItems(snapshot.History)
  assert.ok(history.length > 0, 'expected history')
  const exit = history.at(-1).Exit
  assert.ok(exit != null, 'expected leave exit')
  return exit.name
}

test('WHAT[CAUSAL-002] RED_1_active_wait_visible_after_enter', () => {
  const registry = new CausalWaitRegistry()
  const descriptor = waitFor('A', 'X')
  const lease = registry.Enter(descriptor)
  const snap = viewSnapshot(registry.Snapshot())

  assert.equal(snap.active.length, 1)
  assert.equal(causalWait.ownerKey(snap.active[0].Owner), 'flow:id=A')
  assert.equal(causalWait.producerKey(snap.active[0].Producer), 'external:capability:id=X')
  assert.equal(snap.history.length, 1)
  assert.equal(snap.history[0].kind, 'Entered')

  lease.Dispose()
})

test('WHAT[CAUSAL-006] RED_2_resolve_clears_active_and_records_resolved', async () => {
  const registry = new CausalWaitRegistry()
  const pending = taskSource()
  const awaited = causalAwait.awaitTask(registry, waitFor('A', 'X'), pending.task())

  assert.equal(listItems(registry.Snapshot().Active).length, 1, 'visible while pending')
  pending.resolve('ok')
  assert.equal(await awaited, 'ok')

  const snap = registry.Snapshot()
  assert.equal(listItems(snap.Active).length, 0)
  assert.equal(lastExit(snap), 'WaitResolved')
})

test('WHAT[CAUSAL-006] RED_3_fail_clears_active_and_records_failed', async () => {
  const registry = new CausalWaitRegistry()
  const pending = taskSource()
  const awaited = causalAwait.awaitTask(registry, waitFor('A', 'X'), pending.task())

  pending.reject(new Error('boom'))
  await assert.rejects(() => awaited, /boom/)

  const snap = registry.Snapshot()
  assert.equal(listItems(snap.Active).length, 0)
  assert.equal(lastExit(snap), 'WaitFailed')
})

test('WHAT[CAUSAL-006] RED_4_cancel_clears_active_and_records_cancelled', async () => {
  const registry = new CausalWaitRegistry()
  const pending = taskSource()
  const awaited = causalAwait.awaitTask(registry, waitFor('A', 'X'), pending.task())

  pending.cancel()
  await assert.rejects(() => awaited)

  const snap = registry.Snapshot()
  assert.equal(listItems(snap.Active).length, 0)
  assert.equal(lastExit(snap), 'WaitCancelled')
})

test('WHAT[CAUSAL-006] RED_4_cancel_message_also_classifies_as_cancelled', async () => {
  const registry = new CausalWaitRegistry()
  const pending = taskSource()
  const awaited = causalAwait.awaitTask(registry, waitFor('A', 'X'), pending.task())

  pending.reject(new Error('Operation Cancelled'))
  await assert.rejects(() => awaited, /Cancel/)

  assert.equal(lastExit(registry.Snapshot()), 'WaitCancelled')
  assert.equal(listItems(registry.Snapshot().Active).length, 0)
})

test('WHAT[CAUSAL-006] history_capacity_bounds_ring_buffer', () => {
  const registry = new CausalWaitRegistry(2)
  for (let i = 0; i < 3; i += 1) {
    const lease = registry.Enter(waitFor('A', `X${i}`))
    lease.MarkExit(causalWait.exit.resolved())
    lease.Dispose()
  }

  const history = listItems(registry.Snapshot().History)
  assert.equal(history.length, 2)
  assert.ok(history.length <= 2)
})

test('WHAT[CAUSAL-001] RED_8_application_observer_enter_only_snapshot_via_reader', () => {
  assert.equal(typeof causalWaitHub.observer.Enter, 'function')
  assert.equal(typeof causalWaitHub.snapshot, 'function')
  assert.equal(typeof causalWaitHub.reader.Snapshot, 'function')

  // Application-facing contract: observer is for Enter; Snapshot is read via
  // CausalWaitHub_snapshot / reader, not as a required observer API surface.
  const lease = causalWaitHub.observer.Enter(waitFor('hub', 'ext'))
  const viaSnapshotFn = causalWaitHub.snapshot()
  const viaReader = causalWaitHub.reader.Snapshot()
  assert.ok(listItems(viaSnapshotFn.Active).length >= 1)
  assert.ok(listItems(viaReader.Active).length >= 1)
  lease.MarkExit(causalWait.exit.resolved())
  lease.Dispose()
})

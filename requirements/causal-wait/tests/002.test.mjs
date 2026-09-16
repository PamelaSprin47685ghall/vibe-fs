import assert from 'node:assert/strict'
import test from 'node:test'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')

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

const descriptor = (id) =>
  causal.createWait({
    waitKind: 'lifecycle-wait',
    owner: owner(id),
    subject: { target: id },
    producer: causal.externalProducer('capability', { id }),
    escapes: [causal.escape('processLifetime')],
    source: 'wait-lifecycle.test',
  })

test('WHAT[CAUSAL-002] RED_1_active_wait_visible_after_enter', () => {
  const registry = causal.createRegistry()
  const d = waitFor('A', 'X')
  const lease = causal.enter(registry, d)
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

test('WHAT[CAUSAL-002] CAUSAL_002_descriptor_carries_typed_owner_producer_subject', () => {
  const wait = descriptor('A')
  assert.equal(causal.ownerKey(wait.owner), 'flow:id=A')
  assert.equal(causal.producerKey(wait.producer), 'external:capability:id=A')
  assert.equal(wait.waitKind, 'lifecycle-wait')
})

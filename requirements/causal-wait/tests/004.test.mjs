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

test('WHAT[CAUSAL-004] CAUSAL_004_observer_and_reader_capabilities_are_not_interchangeable', () => {
  const registry = causal.createRegistry()
  const observer = causal.observerCapability(registry)
  const reader = causal.snapshotReaderCapability(registry)
  assertOpaque(observer, 'wait observer capability')
  assertOpaque(reader, 'wait snapshot reader capability')

  assert.throws(() => causal.observerEnter(reader, descriptor('A')), /observer capability required/)
  assert.throws(() => causal.readerSnapshot(observer), /snapshot reader capability required/)

  const lease = causal.observerEnter(observer, descriptor('A'))
  assert.equal(causal.readerSnapshot(reader).active.length, 1)
  causal.dispose(lease)
})

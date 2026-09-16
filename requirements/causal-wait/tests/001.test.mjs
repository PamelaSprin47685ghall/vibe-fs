import assert from 'node:assert/strict'
import test from 'node:test'

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

test('WHAT[CAUSAL-001] RED_8_application_observer_enter_only_snapshot_via_reader', () => {
  const registry = causal.createRegistry()
  const observer = causal.observerCapability(registry)
  const reader = causal.snapshotReaderCapability(registry)
  const lease = causal.observerEnter(observer, waitFor('hub', 'ext'))
  assert.equal(causal.readerSnapshot(reader).active.length, 1)
  assert.throws(() => causal.readerSnapshot(observer), /snapshot reader capability required/)
  causal.markExit(lease, 'WaitResolved')
  causal.dispose(lease)
})

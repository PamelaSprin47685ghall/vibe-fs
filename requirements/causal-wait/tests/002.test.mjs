import assert from 'node:assert/strict'
import test from 'node:test'
import * as causal from '../../../dist/Execution/Session/Wait/Surface.js'

test('WHAT[causal-wait-002] actual registration preserves owner, condition, producer and termination', () => {
  const registry = causal.createRegistry()
  const descriptor = causal.createWait({
    waitKind: 'journal-material',
    owner: causal.owner('workflow', { id: 'consumer' }),
    subject: { revision: 'r7' },
    producer: causal.externalProducer('journal-writer', { id: 'producer' }),
    escapes: [causal.escape('processLifetime')],
    source: 'consumer-boundary',
  })
  const lease = causal.enter(registry, descriptor)
  try {
    const snapshot = causal.snapshot(registry)
    assert.equal(snapshot.active.length, 1)
    const observed = snapshot.active[0]
    assert.equal(causal.ownerKey(observed.owner), 'workflow:id=consumer')
    assert.equal(observed.waitKind, 'journal-material')
    assert.deepEqual(observed.subject, { revision: 'r7' })
    assert.equal(causal.producerKey(observed.producer), 'external:journal-writer:id=producer')
    assert.deepEqual(observed.escapes, [{ tag: 'processLifetime' }])
    assert.equal(snapshot.history[0].kind, 'Entered')
  } finally {
    causal.dispose(lease)
  }
})

test.todo('WHAT[causal-wait-002] production cross-boundary waits expose their last causal progress with a verifiable fact reference')

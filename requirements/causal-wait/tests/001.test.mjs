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

test('WHAT[causal-wait-001] RED_8_application_observer_enter_only_snapshot_via_reader', () => {
  const registry = causal.createRegistry()
  const observer = causal.observerCapability(registry)
  const reader = causal.snapshotReaderCapability(registry)
  const lease = causal.observerEnter(observer, waitFor('hub', 'ext'))
  assert.equal(causal.readerSnapshot(reader).active.length, 1)
  assert.throws(() => causal.readerSnapshot(observer), /snapshot reader capability required/)
  causal.markExit(lease, 'WaitResolved')
  causal.dispose(lease)
})

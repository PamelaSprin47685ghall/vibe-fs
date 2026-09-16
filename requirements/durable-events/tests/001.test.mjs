import assert from 'node:assert/strict'
import test from 'node:test'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'

test('WHAT[DURABLE-EVENTS-001] append_only_prior_writer_bytes_are_a_strict_prefix_after_new_fact', () => {
  const store = eventStore.createMemoryStore()
  const e1 = eventStore.appendEvent(store, { eventType: 'FactA', payload: { a: 1 } })
  const bytes1 = eventStore.rawBytes(store)
  const e2 = eventStore.appendEvent(store, { eventType: 'FactB', payload: { b: 2 } })
  const bytes2 = eventStore.rawBytes(store)
  assert.ok(bytes2.startsWith(bytes1))
  assert.notEqual(bytes1, bytes2)
})

test('WHAT[DURABLE-EVENTS-001] event_store_append_preserves_immutable_history', () => {
  const store = eventStore.createMemoryStore()
  eventStore.appendEvent(store, { eventType: 'Evt1', payload: {} })
  const snap1 = eventStore.readEvents(store)
  eventStore.appendEvent(store, { eventType: 'Evt2', payload: {} })
  const snap2 = eventStore.readEvents(store)
  assert.equal(snap1.length, 1)
  assert.equal(snap2.length, 2)
  assert.equal(snap2[0].eventId, snap1[0].eventId)
})

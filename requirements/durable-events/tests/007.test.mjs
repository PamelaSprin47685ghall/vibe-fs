import assert from 'node:assert/strict'
import test from 'node:test'
import * as store from '../../../dist/Persistence/EventStore/Surface.js'
import * as utf8Codec from '../../../dist/Persistence/EventStore/Utf8CodecSurface.js'

test('WHAT[DURABLE-EVENTS-007] append_rejects_missing_parent_without_writing_bytes', () => {
  const s = store.createMemoryStore()
  assert.throws(
    () => store.appendEvent(s, { eventType: 'Child', parents: ['nonexistent-parent'], payload: {} }),
    /parent event missing/,
  )
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_cycle_in_one_batch_before_durability', () => {
  const s = store.createMemoryStore()
  assert.throws(
    () => store.appendBatch(s, [
      { eventId: 'e1', parents: ['e2'], payload: {} },
      { eventId: 'e2', parents: ['e1'], payload: {} },
    ]),
    /cycle detected/,
  )
})

test('WHAT[DURABLE-EVENTS-007] append_rejects_unknown_event_type_fail_closed', () => {
  const s = store.createMemoryStore()
  assert.throws(
    () => store.appendEvent(s, { eventType: 'UnknownRandomType', payload: {} }),
    /unknown event type/,
  )
})

test('WHAT[DURABLE-EVENTS-007] local writer boot rejects invalid UTF-8 without replacement decoding', () => {
  assert.throws(() => utf8Codec.validateStreamUtf8(Buffer.from([0x80, 0x81])), /invalid UTF-8 bytes/)
})

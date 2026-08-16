// Canonical EventStore identity is the durable boundary; Git object identity is
// deliberately outside the runtime store after the shock cut.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as codec from '../../../../../dist/Persistence/EventStore/CodecSurface.js'

const event = (overrides = {}) => ({
  id: 'a'.repeat(40),
  stream: 'identity/proof',
  type: 'JobRequested',
  parents: [],
  payload: { answer: 42, nested: { b: 2, a: 1 } },
  payloadRefs: [],
  ...overrides,
})

test('WHAT[DURABLE-EVENTS-003] canonical_event_bytes_are_stable_under_object_and_set_order', () => {
  const left = event({
    parents: ['c'.repeat(40), 'b'.repeat(40), 'c'.repeat(40)],
    payloadRefs: ['ref-z', 'ref-a', 'ref-z'],
  })
  const right = event({
    parents: ['b'.repeat(40), 'c'.repeat(40)],
    payloadRefs: ['ref-a', 'ref-z'],
    payload: { nested: { a: 1, b: 2 }, answer: 42 },
  })

  const bytes = codec.encode(left)
  assert.equal(bytes.endsWith('\n'), true)
  assert.equal(bytes.endsWith('\n\n'), false)
  assert.equal(codec.encode(right), bytes)
  assert.equal(codec.checkIdentity(left, right).ok, true)
})

test('WHAT[DURABLE-EVENTS-003] same_event_id_different_canonical_bytes_is_identity_collision', () => {
  const result = codec.checkIdentity(event(), event({ payload: { answer: 43 } }))
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'IdentityCollision')
  assert.equal(result.error.eventId, 'a'.repeat(40))
})

test('WHAT[DURABLE-EVENTS-003] canonical_event_bytes_decode_to_the_same_plain_event', () => {
  const original = event({
    id: 'b'.repeat(40),
    parents: ['d'.repeat(40), 'c'.repeat(40)],
    payloadRefs: ['payload-z', 'payload-a'],
  })
  const decoded = codec.decode(codec.encode(original))
  assert.equal(decoded.ok, true, JSON.stringify(decoded.error))
  assert.deepEqual(decoded.event, {
    ...original,
    parents: ['c'.repeat(40), 'd'.repeat(40)],
    payloadRefs: ['payload-a', 'payload-z'],
  })
})

test('WHAT[DURABLE-EVENTS-003] merge_by_identity_dedupes_equal_bytes_and_rejects_collisions', () => {
  const same = codec.mergeByIdentity([event(), event()])
  assert.equal(same.ok, true)
  assert.equal(same.events.length, 1)

  const collision = codec.mergeByIdentity([event(), event({ payload: { answer: 99 } })])
  assert.equal(collision.ok, false)
  assert.equal(collision.error.code, 'IdentityCollision')
})

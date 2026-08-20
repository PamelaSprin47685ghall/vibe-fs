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

test('WHAT[DURABLE-EVENTS-003] decode_rejects_noncanonical_key_and_set_order_without_reencoding_the_event', async () => {
  const base = {
    event_id: 'e'.repeat(40),
    event_type: 'JobRequested',
    parents: [],
    payload: { a: 1, b: 2 },
    payload_refs: [],
    stream_id: 'identity/proof',
  }

  const wrongTopLevelOrder = `${JSON.stringify({ stream_id: base.stream_id, ...base })}\n`
  const wrongPayloadOrder = `${JSON.stringify({ ...base, payload: { b: 2, a: 1 } })}\n`
  const wrongParentOrder = `${JSON.stringify({ ...base, parents: ['b'.repeat(40), 'a'.repeat(40)] })}\n`
  const duplicateRefs = `${JSON.stringify({ ...base, payload_refs: ['ref-a', 'ref-a'] })}\n`

  for (const text of [wrongTopLevelOrder, wrongPayloadOrder, wrongParentOrder, duplicateRefs]) {
    const decoded = codec.decode(text)
    assert.equal(decoded.ok, false, text)
    assert.equal(decoded.error.code, 'NonCanonical')
  }

  const { readFile } = await import('node:fs/promises')
  const source = await readFile(
    new URL('../../../../../src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fs', import.meta.url),
    'utf8',
  )
  assert.doesNotMatch(
    source,
    /let private ensureCanonical[\s\S]{0,350}encode\s+normalized/,
    'decode must validate parsed canonical shape directly instead of allocating a second normalized event encoding',
  )
})

test('WHAT[DURABLE-EVENTS-003] merge_by_identity_dedupes_equal_bytes_and_rejects_collisions', () => {
  const same = codec.mergeByIdentity([event(), event()])
  assert.equal(same.ok, true)
  assert.equal(same.events.length, 1)

  const collision = codec.mergeByIdentity([event(), event({ payload: { answer: 99 } })])
  assert.equal(collision.ok, false)
  assert.equal(collision.error.code, 'IdentityCollision')
})

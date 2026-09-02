// DURABLE-EVENTS-003: canonical bytes are the identity protocol.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as eventCodec from '../../../dist/Persistence/EventStore/CodecSurface.js'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'

const A = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'

const envelope = ({
  id = A,
  stream = 'job/main',
  eventType = 'JobRequested',
  parents = [],
  payload = { status: 'open' },
  payloadRefs = [],
} = {}) => ({
  id,
  stream,
  type: eventType,
  parents,
  payload,
  payloadRefs,
})

test('WHAT[DURABLE-EVENTS-003] same_EventId_different_canonical_bytes_fail_closed', () => {
  const left = envelope({ payload: { status: 'open' } })
  const right = envelope({ payload: { status: 'closed' } })

  const checked = eventCodec.checkIdentity(left, right)
  assert.equal(checked.ok, false)
  assert.equal(checked.error.code, 'IdentityCollision')
  assert.equal(checked.error.eventId, A)

  const merged = eventCodec.mergeByIdentity([left, right])
  assert.equal(merged.ok, false)
  assert.equal(merged.error.code, 'IdentityCollision')
})

test('WHAT[DURABLE-EVENTS-003] same_EventId_same_canonical_bytes_dedupe_ok', () => {
  const a = envelope({
    parents: [
      'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
      'cccccccccccccccccccccccccccccccccccccccc',
    ],
    payloadRefs: ['oid-2', 'oid-1'],
  })
  const b = envelope({
    parents: [
      'cccccccccccccccccccccccccccccccccccccccc',
      'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
      'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    ],
    payloadRefs: ['oid-1', 'oid-2', 'oid-1'],
  })

  assert.equal(eventCodec.encode(a), eventCodec.encode(b))
  const checked = eventCodec.checkIdentity(a, b)
  assert.equal(checked.ok, true)

  const merged = eventCodec.mergeByIdentity([a, b])
  assert.equal(merged.ok, true)
  assert.equal(merged.events.length, 1)
})

test('WHAT[DURABLE-EVENTS-003] canonical_bytes_are_utf8_json_plus_single_LF_with_sorted_keys', () => {
  const value = envelope({
    parents: [
      'ffffffffffffffffffffffffffffffffffffffff',
      '0000000000000000000000000000000000000000',
    ],
    payload: { z: 1, a: { m: true, b: 2 } },
    payloadRefs: ['p2', 'p1'],
  })

  const text = eventCodec.encode(value)
  assert.equal(text.endsWith('\n'), true)
  assert.equal(text.endsWith('\n\n'), false)
  assert.equal(text.includes('\r'), false)
  assert.equal(text.charCodeAt(0) !== 0xfeff, true)

  const body = text.slice(0, -1)
  assert.deepEqual(Object.keys(JSON.parse(body)), [
    'event_id',
    'event_type',
    'parents',
    'payload',
    'payload_refs',
    'stream_id',
  ])

  const parsed = JSON.parse(body)
  assert.deepEqual(parsed.parents, [
    '0000000000000000000000000000000000000000',
    'ffffffffffffffffffffffffffffffffffffffff',
  ])
  assert.deepEqual(parsed.payload_refs, ['p1', 'p2'])
  assert.deepEqual(Object.keys(parsed.payload), ['a', 'z'])
  assert.deepEqual(Object.keys(parsed.payload.a), ['b', 'm'])

  assert.equal(eventCodec.encode(value), eventCodec.encode(value))
})

test('WHAT[DURABLE-EVENTS-003] distinct_EventIds_are_both_retained', () => {
  const a = envelope({ id: '1111111111111111111111111111111111111111' })
  const b = envelope({
    id: '2222222222222222222222222222222222222222',
    payload: { other: true },
  })

  assert.equal(eventCodec.checkIdentity(a, b).ok, true)
  const merged = eventCodec.mergeByIdentity([b, a])
  assert.equal(merged.ok, true)
  assert.equal(merged.events.length, 2)
})

test('WHAT[DURABLE-EVENTS-016] Git_contract_exposes_canonical_store_ref', () => {
  assert.equal(eventStore.canonicalStoreRef, 'refs/wanxiang/store')
})

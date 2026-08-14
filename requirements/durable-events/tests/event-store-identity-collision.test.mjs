// tests/unit/persist/event-store-identity-collision.test.mjs
// Phase 2 Wave A — §5.0 / §5.3 identity collision fail-closed.
//
// same EventId + different canonical bytes → StorageInvalid.IdentityCollision
// parents / payload_refs order must not invent collisions after canonicalize

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, eventId, idValue, listItems, payloadOf, toList } from '../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const Codec = await import('../../../dist/Infrastructure/Persist/CanonicalEventCodec.js')

const streamId = (v) => Domain.EventStreamIdModule_create(v)
const payloadRef = (v) => Domain.PayloadRefModule_create(v)

const envelope = ({
  id = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
  stream = 'job/main',
  eventType = 'JobRequested',
  parents = [],
  payload = { status: 'open' },
  payloadRefs = [],
} = {}) =>
  new Domain.EventEnvelope(
    eventId(id),
    streamId(stream),
    eventType,
    toList(parents.map(eventId)),
    payload,
    toList(payloadRefs.map(payloadRef)),
  )

test('same_EventId_different_canonical_bytes_fail_closed', () => {
  const left = envelope({ payload: { status: 'open' } })
  const right = envelope({ payload: { status: 'closed' } })

  const checked = Codec.checkIdentity(left, right)
  assert.equal(caseOf(checked), 'Error')
  const err = payloadOf(checked)
  assert.equal(caseOf(err), 'IdentityCollision')
  assert.equal(idValue.event(payloadOf(err)), 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')

  const merged = Codec.mergeByIdentity(toList([left, right]))
  assert.equal(caseOf(merged), 'Error')
  assert.equal(caseOf(payloadOf(merged)), 'IdentityCollision')
})

test('same_EventId_same_canonical_bytes_dedupe_ok', () => {
  const a = envelope({
    parents: [
      'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
      'cccccccccccccccccccccccccccccccccccccccc',
    ],
    payloadRefs: ['oid-2', 'oid-1'],
  })
  // Same identity, different list order — canonicalize must collapse to one event.
  const b = envelope({
    parents: [
      'cccccccccccccccccccccccccccccccccccccccc',
      'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
      'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    ],
    payloadRefs: ['oid-1', 'oid-2', 'oid-1'],
  })

  assert.equal(Codec.encode(a), Codec.encode(b))
  assert.equal(caseOf(Codec.checkIdentity(a, b)), 'Ok')

  const merged = Codec.mergeByIdentity(toList([a, b]))
  assert.equal(caseOf(merged), 'Ok')
  assert.equal(listItems(payloadOf(merged)).length, 1)
})

test('canonical_bytes_are_utf8_json_plus_single_LF_with_sorted_keys', () => {
  const value = envelope({
    parents: [
      'ffffffffffffffffffffffffffffffffffffffff',
      '0000000000000000000000000000000000000000',
    ],
    payload: { z: 1, a: { m: true, b: 2 } },
    payloadRefs: ['p2', 'p1'],
  })

  const text = Codec.encode(value)
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

  assert.equal(Codec.encode(value), Codec.encode(value))
})

test('distinct_EventIds_are_both_retained', () => {
  const a = envelope({ id: '1111111111111111111111111111111111111111' })
  const b = envelope({
    id: '2222222222222222222222222222222222222222',
    payload: { other: true },
  })

  assert.equal(caseOf(Codec.checkIdentity(a, b)), 'Ok')
  const merged = Codec.mergeByIdentity(toList([b, a]))
  assert.equal(caseOf(merged), 'Ok')
  assert.equal(listItems(payloadOf(merged)).length, 2)
})

test('StoreTypes_exposes_canonical_store_ref_and_error_DUs', () => {
  assert.equal(Codec.canonicalStoreRef, 'refs/wanxiang/store')
  assert.equal(Persist.StoreRef_canonical, 'refs/wanxiang/store')
  assert.ok(Persist.StorageInvalid)
  assert.ok(Persist.MergeError)
  assert.ok(Persist.AppendError)
  assert.ok(Persist.PublishError)
  assert.ok(Persist.FoldError)
  assert.ok(Persist.ConvergeError)
  assert.ok(Persist.DomainConflict)
})

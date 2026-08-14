// Split from tests/unit/persist/event-store-merge.test.mjs (cutover Wave 2a);
// owner: durable-events.
//
// The EventStore read/materialization surface of the original merge file:
// shard layout (EventId → events/<hex-prefix>/<id>.jsonl), blob write =
// git hash-object, payload closure fail-closed, and envelope loading across
// shards. The merge algebra assertions (spec oracle / production laws /
// identity collision / materialize-of-union) moved with durable-convergence.

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, eventId, idValue, isSome, listItems, payloadOf, toList } from '../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')

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

const mustOk = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Ok', `${label} should be Ok, got ${caseOf(result)}`)
  return payloadOf(result)
}

const mustErr = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Error', `${label} should be Error`)
  return payloadOf(result)
}

const createStore = () => GitRaw.GitRawStore_createInMemory()

const materialize = async (store, events) => {
  const result = await GitRaw.GitRawStore_materializeSnapshot(store, toList(events))
  return mustOk(result, 'materializeSnapshot')
}

const eventIdsOf = (events) =>
  listItems(events)
    .map((e) => idValue.event(e.EventId))
    .sort()

test('EventId_shard_path_is_events_hex_prefix_EventId_jsonl', () => {
  const id = eventId('ab12aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')
  assert.equal(GitRaw.EventIdShard_prefix(id), 'ab')
  assert.equal(GitRaw.EventIdShard_fileName(id), 'ab12aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jsonl')
  assert.equal(
    GitRaw.EventIdShard_relativePath(id),
    'events/ab/ab12aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jsonl',
  )
})

test('WriteBlob_matches_git_hash_object_sha1', async () => {
  const store = createStore()
  const content = new TextEncoder().encode('{"a":1}\n')
  const oid = await store.WriteBlob(content)
  assert.equal(Persist.GitObjectIdModule_value(oid), '0187f3b09d66cef7911083ad49f890f5a6589589')
  const roundTrip = await store.ReadObject(oid)
  assert.equal(isSome(roundTrip), true)
  assert.deepEqual(Buffer.from(roundTrip), Buffer.from(content))
})

test('materializeSnapshot_payload_closure_and_missing_payload_fail_closed', async () => {
  const store = createStore()
  const payloadOid = await store.WriteBlob(new TextEncoder().encode('large-body\n'))
  const payloadHex = Persist.GitObjectIdModule_value(payloadOid)

  const withPayload = envelope({
    id: '1111111111111111111111111111111111111111',
    payloadRefs: [payloadHex],
  })
  const snapshot = await materialize(store, [withPayload])
  const names = mustOk(await GitRaw.GitRawStore_listPayloadNames(store, snapshot.RootOid))
  assert.deepEqual(listItems(names), [payloadHex])

  const dangling = envelope({
    id: '2222222222222222222222222222222222222222',
    payloadRefs: ['ffffffffffffffffffffffffffffffffffffffff'],
  })
  const missing = await GitRaw.GitRawStore_materializeSnapshot(store, toList([dangling]))
  assert.equal(caseOf(missing), 'Error')
  assert.equal(caseOf(payloadOf(missing)), 'MissingPayload')
})

test('loadEventEnvelopes_reads_every_blob_across_shards', async () => {
  const store = createStore()
  const events = [
    envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { n: 1 } }),
    envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', payload: { n: 2 } }),
    envelope({ id: 'caaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { n: 3 } }),
  ]
  const snapshot = await materialize(store, events)
  const loaded = mustOk(await GitRaw.GitRawStore_loadEventEnvelopes(store, snapshot.RootOid), 'loadEventEnvelopes')
  assert.deepEqual(eventIdsOf(loaded), [
    'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    'caaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
  ])
})

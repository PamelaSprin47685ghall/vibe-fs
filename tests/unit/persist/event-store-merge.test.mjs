// tests/unit/persist/event-store-merge.test.mjs
// Phase 2 Wave B — §10.6 K-way merge: associative / commutative / idempotent / deterministic.
//
// Spec oracle = EventStoreMergeSpec.mergeEvents (set union + identity dedupe)
// Production  = EventStoreMerge.merge (structural tree union → StoreSnapshot)

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, eventId, idValue, isSome, listItems, payloadOf, toList } from '../support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const Merge = await import('../../../dist/Infrastructure/Persist/EventStoreMerge.js')

const streamId = (v) => Domain.EventStreamIdModule_create(v)
const payloadRef = (v) => Domain.PayloadRefModule_create(v)
const oidValue = (rootOid) => Persist.GitObjectIdModule_value(Persist.RootOidModule_value(rootOid))
const snapshotOid = (snapshot) => oidValue(snapshot.RootOid)

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

const productionMerge = async (store, snapshots) => {
  const input = Persist.MergeInputModule_ofList(toList(snapshots))
  return await Merge.EventStoreMerge_merge(store, input)
}

const specMergeEvents = (events) => Merge.EventStoreMergeSpec_mergeEvents(toList(events))

const specMergeSnapshots = async (store, snapshots) => {
  const input = Persist.MergeInputModule_ofList(toList(snapshots))
  return await Merge.EventStoreMergeSpec_merge(store, input)
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

test('CompareAndSwapRef_Absent_then_expected_oid', async () => {
  const store = createStore()
  const oid = await store.WriteBlob(new TextEncoder().encode('x\n'))
  assert.equal(await store.CompareAndSwapRef(Persist.StoreRef_canonical, undefined, oid), true)
  assert.equal(await store.CompareAndSwapRef(Persist.StoreRef_canonical, undefined, oid), false)
  const other = await store.WriteBlob(new TextEncoder().encode('y\n'))
  assert.equal(await store.CompareAndSwapRef(Persist.StoreRef_canonical, oid, other), true)
  const current = await store.ReadRef(Persist.StoreRef_canonical)
  assert.equal(isSome(current), true)
  assert.equal(Persist.GitObjectIdModule_value(current), Persist.GitObjectIdModule_value(other))
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

test('merge_spec_oracle_associative_commutative_idempotent_deterministic', () => {
  const a = envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { n: 1 } })
  const b = envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', payload: { n: 2 } })
  const c = envelope({ id: 'cccccccccccccccccccccccccccccccccccccccc', payload: { n: 3 } })

  const ab = mustOk(specMergeEvents([a, b]))
  const bc = mustOk(specMergeEvents([b, c]))
  const left = mustOk(specMergeEvents([...listItems(ab), c]))
  const right = mustOk(specMergeEvents([a, ...listItems(bc)]))
  assert.deepEqual(eventIdsOf(left), eventIdsOf(right))

  const abOrder = mustOk(specMergeEvents([a, b]))
  const baOrder = mustOk(specMergeEvents([b, a]))
  assert.deepEqual(eventIdsOf(abOrder), eventIdsOf(baOrder))

  const once = mustOk(specMergeEvents([a, b, c]))
  const twice = mustOk(specMergeEvents([...listItems(once), ...listItems(once)]))
  assert.deepEqual(eventIdsOf(once), eventIdsOf(twice))
  assert.equal(listItems(twice).length, 3)

  const orders = [
    [a, b, c],
    [c, a, b],
    [b, c, a],
    [a, c, b],
  ].map((order) => eventIdsOf(mustOk(specMergeEvents(order))))
  for (const ids of orders) assert.deepEqual(ids, orders[0])
})

test('merge_production_associative_commutative_idempotent_deterministic', async () => {
  const store = createStore()
  const a = envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { n: 1 } })
  const b = envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', payload: { n: 2 } })
  const c = envelope({ id: 'cccccccccccccccccccccccccccccccccccccccc', payload: { n: 3 } })

  const SA = await materialize(store, [a])
  const SB = await materialize(store, [b])
  const SC = await materialize(store, [c])

  const merge2 = async (x, y) => mustOk(await productionMerge(store, [x, y]))
  const merge3 = async (xs) => mustOk(await productionMerge(store, xs))

  // commutative
  assert.equal(snapshotOid(await merge2(SA, SB)), snapshotOid(await merge2(SB, SA)))

  // associative
  const left = await merge2(await merge2(SA, SB), SC)
  const right = await merge2(SA, await merge2(SB, SC))
  assert.equal(snapshotOid(left), snapshotOid(right))

  // idempotent
  assert.equal(snapshotOid(await merge2(SA, SA)), snapshotOid(SA))
  const ABC = await merge3([SA, SB, SC])
  assert.equal(snapshotOid(await merge2(ABC, ABC)), snapshotOid(ABC))

  // deterministic across input enumeration order
  const orders = await Promise.all([
    [SA, SB, SC],
    [SC, SA, SB],
    [SB, SC, SA],
    [SA, SC, SB],
  ].map(async (order) => snapshotOid(await merge3(order))))
  for (const oid of orders) assert.equal(oid, orders[0])

  const spec = mustOk(await specMergeSnapshots(store, [SA, SB, SC]))
  const blobs = mustOk(await GitRaw.GitRawStore_listEventBlobs(store, ABC.RootOid))
  assert.equal(listItems(blobs).length, 3)
  assert.equal(listItems(spec).length, 3)
})

test('merge_identity_collision_fail_closed', async () => {
  const store = createStore()
  const left = envelope({
    id: 'dddddddddddddddddddddddddddddddddddddddd',
    payload: { status: 'open' },
  })
  const right = envelope({
    id: 'dddddddddddddddddddddddddddddddddddddddd',
    payload: { status: 'closed' },
  })

  const SL = await materialize(store, [left])
  const SR = await materialize(store, [right])

  const merged = await productionMerge(store, [SL, SR])
  const err = mustErr(merged)
  assert.equal(caseOf(err), 'StorageInvalid')
  assert.equal(caseOf(payloadOf(err)), 'IdentityCollision')

  const specErr = mustErr(specMergeEvents([left, right]))
  assert.equal(caseOf(specErr), 'StorageInvalid')
  assert.equal(caseOf(payloadOf(specErr)), 'IdentityCollision')
})

test('merge_production_matches_materialize_of_union', async () => {
  const store = createStore()
  const payloadOid = await store.WriteBlob(new TextEncoder().encode('shared\n'))
  const payloadHex = Persist.GitObjectIdModule_value(payloadOid)

  const a = envelope({
    id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    payloadRefs: [payloadHex],
  })
  const b = envelope({
    id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    payload: { only: 'b' },
  })

  const SA = await materialize(store, [a])
  const SB = await materialize(store, [b])
  const merged = mustOk(await productionMerge(store, [SA, SB]))
  const direct = await materialize(store, [a, b])
  assert.equal(snapshotOid(merged), snapshotOid(direct))
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

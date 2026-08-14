// Durable-convergence package-owned law tests (storage.md §10/§19, PERSIST-003):
// replica merge = set union that never drops facts; k-way merge algebra;
// convergence is a function of the event set, never of wall-clock/arrival order.

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, eventId, idValue, listItems, mapEntries, payloadOf, toList } from '../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')
const Merge = await import('../../../dist/Infrastructure/Persist/EventStoreMerge.js')
const Fold = await import('../../../dist/Infrastructure/Persist/EventStoreFold.js')

const streamId = (v) => Domain.EventStreamIdModule_create(v)
const oidValue = (rootOid) => Persist.GitObjectIdModule_value(Persist.RootOidModule_value(rootOid))
const snapshotOid = (snapshot) => oidValue(snapshot.RootOid)
const hexId = (n) => n.toString(16).padStart(40, '0')

const envelope = ({
  id = hexId(0x1),
  stream = 'job/main',
  eventType = 'JobRequested',
  parents = [],
  payload = { status: 'open' },
} = {}) =>
  new Domain.EventEnvelope(eventId(id), streamId(stream), eventType, toList(parents.map(eventId)), payload, toList([]))

const mustOk = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Ok', `${label} should be Ok, got ${caseOf(result)}`)
  return payloadOf(result)
}

const createRaw = () => GitRaw.GitRawStore_createInMemory()
const createStore = (raw = createRaw()) => Store.EventStore_create(raw)
const materialize = async (raw, events) => mustOk(await GitRaw.GitRawStore_materializeSnapshot(raw, toList(events)))

const eventIds = async (rootOid, raw) =>
  listItems(mustOk(await GitRaw.GitRawStore_listEventBlobs(raw, rootOid)))
    .map(([path]) => path.split('/').pop().replace(/\.jsonl$/, ''))
    .sort()

test('set_union_never_drops_concurrent_events', async () => {
  // storage.md §10.6/§19: 两个不同 EventId 永远都进入 merged history，即使 DomainConflict。
  const raw = createRaw()
  const es = createStore(raw)
  const a = envelope({ id: hexId(0xa), payload: { side: 'left' } })
  const b = envelope({ id: hexId(0xb), payload: { side: 'right' } })
  const SA = await materialize(raw, [a])
  const SB = await materialize(raw, [b])

  const merged = mustOk(await es.Merge(toList([SA, SB])), 'merge of two concurrent replicas')
  const ids = await eventIds(merged.RootOid, raw)
  assert.deepEqual(ids, [hexId(0xa), hexId(0xb)], 'both facts survive the merge')
})

test('merge_is_commutative_associative_idempotent', async () => {
  // storage.md §10.5: associative / commutative / idempotent / deterministic。
  const raw = createRaw()
  const es = createStore(raw)
  const a = envelope({ id: hexId(0xc), payload: { n: 1 } })
  const b = envelope({ id: hexId(0xd), payload: { n: 2 } })
  const c = envelope({ id: hexId(0xe), payload: { n: 3 } })
  const SA = await materialize(raw, [a])
  const SB = await materialize(raw, [b])
  const SC = await materialize(raw, [c])

  const ab = mustOk(await es.Merge(toList([SA, SB])))
  const ba = mustOk(await es.Merge(toList([SB, SA])))
  assert.equal(snapshotOid(ab), snapshotOid(ba), 'commutative')

  const abC = mustOk(await es.Merge(toList([ab, SC])))
  const aBc = mustOk(await es.Merge(toList([SA, mustOk(await es.Merge(toList([SB, SC])))])))
  assert.equal(snapshotOid(abC), snapshotOid(aBc), 'associative')

  const aa = mustOk(await es.Merge(toList([SA, SA])))
  assert.equal(snapshotOid(aa), snapshotOid(SA), 'idempotent')

  // Determinism: same inputs, repeated run, same canonical result.
  const ab2 = mustOk(await es.Merge(toList([SA, SB])))
  assert.equal(snapshotOid(ab), snapshotOid(ab2), 'deterministic')
})

test('convergence_is_a_function_of_the_event_set_not_arrival_order', async () => {
  // storage.md §19: 禁止 wall_clock LWW；merge 不读时钟。两个 replica 各自 append 的
  // 到达顺序不同，但只要 event 集合相同，merged root 相同。
  const x = envelope({ id: hexId(0xf), payload: { n: 1 } })
  const y = envelope({ id: hexId(0x10), payload: { n: 2 } })

  const raw1 = createRaw()
  const es1 = createStore(raw1)
  const s1a = await materialize(raw1, [x])
  const s1b = await materialize(raw1, [y])
  const merged1 = mustOk(await es1.Merge(toList([s1a, s1b])))

  const raw2 = createRaw()
  const es2 = createStore(raw2)
  const s2a = await materialize(raw2, [y])
  const s2b = await materialize(raw2, [x])
  const merged2 = mustOk(await es2.Merge(toList([s2a, s2b])))

  assert.equal(snapshotOid(merged1), snapshotOid(merged2))
  assert.deepEqual(
    await eventIds(merged1.RootOid, raw1),
    await eventIds(merged2.RootOid, raw2),
    'same event set folds to the same durable world regardless of arrival order',
  )
})

test('production_merge_matches_the_set_union_spec_oracle', async () => {
  // storage.md §10.6: production structural merge ≡ materialize(union(events))。
  const raw = createRaw()
  const es = createStore(raw)
  const a = envelope({ id: hexId(0x11), payload: { n: 1 } })
  const b = envelope({ id: hexId(0x12), payload: { n: 2 } })
  const SA = await materialize(raw, [a])
  const SB = await materialize(raw, [b])

  const merged = mustOk(await es.Merge(toList([SA, SB])))
  const direct = await materialize(raw, [a, b])
  assert.equal(snapshotOid(merged), snapshotOid(direct))

  const input = Persist.MergeInputModule_ofList(toList([SA, SB]))
  const spec = mustOk(await Merge.EventStoreMergeSpec_merge(raw, input))
  // Spec oracle = set-union of envelopes; the merged snapshot must contain the
  // same event identities (storage.md §10.6).
  assert.deepEqual(
    listItems(spec)
      .map((e) => idValue.event(e.EventId))
      .sort(),
    [hexId(0x11), hexId(0x12)],
  )
})

test('concurrent_heads_fold_to_DomainConflict_and_resolution_collapses', async () => {
  // PERSIST-003: 合法并发 fork 是 DomainConflict，不是 StorageInvalid；
  // 以全部 heads 为 parents 的 resolution event 收敛。
  const raw = createRaw()
  const es = createStore(raw)
  const a = envelope({ id: hexId(0x21), payload: { side: 'left' } })
  const b = envelope({ id: hexId(0x22), payload: { side: 'right' } })
  const merged = mustOk(await es.Merge(toList([await materialize(raw, [a]), await materialize(raw, [b])])))

  const events = mustOk(await GitRaw.GitRawStore_loadEventEnvelopes(raw, merged.RootOid))
  const folded = Fold.EventStoreFold_fold(toList(events))
  assert.equal(caseOf(folded), 'Ok', 'concurrent fork is foldable — never StorageInvalid')
  const projection = payloadOf(folded)
  assert.equal(listItems(projection.Conflicts).length, 1, 'conflict is a deterministic projection state')
  const [, streamState] = mapEntries(projection.Streams).find(([key]) => key === 'job/main')
  assert.equal(caseOf(streamState), 'Conflict')

  // Resolution event names every competing head as parent.
  const resolution = envelope({
    id: hexId(0x23),
    eventType: 'JobConflictResolved',
    parents: [hexId(0x21), hexId(0x22)],
    payload: {},
  })
  const resolved = mustOk(await es.Append(merged, toList([resolution])))
  const after = mustOk(await GitRaw.GitRawStore_loadEventEnvelopes(raw, resolved.RootOid))
  const refolded = payloadOf(Fold.EventStoreFold_fold(toList(after)))
  const [, refoldedState] = mapEntries(refolded.Streams).find(([key]) => key === 'job/main')
  assert.equal(caseOf(refoldedState), 'Unique', 'resolution leaves the conflict state')
})

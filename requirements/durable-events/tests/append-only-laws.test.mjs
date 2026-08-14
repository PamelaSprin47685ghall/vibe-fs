// Durable-events package-owned law tests (PERSIST-001..003, storage.md §2/§4/§9):
// append-only facts; canonical root is a pure function of the event set;
// one event = one immutable blob; no version tokens; CAS-witnessed commits.

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, eventId, idValue, isSome, listItems, payloadOf, toList } from '../../../tests/unit/support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')
const Codec = await import('../../../dist/Infrastructure/Persist/CanonicalEventCodec.js')

const streamId = (v) => Domain.EventStreamIdModule_create(v)
const payloadRef = (v) => Domain.PayloadRefModule_create(v)
const oidValue = (rootOid) => Persist.GitObjectIdModule_value(Persist.RootOidModule_value(rootOid))
const snapshotOid = (snapshot) => oidValue(snapshot.RootOid)
const hexId = (n) => n.toString(16).padStart(40, '0')

const envelope = ({
  id = hexId(0x1),
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

const createRaw = () => GitRaw.GitRawStore_createInMemory()
const createStore = (raw = createRaw()) => Store.EventStore_create(raw)

test('committed_event_bytes_are_never_rewritten', async () => {
  // storage.md §3: committed event 永远不可修改/覆盖/原地升级；错误用新事实纠正。
  const raw = createRaw()
  const es = createStore(raw)
  const base = await es.OpenSnapshot()
  const id = hexId(0xa)
  const original = envelope({ id, payload: { status: 'open' } })
  mustOk(await es.Append(base, toList([original])))

  // Same EventId, different bytes — the rewrite attempt must fail closed,
  // and the committed history must still hold the original fact.
  const rewritten = envelope({ id, payload: { status: 'closed' } })
  const err = mustErr(await es.Append(await es.Refresh(), toList([rewritten])))
  assert.equal(caseOf(err), 'StorageInvalid')
  assert.equal(caseOf(payloadOf(err)), 'IdentityCollision')

  const blobs = mustOk(await GitRaw.GitRawStore_listEventBlobs(raw, (await es.OpenSnapshot()).RootOid))
  assert.equal(listItems(blobs).length, 1, 'history must still hold exactly the original event')

  const [, oid] = listItems(blobs)[0]
  const bytes = await raw.ReadObject(oid)
  const decoded = mustOk(Codec.tryDecodeUtf8(bytes), 'decoded committed event')
  assert.equal(idValue.event(decoded.EventId), id)
  assert.deepEqual(decoded.Payload, { status: 'open' }, 'committed bytes unchanged by the failed rewrite')
})

test('same_event_set_same_root_regardless_of_batch_grouping', async () => {
  // storage.md §4: 不同 replica 批处理分组不同，只要 event 集合相同，canonical root 就相同。
  const a = envelope({ id: hexId(0xb), payload: { n: 1 } })
  const b = envelope({ id: hexId(0xc), payload: { n: 2 } })

  const raw1 = createRaw()
  const es1 = createStore(raw1)
  const grouped = mustOk(await es1.Append(await es1.OpenSnapshot(), toList([a, b])), 'one-batch append')

  const raw2 = createRaw()
  const es2 = createStore(raw2)
  const first = mustOk(await es2.Append(await es2.OpenSnapshot(), toList([a])), 'first split append')
  const second = mustOk(await es2.Append(first, toList([b])), 'second split append')

  assert.equal(snapshotOid(grouped), snapshotOid(second))
  assert.equal(snapshotOid(grouped), snapshotOid(await es2.Refresh()))
})

test('one_event_one_blob_no_partial_write', async () => {
  // storage.md §4/§9: one event = one canonical JSON+LF blob；不存在半条记录进入 canonical history。
  const raw = createRaw()
  const es = createStore(raw)
  const a = envelope({ id: hexId(0xd), payload: { n: 1 } })
  const b = envelope({ id: hexId(0xe), payload: { n: 2 } })
  const published = mustOk(await es.Append(await es.OpenSnapshot(), toList([a, b])))

  const blobs = mustOk(await GitRaw.GitRawStore_listEventBlobs(raw, published.RootOid))
  const entries = listItems(blobs)
  assert.equal(entries.length, 2, 'one blob per committed event, never a partial NDJSON line')

  for (const [, oid] of entries) {
    const bytes = await raw.ReadObject(oid)
    assert.ok(bytes.length > 0)
    const text = new TextDecoder().decode(bytes)
    assert.equal(text.endsWith('\n'), true, 'canonical event bytes end with exactly one LF')
    const decoded = mustOk(Codec.tryDecodeUtf8(bytes), 'every committed blob decodes as a full event')
    assert.ok(idValue.event(decoded.EventId), 'decoded event carries identity')
  }
})

test('canonical_envelope_bytes_carry_no_version_tokens', () => {
  // PERSIST-001 / storage.md §5: 禁止 envelope/store 携带 schemaVersion 等版本字段；
  // canonical 字节是 identity 协议，不是格式版本。
  const text = Codec.encode(envelope({ payload: { z: 1, a: 2 } }))
  const parsed = JSON.parse(text.slice(0, -1))
  assert.deepEqual(Object.keys(parsed), [
    'event_id',
    'event_type',
    'parents',
    'payload',
    'payload_refs',
    'stream_id',
  ])
  for (const token of [
    'schemaVersion',
    'storageVersion',
    'journalVersion',
    'formatVersion',
    'generationVersion',
    'schema_version',
  ]) {
    assert.equal(text.includes(token), false, `canonical bytes must not carry ${token}`)
  }
})

test('cas_not_witnessed_is_not_committed', async () => {
  // PERSIST-002/003: CAS 未见证 EventId → 不得假装已提交；失败以显式错误返回。
  const raw = createRaw()
  const es = Store.EventStore_createRejectingCas(raw, 0)
  const base = await es.OpenSnapshot()
  const a = envelope({ id: hexId(0xf) })
  const err = mustErr(await es.Append(base, toList([a])))
  assert.equal(caseOf(err), 'AppendCasRejected')
  assert.equal(isSome(await raw.ReadRef(Persist.StoreRef_canonical)), false, 'no ref witness, no committed history')
})

test('materialized_root_is_a_plain_tree_with_no_commit_history', async () => {
  // storage.md §6/§8: canonical ref 指向 root tree（不是 commit）；历史来自 event DAG，
  // 不来自 Git commit/branch/tag。
  const raw = createRaw()
  const es = createStore(raw)
  mustOk(await es.Append(await es.OpenSnapshot(), toList([envelope({ id: hexId(0x1) })])))
  const snapshot = await es.OpenSnapshot()

  const tree = await raw.ReadTree(Persist.RootOidModule_value(snapshot.RootOid))
  assert.ok(tree, 'root tree readable')
  const names = listItems(tree).map((entry) => entry.Name).sort()
  assert.deepEqual(names, ['events', 'payloads'], 'root contains only events/ + payloads/ subtrees')
})

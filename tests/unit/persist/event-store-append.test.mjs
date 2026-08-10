// tests/unit/persist/event-store-append.test.mjs
// Phase 2 Wave C — §9 Absent CAS append / retry / Publish; Converge unbound without gateway.

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, eventId, idValue, isSome, listItems, payloadOf, toList } from '../support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')

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

const createRaw = () => GitRaw.GitRawStore_createInMemory()
const createStore = (raw = createRaw()) => Store.EventStore_create(raw)

test('OpenSnapshot_Absent_returns_empty_root_without_publishing_ref', () => {
  const raw = createRaw()
  const es = createStore(raw)
  const snap = es.OpenSnapshot()
  assert.equal(typeof snapshotOid(snap), 'string')
  assert.equal(snapshotOid(snap).length, 40)
  assert.equal(isSome(raw.ReadRef(Persist.StoreRef_canonical)), false)
})

test('Append_Absent_CAS_publishes_canonical_ref', () => {
  const raw = createRaw()
  const es = createStore(raw)
  const base = es.OpenSnapshot()
  const a = envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' })
  const published = mustOk(es.Append(base, toList([a])), 'Append')
  const current = raw.ReadRef(Persist.StoreRef_canonical)
  assert.equal(isSome(current), true)
  assert.equal(Persist.GitObjectIdModule_value(current), snapshotOid(published))

  const blobs = mustOk(GitRaw.GitRawStore_listEventBlobs(raw, published.RootOid))
  assert.equal(listItems(blobs).length, 1)
})

test('Append_CAS_conflict_retries_on_fresh_root', () => {
  const raw = createRaw()
  const es = createStore(raw)
  const base = es.OpenSnapshot()
  const a = envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { n: 1 } })
  const b = envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', payload: { n: 2 } })

  mustOk(es.Append(base, toList([a])), 'first Append')
  // Stale base: second writer must refresh + rebuild (§9 bounded retry).
  const merged = mustOk(es.Append(base, toList([b])), 'stale Append')
  const blobs = mustOk(GitRaw.GitRawStore_listEventBlobs(raw, merged.RootOid))
  const ids = listItems(blobs)
    .map(([path]) => path.split('/').pop().replace(/\.jsonl$/, ''))
    .sort()
  assert.deepEqual(ids, [
    'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
  ])
})

test('Append_idempotent_when_EventIds_already_committed', () => {
  const raw = createRaw()
  const es = createStore(raw)
  const base = es.OpenSnapshot()
  const a = envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' })
  const first = mustOk(es.Append(base, toList([a])))
  const second = mustOk(es.Append(first, toList([a])))
  assert.equal(snapshotOid(first), snapshotOid(second))
})

test('Append_retry_exhausted_when_CAS_always_rejects', () => {
  const raw = createRaw()
  const es = Store.EventStore_createRejectingCas(raw, 2)
  const base = es.OpenSnapshot()
  const a = envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' })
  const err = mustErr(es.Append(base, toList([a])))
  assert.equal(caseOf(err), 'AppendRetryExhausted')
})

test('Append_CAS_rejected_when_maxRetries_zero', () => {
  const raw = createRaw()
  const es = Store.EventStore_createRejectingCas(raw, 0)
  const base = es.OpenSnapshot()
  const a = envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' })
  const err = mustErr(es.Append(base, toList([a])))
  assert.equal(caseOf(err), 'AppendCasRejected')
})

test('Publish_writes_payloads_then_CAS_appends', () => {
  const raw = createRaw()
  const es = createStore(raw)
  const base = es.OpenSnapshot()
  const body = new TextEncoder().encode('large-payload\n')
  const payloadOid = raw.WriteBlob(body)
  const payloadHex = Persist.GitObjectIdModule_value(payloadOid)
  const event = envelope({
    id: 'cccccccccccccccccccccccccccccccccccccccc',
    payloadRefs: [payloadHex],
  })
  const candidate = new Persist.AppendCandidate(base, toList([event]), toList([[payloadOid, body]]))
  const published = mustOk(es.Publish(candidate), 'Publish')
  const names = mustOk(GitRaw.GitRawStore_listPayloadNames(raw, published.RootOid))
  assert.deepEqual(listItems(names), [payloadHex])
})

test('Publish_IncompletePayloadClosure_when_payload_missing', () => {
  const raw = createRaw()
  const es = createStore(raw)
  const base = es.OpenSnapshot()
  const missing = 'ffffffffffffffffffffffffffffffffffffffff'
  const event = envelope({
    id: 'dddddddddddddddddddddddddddddddddddddddd',
    payloadRefs: [missing],
  })
  const candidate = new Persist.AppendCandidate(base, toList([event]), toList([]))
  const err = mustErr(es.Publish(candidate))
  assert.equal(caseOf(err), 'IncompletePayloadClosure')
})

test('Merge_delegates_to_structural_EventStoreMerge', () => {
  const raw = createRaw()
  const es = createStore(raw)
  const a = envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' })
  const b = envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb' })
  const SA = mustOk(GitRaw.GitRawStore_materializeSnapshot(raw, toList([a])))
  const SB = mustOk(GitRaw.GitRawStore_materializeSnapshot(raw, toList([b])))
  const merged = mustOk(es.Merge(toList([SA, SB])))
  const direct = mustOk(GitRaw.GitRawStore_materializeSnapshot(raw, toList([a, b])))
  assert.equal(snapshotOid(merged), snapshotOid(direct))
})

test('Refresh_observes_published_root', () => {
  const raw = createRaw()
  const es = createStore(raw)
  const base = es.OpenSnapshot()
  const a = envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' })
  const published = mustOk(es.Append(base, toList([a])))
  const refreshed = es.Refresh()
  assert.equal(snapshotOid(refreshed), snapshotOid(published))
})

test('Converge_unbound_without_gateway', () => {
  const es = createStore()
  const err = mustErr(es.Converge('origin'))
  assert.equal(caseOf(err), 'Transport')
  assert.match(payloadOf(err), /no GitGateway bound/)
})

test('Append_identity_collision_fail_closed', () => {
  const raw = createRaw()
  const es = createStore(raw)
  const base = es.OpenSnapshot()
  const left = envelope({ id: 'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee', payload: { status: 'open' } })
  mustOk(es.Append(base, toList([left])))
  const right = envelope({ id: 'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee', payload: { status: 'closed' } })
  const err = mustErr(es.Append(es.Refresh(), toList([right])))
  assert.equal(caseOf(err), 'StorageInvalid')
  assert.equal(caseOf(payloadOf(err)), 'IdentityCollision')
})

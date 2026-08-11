import assert from 'node:assert/strict'
import test from 'node:test'

import * as DomainStore from '../../../dist/Domain/EventStore.js'
import * as Events from '../../../dist/Domain/StrengthEvents.js'
import * as StrengthStore from '../../../dist/Infrastructure/Persist/StrengthStore.js'
import * as Raw from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import * as PersistStore from '../../../dist/Infrastructure/Persist/EventStore.js'
import * as Fold from '../../../dist/Infrastructure/Persist/EventStoreFold.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { StrengthBudget } from '../../../dist/Domain/StrengthBudget.js'
import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

const H = (text) => `H(${text})`
const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
const caseOf = (value) => value.cases()[value.tag]
const session = (value) => Id.SessionIdModule_create(value)
const run = (value) => Id.ProviderRunIdentityModule_create(value)
const decision = (value) => Id.StrengthDecisionIdModule_create(value)
const payload = (value) => DomainStore.PayloadRefModule_create(value)

const prepared = (refs = ['oid-a'], digest = 'frame-a') => Events.StrengthEvents_prepared(
  session('owner'), decision('d1'), run('run-1'), session('replica'),
  StrengthBudget.K1, 'anchor-a', digest, 123, toList(refs.map(payload)),
)

test('STRENGTH_006_017_strength_event_types_are_authoritative_store_vocabulary', () => {
  for (const name of [
    'StrengthCandidatePrepared',
    'StrengthCandidatePromoted',
    'StrengthFramesTraced',
    'StrengthCandidateAbandoned',
  ]) {
    assert.equal(Fold.AuthoritativeEventTypes_isKnown(name), true, name)
  }
})

test('STRENGTH_006_store_envelope_puts_large_material_only_in_payload_refs', () => {
  const first = StrengthStore.toEnvelope(H, prepared())
  const conflicting = StrengthStore.toEnvelope(H, prepared(['oid-b'], 'frame-b'))

  assert.equal(first.EventType, 'StrengthCandidatePrepared')
  assert.deepEqual(listItems(first.PayloadRefs).map(DomainStore.PayloadRefModule_value), ['oid-a'])
  assert.equal(Id.EventIdModule_value(first.EventId), Id.EventIdModule_value(conflicting.EventId), 'same decision+fact kind has one idempotency identity')

  const decoded = resultOf(StrengthStore.tryDecodeEnvelope(first))
  assert.equal(decoded.ok, true)
  assert.equal(caseOf(decoded.value), 'Prepared')
  assert.equal(decoded.value.fields[0].FrameDigest, 'frame-a')
  assert.deepEqual(listItems(decoded.value.fields[0].MaterialPayloads).map(DomainStore.PayloadRefModule_value), ['oid-a'])
})

test('STRENGTH_006_same_decision_different_prepared_material_is_store_identity_collision', () => {
  const raw = Raw.GitRawStore_createInMemory()
  const store = PersistStore.EventStore_create(raw)
  const firstRef = StrengthStore.storePayload(raw, new TextEncoder().encode('first'))
  const secondRef = StrengthStore.storePayload(raw, new TextEncoder().encode('second'))

  const first = Events.StrengthEvents_prepared(
    session('owner'), decision('d1'), run('run-1'), session('replica'),
    StrengthBudget.K1, 'anchor-a', 'frame-a', 5, toList([firstRef]),
  )
  const conflict = Events.StrengthEvents_prepared(
    session('owner'), decision('d1'), run('run-1'), session('replica'),
    StrengthBudget.K1, 'anchor-a', 'frame-b', 6, toList([secondRef]),
  )

  assert.equal(resultOf(StrengthStore.append(store, H, first)).ok, true)
  const rejected = resultOf(StrengthStore.append(store, H, conflict))
  assert.equal(rejected.ok, false)
  assert.equal(caseOf(rejected.error), 'StorageInvalid')
  assert.equal(caseOf(rejected.error.fields[0]), 'IdentityCollision')
})

test('STRENGTH_007_promotion_without_prepared_is_store_missing_parent', () => {
  const raw = Raw.GitRawStore_createInMemory()
  const store = PersistStore.EventStore_create(raw)
  const frameRef = StrengthStore.storePayload(raw, new TextEncoder().encode('frame'))
  const promotion = Events.StrengthEvents_promoted(
    session('owner'), decision('d1'), run('run-1'), 'frame-a', toList([frameRef]),
  )

  const rejected = resultOf(StrengthStore.append(store, H, promotion))
  assert.equal(rejected.ok, false)
  assert.equal(caseOf(rejected.error), 'StorageInvalid')
  assert.equal(caseOf(rejected.error.fields[0]), 'MissingParent')
})

test('STRENGTH_006_payload_bytes_use_the_unified_raw_object_store', () => {
  const raw = Raw.GitRawStore_createInMemory()
  const bytes = new Uint8Array([1, 2, 3, 4])
  const first = StrengthStore.storePayload(raw, bytes)
  const second = StrengthStore.storePayload(raw, bytes)
  assert.equal(DomainStore.PayloadRefModule_value(first), DomainStore.PayloadRefModule_value(second))

  const loaded = StrengthStore.tryReadPayload(raw, first)
  assert.deepEqual([...loaded], [...bytes])
})

test('STRENGTH_006_007_008_eventstore_roundtrip_rebuilds_projection', () => {
  const raw = Raw.GitRawStore_createInMemory()
  const store = PersistStore.EventStore_create(raw)
  const frameRef = StrengthStore.storePayload(raw, new TextEncoder().encode('frame-material'))
  const p = Events.StrengthEvents_prepared(
    session('owner'), decision('d1'), run('run-1'), session('replica'),
    StrengthBudget.K1, 'anchor-a', 'frame-a', 14, toList([frameRef]),
  )
  const m = Events.StrengthEvents_promoted(session('owner'), decision('d1'), run('run-1'), 'frame-a', toList([frameRef]))
  const t = Events.StrengthEvents_traced(decision('d1'), 10n, 12n)

  let appended = resultOf(StrengthStore.append(store, H, p))
  assert.equal(appended.ok, true)
  appended = resultOf(StrengthStore.append(store, H, m))
  assert.equal(appended.ok, true)
  appended = resultOf(StrengthStore.append(store, H, t))
  assert.equal(appended.ok, true)

  const projection = resultOf(StrengthStore.loadProjection(raw, appended.value))
  assert.equal(projection.ok, true)
  assert.equal(StrengthStore.isPromoted(decision('d1'), projection.value), true)
  const range = StrengthStore.tryTraceRange(decision('d1'), projection.value)
  assert.equal(range.StartInclusive, 10n)
  assert.equal(range.EndExclusive, 12n)
})

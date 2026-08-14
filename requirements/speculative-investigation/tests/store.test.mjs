// FROZEN — 2026-08-14. Rewritten for local NDJSON + canonical Integrator Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as DomainStore from '../../../dist/Persistence/EventStore/Model.js'
import * as Events from '../../../dist/Strength/Events.js'
import * as StrengthStore from '../../../dist/Strength/Persistence/Store.js'
import * as Vocabulary from '../../../dist/Persistence/EventStore/EventVocabulary.js'
import * as Id from '../../../dist/Foundation/Identity.js'
import { StrengthBudget } from '../../../dist/Strength/Budget.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { toList, listItems, resultOf, caseOf } from '../../verification-system/tests/support/domain.mjs'

const H = (text) => `H(${text})`
const session = (value) => Id.SessionIdModule_create(value)
const run = (value) => Id.ProviderRunIdentityModule_create(value)
const decision = (value) => Id.StrengthDecisionIdModule_create(value)
const payload = (value) => DomainStore.PayloadRefModule_create(value)

const prepared = (refs = ['payload-a'], digest = 'frame-a') => Events.StrengthEvents_prepared(
  session('owner'), decision('d1'), run('run-1'), session('replica'),
  StrengthBudget.K1, 'anchor-a', digest, 123, toList(refs.map(payload)),
)

const unwrap = (value) => {
  const result = resultOf(value)
  assert.equal(result.ok, true, `expected Ok: ${JSON.stringify(result.error)}`)
  return result.value
}

const writePayload = async (store, text) => unwrap(await store.WritePayload(new TextEncoder().encode(text)))

test('STRENGTH_006_017_strength_event_types_are_authoritative_store_vocabulary', () => {
  for (const name of [
    'StrengthCandidatePrepared',
    'StrengthCandidatePromoted',
    'StrengthFramesTraced',
    'StrengthCandidateAbandoned',
  ]) {
    assert.equal(Vocabulary.isKnown(name), true, name)
  }
})

test('STRENGTH_006_store_envelope_puts_large_material_only_in_payload_refs', () => {
  const first = StrengthStore.toEnvelope(H, prepared())
  const conflicting = StrengthStore.toEnvelope(H, prepared(['payload-b'], 'frame-b'))

  assert.equal(first.EventType, 'StrengthCandidatePrepared')
  assert.deepEqual(listItems(first.PayloadRefs).map(DomainStore.PayloadRefModule_value), ['payload-a'])
  assert.equal(Id.EventIdModule_value(first.EventId), Id.EventIdModule_value(conflicting.EventId))

  const decoded = resultOf(StrengthStore.tryDecodeEnvelope(first))
  assert.equal(decoded.ok, true)
  assert.equal(caseOf(decoded.value), 'Prepared')
  assert.equal(decoded.value.fields[0].FrameDigest, 'frame-a')
})

test('STRENGTH_006_same_decision_different_prepared_material_is_identity_collision', async () => {
  const local = createLocalEventStore()
  try {
    const firstRef = await writePayload(local.store, 'first')
    const secondRef = await writePayload(local.store, 'second')
    const first = Events.StrengthEvents_prepared(
      session('owner'), decision('d1'), run('run-1'), session('replica'),
      StrengthBudget.K1, 'anchor-a', 'frame-a', 5, toList([firstRef]),
    )
    const conflict = Events.StrengthEvents_prepared(
      session('owner'), decision('d1'), run('run-1'), session('replica'),
      StrengthBudget.K1, 'anchor-a', 'frame-b', 6, toList([secondRef]),
    )

    assert.equal(resultOf(await StrengthStore.append(local.store, H, first)).ok, true)
    const rejected = resultOf(await StrengthStore.append(local.store, H, conflict))
    assert.equal(rejected.ok, false)
    assert.equal(caseOf(rejected.error), 'StorageInvalid')
    assert.equal(caseOf(rejected.error.fields[0]), 'IdentityCollision')
  } finally {
    local.close()
  }
})

test('STRENGTH_007_promotion_without_prepared_is_missing_parent', async () => {
  const local = createLocalEventStore()
  try {
    const frameRef = await writePayload(local.store, 'frame')
    const promotion = Events.StrengthEvents_promoted(
      session('owner'), decision('d1'), run('run-1'), 'frame-a', toList([frameRef]),
    )
    const rejected = resultOf(await StrengthStore.append(local.store, H, promotion))
    assert.equal(rejected.ok, false)
    assert.equal(caseOf(rejected.error), 'StorageInvalid')
    assert.equal(caseOf(rejected.error.fields[0]), 'MissingParent')
  } finally {
    local.close()
  }
})

test('STRENGTH_006_payload_bytes_are_local_content_addressed_payloads', async () => {
  const local = createLocalEventStore()
  try {
    const bytes = new Uint8Array([1, 2, 3, 4])
    const first = unwrap(await local.store.WritePayload(bytes))
    const second = unwrap(await local.store.WritePayload(bytes))
    assert.equal(DomainStore.PayloadRefModule_value(first), DomainStore.PayloadRefModule_value(second))
    const loaded = unwrap(await local.store.ReadPayload(first))
    assert.deepEqual([...loaded], [...bytes])
  } finally {
    local.close()
  }
})

test('STRENGTH_006_007_008_integrator_Current_tracks_projection_without_history_scan', async () => {
  const local = createLocalEventStore()
  try {
    const frameRef = await writePayload(local.store, 'frame-material')
    const p = Events.StrengthEvents_prepared(
      session('owner'), decision('d1'), run('run-1'), session('replica'),
      StrengthBudget.K1, 'anchor-a', 'frame-a', 14, toList([frameRef]),
    )
    const m = Events.StrengthEvents_promoted(session('owner'), decision('d1'), run('run-1'), 'frame-a', toList([frameRef]))
    const t = Events.StrengthEvents_traced(decision('d1'), 10n, 12n)

    assert.equal(resultOf(await StrengthStore.append(local.store, H, p)).ok, true)
    assert.equal(resultOf(await StrengthStore.append(local.store, H, m)).ok, true)
    assert.equal(resultOf(await StrengthStore.append(local.store, H, t)).ok, true)

    const projection = local.store.TryCurrent('Strength')
    assert.equal(StrengthStore.isPromoted(decision('d1'), projection), true)
    const range = StrengthStore.tryTraceRange(decision('d1'), projection)
    assert.equal(range.StartInclusive, 10n)
    assert.equal(range.EndExclusive, 12n)
  } finally {
    local.close()
  }
})

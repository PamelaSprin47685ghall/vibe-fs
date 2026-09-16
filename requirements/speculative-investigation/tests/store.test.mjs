// Strength persistence semantics through the Strength owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const H = (text) => `H(${text})`
const prepared = ({ refs = ['payload-a'], digest = 'frame-a', decision = 'd1' } = {}) => Strength.eventPrepared('owner', decision, 'run-1', 'replica', 'K1', 'anchor-a', digest, 123, refs)
const promoted = ({ refs = ['payload-a'], digest = 'frame-a', decision = 'd1' } = {}) => Strength.eventPromoted('owner', decision, 'run-1', digest, refs)
const append = async (store, event) => Strength.storeAppend(store, H, event)
const writePayload = async (store, text) => {
  const result = await Strength.storeWritePayload(store, new TextEncoder().encode(text))
  assert.equal(result.ok, true)
  return result.value
}

test('WHAT[SPEC-INV-006] STRENGTH_006_017_strength_event_types_are_authoritative_store_vocabulary', () => {
  assert.deepEqual([
    Strength.eventType(prepared()),
    Strength.eventType(promoted()),
    Strength.eventType(Strength.eventTraced('d1', 1n, 2n)),
    Strength.eventType(Strength.eventAbandoned('d1', 'run-1')),
  ], ['StrengthCandidatePrepared', 'StrengthCandidatePromoted', 'StrengthFramesTraced', 'StrengthCandidateAbandoned'])
})

test('WHAT[SPEC-INV-006] STRENGTH_006_store_envelope_puts_large_material_only_in_payload_refs', () => {
  const first = Strength.envelopeView(Strength.storeToEnvelope(H, prepared()))
  const conflicting = Strength.envelopeView(Strength.storeToEnvelope(H, prepared({ refs: ['payload-b'], digest: 'frame-b' })))
  assert.equal(first.eventType, 'StrengthCandidatePrepared')
  assert.deepEqual(first.payloadRefs, ['payload-a'])
  assert.equal(first.id, conflicting.id)
  const decoded = Strength.storeTryDecodeEnvelope(Strength.storeToEnvelope(H, prepared()))
  assert.equal(decoded.ok, true)
  assert.equal(decoded.value.kind, 'Prepared')
  assert.equal(decoded.value.frameDigest, 'frame-a')
})

test('WHAT[SPEC-INV-006] STRENGTH_006_same_decision_different_prepared_material_is_identity_collision', async () => {
  const local = createLocalEventStore()
  try {
    const firstRef = await writePayload(local.store, 'first')
    const secondRef = await writePayload(local.store, 'second')
    const first = Strength.eventPrepared('owner', 'd1', 'run-1', 'replica', 'K1', 'anchor-a', 'frame-a', 5, [firstRef])
    const conflict = Strength.eventPrepared('owner', 'd1', 'run-1', 'replica', 'K1', 'anchor-a', 'frame-b', 6, [secondRef])
    assert.equal((await append(local.store, first)).ok, true)
    const rejected = await append(local.store, conflict)
    assert.equal(rejected.ok, false)
    assert.equal(rejected.error, 'IdentityCollision')
  } finally { local.close() }
})

test('WHAT[SPEC-INV-007] STRENGTH_007_promotion_without_prepared_is_missing_parent', async () => {
  const local = createLocalEventStore()
  try {
    const frameRef = await writePayload(local.store, 'frame')
    const rejected = await append(local.store, promoted({ refs: [frameRef] }))
    assert.equal(rejected.ok, false)
    assert.equal(rejected.error, 'MissingParent')
  } finally { local.close() }
})

test('WHAT[SPEC-INV-006] STRENGTH_006_payload_bytes_are_local_content_addressed_payloads', async () => {
  const local = createLocalEventStore()
  try {
    const bytes = new Uint8Array([1, 2, 3, 4])
    const first = await Strength.storeWritePayload(local.store, bytes)
    const second = await Strength.storeWritePayload(local.store, bytes)
    assert.equal(first.value, second.value)
    const loaded = await Strength.storeReadPayload(local.store, first.value)
    assert.deepEqual([...loaded.value], [...bytes])
  } finally { local.close() }
})

test('WHAT[SPEC-INV-006] STRENGTH_006_integrator_Current_reflects_Prepared_binding_without_history_scan', async () => {
  const local = createLocalEventStore()
  try {
    const ref = await writePayload(local.store, 'frame-material')
    assert.equal((await append(local.store, Strength.eventPrepared('owner', 'd1', 'run-1', 'replica', 'K1', 'anchor-a', 'frame-a', 14, [ref]))).ok, true)
    const projection = Strength.storeCurrent(local.store)
    assert.equal(Strength.projectionDecisionForTarget('run-1', projection), 'd1')
    assert.equal(Strength.projectionIsPromoted('d1', projection), false)
  } finally { local.close() }
})

test('WHAT[SPEC-INV-007] STRENGTH_007_integrator_Current_reflects_Promoted_without_history_scan', async () => {
  const local = createLocalEventStore()
  try {
    const ref = await writePayload(local.store, 'frame-material')
    assert.equal((await append(local.store, prepared({ refs: [ref] }))).ok, true)
    assert.equal((await append(local.store, promoted({ refs: [ref] }))).ok, true)
    assert.equal(Strength.projectionIsPromoted('d1', Strength.storeCurrent(local.store)), true)
  } finally { local.close() }
})

test('WHAT[SPEC-INV-008] STRENGTH_008_integrator_Current_reflects_Traced_range_without_history_scan', async () => {
  const local = createLocalEventStore()
  try {
    const ref = await writePayload(local.store, 'frame-material')
    assert.equal((await append(local.store, prepared({ refs: [ref] }))).ok, true)
    assert.equal((await append(local.store, promoted({ refs: [ref] }))).ok, true)
    assert.equal((await append(local.store, Strength.eventTraced('d1', 10n, 12n))).ok, true)
    const projection = Strength.storeCurrent(local.store)
    assert.equal(Strength.projectionIsPromoted('d1', projection), true)
    assert.deepEqual(Strength.projectionTraceRange('d1', projection), { startInclusive: 10n, endExclusive: 12n })
  } finally { local.close() }
})

test('WHAT[SPEC-INV-006] STRENGTH_006_prepared_event_persists_nominal_budget_without_duplicate_derived_value_estimates', () => {
  const envelope = Strength.storeToEnvelope(H, prepared({ digest: 'frame-exact' }))
  const decoded = Strength.storeTryDecodeEnvelope(envelope)
  assert.equal(decoded.ok, true)
  const payload = decoded.value
  assert.equal(payload.budget, 'K1')
  assert.equal('V0' in payload, false)
  assert.equal('V1' in payload, false)
  assert.equal('V2' in payload, false)
  assert.equal('P1' in payload, false)
  assert.equal('P2' in payload, false)
  assert.equal('estimate' in payload, false)
  assert.equal('prediction' in payload, false)
})

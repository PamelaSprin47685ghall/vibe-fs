import assert from 'node:assert/strict'
import test from 'node:test'
import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'

test('WHAT[HOST-BOUNDARY-004] TurnUnknown is not a TurnOutcome case', () => {
  // ReconcileSurface.tryOutcome rejects TurnUnknown — it is not accepted as a
  // publishable TurnOutcome. The production classifier throws, tryOutcome
  // catches and returns { accepted: false }.
  const result = ReconcileSurface.tryOutcome('TurnUnknown')
  assert.equal(result.accepted, false)
})

test('WHAT[HOST-BOUNDARY-004] TurnUnknown lives only in SnapshotObservation', () => {
  assert.equal(ReconcileSurface.isSnapshotObservation('TurnUnknown'), true)
  assert.equal(ReconcileSurface.isSnapshotObservation('TurnCompleted'), false)
  assert.equal(ReconcileSurface.isSnapshotObservation('TurnInProgress'), false)
})

test('WHAT[HOST-BOUNDARY-004] outcomeOf refuses TurnUnknown instead of minting a TurnOutcome', () => {
  const result = ReconcileSurface.tryOutcome('TurnUnknown')
  assert.equal(result.accepted, false)
  assert.match(result.error, /SnapshotObservation, not a TurnOutcome/)
})

test('WHAT[HOST-BOUNDARY-004] TurnUnknown is not publishable', () => {
  assert.equal(ReconcileSurface.isPublishableOutcome('TurnUnknown'), false)
  assert.equal(ReconcileSurface.isPublishableOutcome('TurnCompleted'), true)
})

test('WHAT[HOST-BOUNDARY-004] classifyTurn rejects TurnUnknown as terminal or provisional', () => {
  // The production classifier must throw on TurnUnknown — it is neither
  // terminal nor provisional, it is a private snapshot observation.
  assert.throws(() => ReconcileSurface.classifyTurn('TurnUnknown'), /SnapshotObservation, not a TurnOutcome/)
})

// ── Mutation sensitivity ─────────────────────────────────────────────────
//
// If someone adds TurnUnknown as a TurnOutcome case, isPublishableOutcome
// would still reject it (defense in depth), but tryOutcome would start
// accepting it. This canary catches that regression.

test('WHAT[HOST-BOUNDARY-004] mutation_canary_TurnUnknown_must_not_be_accepted_as_outcome', () => {
  const result = ReconcileSurface.tryOutcome('TurnUnknown')
  assert.equal(result.accepted, false,
    'mutation guard: TurnUnknown must be rejected by tryOutcome, not silently accepted')
})

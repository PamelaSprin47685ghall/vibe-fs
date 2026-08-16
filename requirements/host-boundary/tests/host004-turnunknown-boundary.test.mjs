// host-boundary HOST-BOUNDARY-004: TurnUnknown is a reconciliation-private
// observation (SnapshotObservation), never a publishable TurnOutcome case.
// The structured-workflow package covers the pure decision behavior; this
// contract test pins the type boundary itself at the compiled surface.
//
// Given: the compiled ReconcileProgram union surface.
// When: inspecting TurnOutcome cases / minting via outcomeOf / observing
//       SnapshotObservation.
// Expected: TurnUnknown exists only under SnapshotObservation; outcomeOf
//       refuses it (throw, never a TurnOutcome); publishDecision never sees it.
// Forbidden: TurnUnknown as a TurnOutcome case, or silently collapsing it
//       into TurnFailed/TurnInProgress.

import assert from 'node:assert/strict'
import test from 'node:test'
import { reconcileProgram } from '../../verification-system/tests/support/domain.mjs'

test('WHAT[HOST-BOUNDARY-004] TurnUnknown is not a TurnOutcome case', () => {
  const turnOutcomeCases = reconcileProgram.turnOutcomeCases()
  assert.equal(
    turnOutcomeCases.includes('TurnUnknown'),
    false,
    `TurnUnknown must not be a TurnOutcome case; have: ${turnOutcomeCases.join(', ')}`,
  )
})

test('WHAT[HOST-BOUNDARY-004] TurnUnknown lives only in SnapshotObservation', () => {
  const observationCases = reconcileProgram.snapshotObservationCases()
  assert.deepEqual(observationCases, ['TurnUnknown'], 'SnapshotObservation is the private carrier for TurnUnknown only')
  assert.equal(reconcileProgram.snapshotUnknownIsInstance(), true)
})

test('WHAT[HOST-BOUNDARY-004] outcomeOf refuses TurnUnknown instead of minting a TurnOutcome', () => {
  const outcome = reconcileProgram.tryOutcome('TurnUnknown')
  assert.equal(outcome.accepted, false)
  assert.match(outcome.error, /TurnUnknown is SnapshotObservation, not a TurnOutcome/)
})

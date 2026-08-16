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

const mod = await import('../../../dist/Composition/Turn/Program.js')

test('WHAT[HOST-BOUNDARY-004] TurnUnknown is not a TurnOutcome case', () => {
  const turnOutcomeCases = Object.create(mod.TurnOutcome.prototype).cases()
  assert.equal(
    turnOutcomeCases.includes('TurnUnknown'),
    false,
    `TurnUnknown must not be a TurnOutcome case; have: ${turnOutcomeCases.join(', ')}`,
  )
})

test('WHAT[HOST-BOUNDARY-004] TurnUnknown lives only in SnapshotObservation', () => {
  const observationCases = Object.create(mod.SnapshotObservation.prototype).cases()
  assert.deepEqual(observationCases, ['TurnUnknown'], 'SnapshotObservation is the private carrier for TurnUnknown only')
  assert.equal(mod.SnapshotObservation.TurnUnknown instanceof mod.SnapshotObservation, true)
})

test('WHAT[HOST-BOUNDARY-004] outcomeOf refuses TurnUnknown instead of minting a TurnOutcome', () => {
  assert.throws(
    () => mod.outcomeOf('TurnUnknown'),
    /TurnUnknown is SnapshotObservation, not a TurnOutcome/,
    'minting TurnUnknown as a TurnOutcome must fail closed, never silently collapse',
  )
})

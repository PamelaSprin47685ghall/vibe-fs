import assert from 'node:assert/strict'
import test from 'node:test'
import { turnUnknown } from './support/host-surface.mjs'

test('WHAT[HOST-BOUNDARY-004] TurnUnknown is not a TurnOutcome case', () => {
  assert.equal(turnUnknown.turnOutcomeCases().includes('TurnUnknown'), false)
})

test('WHAT[HOST-BOUNDARY-004] TurnUnknown lives only in SnapshotObservation', () => {
  assert.deepEqual(turnUnknown.snapshotObservationCases(), ['TurnUnknown'])
  assert.equal(turnUnknown.snapshotUnknownIsInstance(), true)
})

test('WHAT[HOST-BOUNDARY-004] outcomeOf refuses TurnUnknown instead of minting a TurnOutcome', () => {
  const outcome = turnUnknown.tryOutcome('TurnUnknown')
  assert.equal(outcome.accepted, false)
  assert.match(outcome.error, /SnapshotObservation, not a TurnOutcome/)
})

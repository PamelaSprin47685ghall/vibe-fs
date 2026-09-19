import assert from 'node:assert/strict'
import test from 'node:test'
import * as reconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'

const evidence = {
  snapshotError: (reason) => reconcileSurface.evidenceSnapshotError(reason),
  noTurn: () => reconcileSurface.evidenceNoTurn(),
  provisional: (outcome) => reconcileSurface.evidenceProvisional(outcome),
  unknown: () => reconcileSurface.evidenceUnknown(),
  terminal: (outcome) => reconcileSurface.evidenceTerminal(outcome),
  sessionCleared: () => reconcileSurface.evidenceSessionCleared(),
}

const wake = {
  idle: (session = 'ses-a', attemptSerial = 1) => reconcileSurface.idleWake(session, attemptSerial),
  retry: () => reconcileSurface.retryWake(),
  failure: () => reconcileSurface.failureWake(),
  abort: () => reconcileSurface.abortWake(),
}

const name = (observation, signal = wake.retry()) =>
  reconcileSurface.decisionName(reconcileSurface.decideStep(signal, observation))

test('WHAT[structured-workflow-009] operator abort is a control-plane wake, never a business outcome', () => {
  // EXEC-020 / structured-workflow-009: cancellation/interruption are control
  // events, not business result data. The abort signal lives in ReconcileWake
  // (a typed control-plane channel) and must never be minted as a TurnOutcome.
  const wakes = [wake.idle('ses-a', 1), wake.retry(), wake.failure(), wake.abort()]
  assert.deepEqual(
    wakes.map((value) => value.kind),
    ['IdleWake', 'RetryWake', 'FailureWake', 'AbortWake'],
    'control-plane wakes are Idle/Retry/Failure/Abort observations',
  )
  assert.equal(wakes[0].hasQuiescence, true)
  assert.equal(wakes[1].hasQuiescence, false)
  assert.equal(wakes[2].hasQuiescence, false)
  assert.equal(wakes[3].hasQuiescence, false)
  assert.equal(reconcileSurface.isPublishableOutcome('AbortWake'), false)
  assert.equal(reconcileSurface.isPublishableOutcome('TurnFailed'), true)
})

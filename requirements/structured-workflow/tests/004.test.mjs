import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { TaskResultListSurface_traverseM } = await import("../../../dist/Foundation/FsToolkitFableCompat.js");
const outcomeSurface = await import("../../../dist/Foundation/OutcomeSurface.js");


test('WHAT[structured-workflow-004] Fable async Result plumbing provides sequential short-circuiting traversal', async () => {
  const traversedOk = await TaskResultListSurface_traverseM((x) => Promise.resolve(x > 0), [1, 2, 3])
  assert.deepEqual(traversedOk, ['Ok', 1, 2, 3])

  const traversedErr = await TaskResultListSurface_traverseM((x) => Promise.resolve(x !== 2), [1, 2, 3])
  assert.deepEqual(traversedErr, ['Error', 2])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const reconcileSurface = await import("../../../dist/Composition/Turn/ReconcileSurface.js");

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

test('WHAT[structured-workflow-004] RECONCILE_PROGRAM_001: isTerminalOutcome classifies terminal vs provisional', () => {
  assert.equal(typeof reconcileSurface.isTerminalOutcome, 'function')
  assert.equal(typeof reconcileSurface.classifyTurn, 'function')

  for (const outcome of ['TurnCompleted', 'TurnAborted', 'TurnFailed']) {
    assert.equal(reconcileSurface.isTerminalOutcome(outcome), true)
    assert.deepEqual(reconcileSurface.classifyTurn(outcome), {
      outcome,
      state: 'terminal',
      isTerminal: true,
    })
  }

  for (const outcome of ['TurnInProgress', 'TurnNeedsContinuation']) {
    assert.equal(reconcileSurface.isTerminalOutcome(outcome), false)
    assert.deepEqual(reconcileSurface.classifyTurn(outcome), {
      outcome,
      state: 'provisional',
      isTerminal: false,
    })
  }

  // HOST-004 Clean Break: TurnUnknown is a snapshot observation, not a
  // publishable business turn. The owner rejects it rather than classifying it
  // as a provisional or terminal outcome.
  assert.equal(reconcileSurface.isSnapshotObservation('TurnUnknown'), true)
  assert.equal(reconcileSurface.isPublishableOutcome('TurnUnknown'), false)
  assert.throws(() => reconcileSurface.isTerminalOutcome('TurnUnknown'), /TurnUnknown/)
})
test('WHAT[structured-workflow-004] RECONCILE_PROGRAM_003: decideStep produces one decision per causal edge (host-boundary-005)', () => {
  assert.equal(typeof reconcileSurface.decideStep, 'function')
  assert.equal(typeof reconcileSurface.decisionName, 'function')

  // host-boundary-005: a later read needs a new coarse Host signal or exact
  // projection-change edge. The decision API has no read-budget input.
  //
  // SnapshotError / NoTurn → StopPass (nothing to act on).
  assert.equal(name(evidence.snapshotError('transient')), 'StopPass')
  assert.equal(name(evidence.noTurn()), 'StopPass')

  // Provisional: IdleWake → Publish (quiescent, belongs to business repair);
  // Retry/Failure/Abort → StopPass (provider status can race the public
  // session projection; Scheduler parks and the exact terminal edge re-kicks).
  assert.equal(name(evidence.provisional('TurnInProgress'), wake.idle('ses-a', 1)), 'Publish')
  assert.equal(name(evidence.provisional('TurnNeedsContinuation'), wake.idle('ses-a', 1)), 'Publish')
  assert.equal(name(evidence.provisional('TurnInProgress'), wake.retry()), 'StopPass')
  assert.equal(name(evidence.provisional('TurnNeedsContinuation'), wake.retry()), 'StopPass')
  assert.equal(name(evidence.provisional('TurnInProgress'), wake.failure()), 'StopPass')
  assert.equal(name(evidence.provisional('TurnInProgress'), wake.abort()), 'StopPass')

  // Unknown: IdleWake → Publish; Retry/Failure/Abort → StopPass.
  assert.equal(name(evidence.unknown(), wake.retry()), 'StopPass')
  assert.equal(name(evidence.unknown(), wake.failure()), 'StopPass')
  assert.equal(name(evidence.unknown(), wake.abort()), 'StopPass')
  assert.equal(name(evidence.unknown(), wake.idle('ses-a', 1)), 'Publish')

  // Successful/aborted terminal observations publish directly. A failed
  // provider terminal must wait for the matching typed physical witness.
  assert.equal(name(evidence.terminal('TurnCompleted')), 'Publish')
  assert.equal(name(evidence.terminal('TurnAborted')), 'Publish')
  assert.equal(name(evidence.terminal('TurnFailed')), 'StopPass')

  const failedPhysical = 'msg-structured-failure'
  assert.equal(
    reconcileSurface.decisionName(
      reconcileSurface.decideStep(
        reconcileSurface.failureWakeFor(failedPhysical),
        reconcileSurface.evidenceTerminalFor(failedPhysical, 'TurnFailed'),
      ),
    ),
    'Publish',
  )

  // Session cleared → StopPass.
  assert.equal(name(evidence.sessionCleared()), 'StopPass')

  assert.deepEqual(reconcileSurface.decideStep(wake.retry(), evidence.unknown()), { name: 'StopPass' })
})
test('WHAT[structured-workflow-004] RECONCILE_PROGRAM_004: publishDecision gates already-published terminal and provisional', () => {
  assert.equal(typeof reconcileSurface.publishDecision, 'function')
  assert.equal(typeof reconcileSurface.consumeKey, 'function')
  assert.deepEqual(reconcileSurface.acceptedTurnFields(), ['session', 'physical', 'providerRun', 'outcome'])

  const terminal = reconcileSurface.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnCompleted',
  })
  const provisional = reconcileSurface.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnInProgress',
  })
  const laterTerminal = reconcileSurface.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnCompleted',
  })

  const empty = reconcileSurface.empty()

  // First provisional publish allowed; marks provisional map.
  const firstProv = reconcileSurface.publishDecision(empty, provisional)
  assert.equal(firstProv.shouldPublish, true)
  assert.equal(reconcileSurface.provisionalHas(firstProv.maps, provisional), true)
  assert.equal(reconcileSurface.consumedHas(firstProv.maps, provisional), false)

  // Same provisional token again: sealed out.
  const secondProv = reconcileSurface.publishDecision(firstProv.maps, provisional)
  assert.equal(secondProv.shouldPublish, false)

  // Terminal with same run identity is not sealed by provisional map.
  const term = reconcileSurface.publishDecision(firstProv.maps, laterTerminal)
  assert.equal(term.shouldPublish, true)
  assert.equal(reconcileSurface.consumedHas(term.maps, laterTerminal), true)
  // Terminal mark clears provisional for the session.
  assert.equal(reconcileSurface.provisionalHas(term.maps, provisional), false)

  // Duplicate terminal token sealed.
  const dupTerm = reconcileSurface.publishDecision(term.maps, laterTerminal)
  assert.equal(dupTerm.shouldPublish, false)

  // clearProvisional removes provisional seal without touching consumed.
  const cleared = reconcileSurface.clearProvisional(firstProv.maps, 'ses-a')
  assert.equal(reconcileSurface.provisionalHas(cleared, provisional), false)
})
}

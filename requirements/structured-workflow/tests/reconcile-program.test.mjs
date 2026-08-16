// requirements/structured-workflow/tests/reconcile-program.test.mjs — PR4 pure Reconcile Domain.
//
// Locks Evidence → Decision + publish seals. Command/Reply/Step AST and
// TraceInterpreter are deleted; workflow CE lives in Application/Reconciliation/Reconciler.fs
// and is covered by tests/unit/execution/reconcile-supervisor.test.mjs.
//
// The production ReconcileSurface is the only boundary used here. Inputs and
// observations are plain JS values; the domain's private representation is not
// part of the semantic contract.

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

const name = (remaining, observation, signal = wake.retry()) =>
  reconcileSurface.decisionName(reconcileSurface.decideStep(signal, remaining, observation))

// ── pure classifiers ─────────────────────────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-007] RECONCILE_PROGRAM_001: isTerminalOutcome classifies terminal vs provisional', () => {
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

// ── decideStep: bounded causal reread → next pure decision ───────────────────

test('WHAT[STRUCTURED-WORKFLOW-007] RECONCILE_PROGRAM_003: decideStep bounds causal rereads and stops on exhaustion', () => {
  assert.equal(typeof reconcileSurface.decideStep, 'function')
  assert.equal(typeof reconcileSurface.decisionName, 'function')
  assert.equal(typeof reconcileSurface.clearsContinuationCandidate, 'function')

  // Non-terminal evidence with budget remaining → Reread.
  assert.equal(name(3, evidence.snapshotError('transient')), 'Reread')
  assert.equal(name(3, evidence.noTurn()), 'Reread')
  assert.equal(name(3, evidence.provisional('TurnInProgress')), 'Reread')
  assert.equal(name(3, evidence.provisional('TurnNeedsContinuation')), 'Reread')
  assert.equal(name(3, evidence.unknown()), 'Reread')

  // Non-terminal with budget exhausted → fail closed per evidence kind: nothing
  // to act on (SnapshotError/NoTurn) keeps StopPass; Provisional publishes the
  // stop text; Unknown Publishes a stable observation only when the pass
  // carries idle evidence (HOST-004 rev.3 / rabbit §7) — retry/failure wakes
  // prove observation stability, not quiescence, and never hand off.
  assert.equal(name(0, evidence.snapshotError('transient')), 'StopPass')
  assert.equal(name(0, evidence.noTurn()), 'StopPass')
  assert.equal(name(0, evidence.provisional('TurnInProgress')), 'Publish')
  assert.equal(name(0, evidence.provisional('TurnNeedsContinuation')), 'Publish')
  assert.equal(name(0, evidence.unknown(), wake.retry()), 'StopPass')
  assert.equal(name(0, evidence.unknown(), wake.failure()), 'StopPass')
  assert.equal(name(0, evidence.unknown(), wake.idle('ses-a', 1)), 'Publish')

  // Unknown clears continuation candidate; provisional keeps it.
  assert.equal(
    reconcileSurface.clearsContinuationCandidate(
      reconcileSurface.decideStep(wake.retry(), 3, evidence.unknown()),
    ),
    true,
  )
  assert.equal(
    reconcileSurface.clearsContinuationCandidate(
      reconcileSurface.decideStep(wake.retry(), 3, evidence.provisional('TurnInProgress')),
    ),
    false,
  )

  // Terminal → Publish regardless of remaining and wake.
  assert.equal(name(0, evidence.terminal('TurnCompleted')), 'Publish')
  assert.equal(name(3, evidence.terminal('TurnAborted')), 'Publish')
  assert.equal(name(3, evidence.terminal('TurnFailed')), 'Publish')

  // HOST-004: an exhausted operator-abort wake must not Publish Unknown /
  // Provisional (business must not mint InteractionRepair / bare "#");
  // it StopPasses until the real TurnAborted terminal.
  assert.equal(name(0, evidence.unknown(), wake.abort()), 'StopPass')
  assert.equal(name(0, evidence.provisional('TurnInProgress'), wake.abort()), 'StopPass')
  assert.equal(name(0, evidence.provisional('TurnNeedsContinuation'), wake.abort()), 'StopPass')
  // Bounded reread still allowed under abort so a genuine TurnAborted can settle.
  assert.equal(name(3, evidence.unknown(), wake.abort()), 'Reread')

  // Session cleared → StopPass.
  assert.equal(name(3, evidence.sessionCleared()), 'StopPass')

  // Reread decrements remaining (no time/backoff information carried).
  const rereadDecision = reconcileSurface.decideStep(
    wake.retry(),
    3,
    evidence.provisional('TurnInProgress'),
  )
  assert.equal(reconcileSurface.decisionName(rereadDecision), 'Reread')
  assert.equal(rereadDecision.rereadsRemaining, 2)
  assert.equal(reconcileSurface.clearsContinuationCandidate(rereadDecision), false)
})

// ── publishDecision: consumed (terminal) vs provisional maps ─────────────────

test('WHAT[STRUCTURED-WORKFLOW-007] RECONCILE_PROGRAM_004: publishDecision gates already-published terminal and provisional', () => {
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

test('WHAT[STRUCTURED-WORKFLOW-009] RECONCILE_PROGRAM_005: TurnUnknown never crosses the stable business-turn boundary', () => {
  // HOST-004 / rabbit §7: TurnUnknown is type-unreachable for publishDecision
  // (not a TurnOutcome). IdleWake + exhausted Unknown → Publish (observation
  // handoff only); business repair lives in TurnWorkflow / InteractionRepair.
  const handoff = reconcileSurface.decideStep(wake.idle('ses-a', 1), 0, evidence.unknown())
  assert.equal(reconcileSurface.decisionName(handoff), 'Publish')

  // The owner-level observations keep the clean-break contract without exposing
  // DU constructors or reflection metadata.
  assert.equal(reconcileSurface.isSnapshotObservation('TurnUnknown'), true)
  assert.equal(reconcileSurface.isPublishableOutcome('TurnUnknown'), false)
  assert.equal(reconcileSurface.tryOutcome('TurnUnknown').accepted, false)
  assert.equal(reconcileSurface.tryOutcome('TurnUnknown').name, undefined)
})

test('WHAT[STRUCTURED-WORKFLOW-002] RECONCILE_PROGRAM_006: Domain surface has no Command/Reply/Trace AST exports', () => {
  // The semantic owner exposes named observations and opaque publish maps, not
  // a second-runtime AST. Presence of the owner operations is the contract;
  // emitted export enumeration is deliberately not part of this test.
  assert.equal(typeof reconcileSurface.decideStep, 'function')
  assert.equal(typeof reconcileSurface.publishDecision, 'function')
  assert.equal(typeof reconcileSurface.acceptedTurnFields, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-009] RECONCILE_PROGRAM_007: TurnUnknown is SnapshotObservation, not TurnOutcome', () => {
  // The five accepted JS outcome names are checked through the owner’s stable
  // acceptance result, never through DU case metadata.
  for (const outcome of [
    'TurnInProgress',
    'TurnNeedsContinuation',
    'TurnCompleted',
    'TurnAborted',
    'TurnFailed',
  ]) {
    assert.equal(reconcileSurface.tryOutcome(outcome).accepted, true)
  }
  assert.equal(reconcileSurface.isSnapshotObservation('TurnUnknown'), true)
  assert.equal(reconcileSurface.tryOutcome('TurnUnknown').accepted, false)
  assert.equal(reconcileSurface.isPublishableOutcome('TurnUnknown'), false)

  // outcomeOf must refuse TurnUnknown as a TurnOutcome. The owner returns a
  // rejected observation rather than minting Unknown or collapsing it into
  // TurnFailed.
  const refused = reconcileSurface.tryOutcome('TurnUnknown')
  assert.equal(refused.accepted, false)
  assert.equal(reconcileSurface.isPublishableOutcome('TurnFailed'), true)
})

test('WHAT[STRUCTURED-WORKFLOW-015] operator abort is a control-plane wake, never a business outcome', () => {
  // EXEC-020 / STRUCTURED-WORKFLOW-015: cancellation/interruption are control
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

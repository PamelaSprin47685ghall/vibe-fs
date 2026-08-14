// tests/unit/domain/reconcile-program.test.mjs — PR4 pure Reconcile Domain.
//
// Locks Evidence → Decision + publish seals. Command/Reply/Step AST and
// TraceInterpreter are deleted; workflow CE lives in Application/Reconciliation/Reconciler.fs
// and is covered by tests/unit/execution/reconcile-supervisor.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'
import { quiescencePermit, reconcileProgram, reconcileWake } from '../../../tests/unit/support/domain.mjs'

// ── pure classifiers ─────────────────────────────────────────────────────────

test('RECONCILE_PROGRAM_001: isTerminalOutcome classifies terminal vs provisional', async () => {
  assert.equal(typeof reconcileProgram.isTerminalOutcome, 'function')

  assert.equal(reconcileProgram.isTerminalOutcome('TurnCompleted'), true)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnAborted'), true)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnFailed'), true)

  assert.equal(reconcileProgram.isTerminalOutcome('TurnInProgress'), false)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnNeedsContinuation'), false)

  // HOST-004 Clean Break: TurnUnknown is not a TurnOutcome member, so it cannot
  // be classified as a non-terminal TurnOutcome. Structural demotion is the gate;
  // isTerminalOutcome('TurnUnknown') is type-unreachable after the cut.
  const mod = await import(new URL('../../../dist/Domain/ReconcileProgram.js', import.meta.url).pathname)
  const turnOutcomeCases = Object.create(mod.TurnOutcome.prototype).cases()
  assert.equal(
    turnOutcomeCases.includes('TurnUnknown'),
    false,
    `TurnUnknown must not be a TurnOutcome case; have: ${turnOutcomeCases.join(', ')}`,
  )
})

// ── decideStep: bounded causal reread → next pure decision ───────────────────

test('RECONCILE_PROGRAM_003: decideStep bounds causal rereads and stops on exhaustion', () => {
  assert.equal(typeof reconcileProgram.decideStep, 'function')
  assert.equal(typeof reconcileProgram.decisionName, 'function')

  const name = (remaining, evidence, wake = reconcileWake.retryWake()) =>
    reconcileProgram.decisionName(reconcileProgram.decideStep(wake, remaining, evidence))

  // Non-terminal evidence with budget remaining → Reread.
  assert.equal(name(3, reconcileProgram.evidence.snapshotError('transient')), 'Reread')
  assert.equal(name(3, reconcileProgram.evidence.noTurn()), 'Reread')
  assert.equal(name(3, reconcileProgram.evidence.provisional('TurnInProgress')), 'Reread')
  assert.equal(name(3, reconcileProgram.evidence.provisional('TurnNeedsContinuation')), 'Reread')
  assert.equal(name(3, reconcileProgram.evidence.unknown()), 'Reread')

  // Non-terminal with budget exhausted → fail closed per evidence kind: nothing
  // to act on (SnapshotError/NoTurn) keeps StopPass; Provisional publishes the
  // stop text; Unknown Publishes a stable observation only when the pass
  // carries idle evidence (HOST-004 rev.3 / rabbit §7) — retry/failure wakes
  // prove observation stability, not quiescence, and never hand off.
  assert.equal(name(0, reconcileProgram.evidence.snapshotError('transient')), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.noTurn()), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.provisional('TurnInProgress')), 'Publish')
  assert.equal(name(0, reconcileProgram.evidence.provisional('TurnNeedsContinuation')), 'Publish')
  assert.equal(name(0, reconcileProgram.evidence.unknown(), reconcileWake.retryWake()), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.unknown(), reconcileWake.failureWake()), 'StopPass')
  assert.equal(
    name(0, reconcileProgram.evidence.unknown(), reconcileWake.idleWake(quiescencePermit.create('ses-a', 1))),
    'Publish',
  )

  // Unknown clears continuation candidate; provisional keeps it.
  assert.equal(
    reconcileProgram.clearsContinuationCandidate(
      reconcileProgram.decideStep(reconcileWake.retryWake(), 3, reconcileProgram.evidence.unknown()),
    ),
    true,
  )
  assert.equal(
    reconcileProgram.clearsContinuationCandidate(
      reconcileProgram.decideStep(
        reconcileWake.retryWake(),
        3,
        reconcileProgram.evidence.provisional('TurnInProgress'),
      ),
    ),
    false,
  )

  // Terminal → Publish regardless of remaining and wake.
  assert.equal(name(0, reconcileProgram.evidence.terminal('TurnCompleted')), 'Publish')
  assert.equal(name(3, reconcileProgram.evidence.terminal('TurnAborted')), 'Publish')
  assert.equal(name(3, reconcileProgram.evidence.terminal('TurnFailed')), 'Publish')

  // HOST-004: an exhausted operator-abort wake must not Publish Unknown /
  // Provisional (business must not mint InteractionRepair / bare "#");
  // it StopPasses until the real TurnAborted terminal.
  assert.equal(name(0, reconcileProgram.evidence.unknown(), reconcileWake.abortWake()), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.provisional('TurnInProgress'), reconcileWake.abortWake()), 'StopPass')
  assert.equal(
    name(0, reconcileProgram.evidence.provisional('TurnNeedsContinuation'), reconcileWake.abortWake()),
    'StopPass',
  )
  // Bounded reread still allowed under abort so a genuine TurnAborted can settle.
  assert.equal(name(3, reconcileProgram.evidence.unknown(), reconcileWake.abortWake()), 'Reread')

  // Session cleared → StopPass.
  assert.equal(name(3, reconcileProgram.evidence.sessionCleared()), 'StopPass')

  // Reread decrements remaining (no time/backoff information carried).
  const rereadDecision = reconcileProgram.decideStep(
    reconcileWake.retryWake(),
    3,
    reconcileProgram.evidence.provisional('TurnInProgress'),
  )
  // decisionName is 'Reread'; verify clearsContinuationCandidate is false.
  assert.equal(reconcileProgram.clearsContinuationCandidate(rereadDecision), false)
})

// ── publishDecision: consumed (terminal) vs provisional maps ─────────────────

test('RECONCILE_PROGRAM_004: publishDecision gates already-published terminal and provisional', () => {
  assert.equal(typeof reconcileProgram.publishDecision, 'function')
  assert.equal(typeof reconcileProgram.consumeKey, 'function')

  const terminal = reconcileProgram.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnCompleted',
  })
  const provisional = reconcileProgram.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnInProgress',
  })
  const laterTerminal = reconcileProgram.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnCompleted',
  })

  const empty = reconcileProgram.publishMaps.empty()

  // First provisional publish allowed; marks provisional map.
  const firstProv = reconcileProgram.publishDecision(empty, provisional)
  assert.equal(firstProv.shouldPublish, true)
  assert.equal(firstProv.maps.provisionalHas(provisional), true)
  assert.equal(firstProv.maps.consumedHas(provisional), false)

  // Same provisional token again: sealed out.
  const secondProv = reconcileProgram.publishDecision(firstProv.maps, provisional)
  assert.equal(secondProv.shouldPublish, false)

  // Terminal with same run identity is not sealed by provisional map.
  const term = reconcileProgram.publishDecision(firstProv.maps, laterTerminal)
  assert.equal(term.shouldPublish, true)
  assert.equal(term.maps.consumedHas(laterTerminal), true)
  // Terminal mark clears provisional for the session.
  assert.equal(term.maps.provisionalHas(provisional), false)

  // Duplicate terminal token sealed.
  const dupTerm = reconcileProgram.publishDecision(term.maps, laterTerminal)
  assert.equal(dupTerm.shouldPublish, false)

  // clearProvisional removes provisional seal without touching consumed.
  const cleared = reconcileProgram.clearProvisional(firstProv.maps, 'ses-a')
  assert.equal(cleared.provisionalHas(provisional), false)
})

test('RECONCILE_PROGRAM_005: TurnUnknown never crosses the stable business-turn boundary', async () => {
  // HOST-004 / rabbit §7: TurnUnknown is type-unreachable for publishDecision
  // (not a TurnOutcome). IdleWake + exhausted Unknown → Publish (observation
  // handoff only); business repair lives in TurnWorkflow / InteractionRepair.
  const handoff = reconcileProgram.decideStep(
    reconcileWake.idleWake(quiescencePermit.create('ses-a', 1)),
    0,
    reconcileProgram.evidence.unknown(),
  )
  assert.equal(reconcileProgram.decisionName(handoff), 'Publish')

  // HOST-004 Clean Break: TurnUnknown must leave TurnOutcome entirely and live
  // only as reconciliation-private SnapshotObservation (type-unreachable for
  // publishDecision). Behavior above stays; this locks the structural demotion.
  const mod = await import(new URL('../../../dist/Domain/ReconcileProgram.js', import.meta.url).pathname)
  const turnOutcomeCases = Object.create(mod.TurnOutcome.prototype).cases()
  assert.equal(
    turnOutcomeCases.includes('TurnUnknown'),
    false,
    `TurnUnknown must not be a TurnOutcome case; have: ${turnOutcomeCases.join(', ')}`,
  )
  assert.equal(
    typeof mod.SnapshotObservation,
    'function',
    'SnapshotObservation must exist as the private observation carrier for TurnUnknown',
  )
  const observationCases = Object.create(mod.SnapshotObservation.prototype).cases()
  assert.deepEqual(
    observationCases,
    ['TurnUnknown'],
    `SnapshotObservation must carry TurnUnknown only; have: ${observationCases.join(', ')}`,
  )
})


test('RECONCILE_PROGRAM_006: Domain surface has no Command/Reply/Trace AST exports', async () => {
  const mod = await import(new URL('../../../dist/Domain/ReconcileProgram.js', import.meta.url).pathname)
  const names = Object.keys(mod).filter((n) => !n.endsWith('_$reflection'))
  assert.equal(
    names.some((n) => /Command|Reply|materializePass|interpretWith|TraceInterpreter|ProtocolMismatch/.test(n)),
    false,
    `second-runtime exports leaked: ${names.join(', ')}`,
  )
  assert.ok(names.some((n) => n.includes('decideStep')), `decideStep missing; ${names.join(', ')}`)
  assert.ok(names.some((n) => n.includes('publishDecision')), `publishDecision missing; ${names.join(', ')}`)
})

test('RECONCILE_PROGRAM_007: TurnUnknown is SnapshotObservation, not TurnOutcome', async () => {
  const mod = await import(new URL('../../../dist/Domain/ReconcileProgram.js', import.meta.url).pathname)
  const turnOutcomeCases = Object.create(mod.TurnOutcome.prototype).cases()

  assert.deepEqual(
    turnOutcomeCases,
    ['TurnInProgress', 'TurnNeedsContinuation', 'TurnCompleted', 'TurnAborted', 'TurnFailed'],
    `publishable TurnOutcome must exclude TurnUnknown; have: ${turnOutcomeCases.join(', ')}`,
  )
  assert.equal(typeof mod.SnapshotObservation, 'function')
  assert.deepEqual(Object.create(mod.SnapshotObservation.prototype).cases(), ['TurnUnknown'])

  // outcomeOf must refuse TurnUnknown as a TurnOutcome. Throwing is fine;
  // silently minting TurnUnknown or collapsing into TurnFailed is not.
  let minted
  let refused = false
  try {
    minted = mod.outcomeOf('TurnUnknown')
  } catch {
    refused = true
  }
  if (!refused) {
    assert.notEqual(
      minted.cases()[minted.tag],
      'TurnUnknown',
      'outcomeOf("TurnUnknown") must not return a TurnOutcome.TurnUnknown case',
    )
    assert.notEqual(
      minted.cases()[minted.tag],
      'TurnFailed',
      'outcomeOf("TurnUnknown") must not collapse Unknown into a false TurnFailed terminal',
    )
  }
})

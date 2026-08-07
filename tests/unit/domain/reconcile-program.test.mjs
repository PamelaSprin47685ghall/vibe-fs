// tests/unit/domain/reconcile-program.test.mjs — PR4 pure Reconcile Domain.
//
// Locks Evidence → Decision + publish seals. Command/Reply/Step AST and
// TraceInterpreter are deleted; workflow CE lives in Application/Reconciliation/Reconciler.fs
// and is covered by tests/unit/execution/reconcile-supervisor.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'
import { reconcileProgram } from '../support/domain.mjs'

// ── pure classifiers ─────────────────────────────────────────────────────────

test('RECONCILE_PROGRAM_001: isTerminalOutcome classifies terminal vs provisional', () => {
  assert.equal(typeof reconcileProgram.isTerminalOutcome, 'function')

  assert.equal(reconcileProgram.isTerminalOutcome('TurnCompleted'), true)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnAborted'), true)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnFailed'), true)

  assert.equal(reconcileProgram.isTerminalOutcome('TurnInProgress'), false)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnNeedsContinuation'), false)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnUnknown'), false)
})

// ── decideStep: bounded causal reread → next pure decision ───────────────────

test('RECONCILE_PROGRAM_003: decideStep bounds causal rereads and stops on exhaustion', () => {
  assert.equal(typeof reconcileProgram.decideStep, 'function')
  assert.equal(typeof reconcileProgram.decisionName, 'function')

  const name = (remaining, evidence) =>
    reconcileProgram.decisionName(reconcileProgram.decideStep(remaining, evidence))

  // Non-terminal evidence with budget remaining → Reread.
  assert.equal(name(3, reconcileProgram.evidence.snapshotError('transient')), 'Reread')
  assert.equal(name(3, reconcileProgram.evidence.noTurn()), 'Reread')
  assert.equal(name(3, reconcileProgram.evidence.provisional('TurnInProgress')), 'Reread')
  assert.equal(name(3, reconcileProgram.evidence.provisional('TurnNeedsContinuation')), 'Reread')
  assert.equal(name(3, reconcileProgram.evidence.unknown()), 'Reread')

  // Non-terminal with budget exhausted → StopPass (keep Dirty, wait next signal).
  assert.equal(name(0, reconcileProgram.evidence.snapshotError('transient')), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.noTurn()), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.provisional('TurnInProgress')), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.unknown()), 'StopPass')

  // Unknown clears continuation candidate; provisional keeps it.
  assert.equal(
    reconcileProgram.clearsContinuationCandidate(reconcileProgram.decideStep(3, reconcileProgram.evidence.unknown())),
    true,
  )
  assert.equal(
    reconcileProgram.clearsContinuationCandidate(
      reconcileProgram.decideStep(3, reconcileProgram.evidence.provisional('TurnInProgress')),
    ),
    false,
  )

  // Terminal → Publish regardless of remaining.
  assert.equal(name(0, reconcileProgram.evidence.terminal('TurnCompleted')), 'Publish')
  assert.equal(name(3, reconcileProgram.evidence.terminal('TurnAborted')), 'Publish')
  assert.equal(name(3, reconcileProgram.evidence.terminal('TurnFailed')), 'Publish')

  // Session cleared → StopPass.
  assert.equal(name(3, reconcileProgram.evidence.sessionCleared()), 'StopPass')

  // Reread decrements remaining (no time/backoff information carried).
  const rereadDecision = reconcileProgram.decideStep(3, reconcileProgram.evidence.provisional('TurnInProgress'))
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

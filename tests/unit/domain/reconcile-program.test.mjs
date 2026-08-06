// tests/unit/domain/reconcile-program.test.mjs — M3 Reconcile Domain pure extract.
//
// Locks Evidence → Decision → Program AST + Trace before Application Interpreter
// (M4) deletes Dirty/Running/cont/terminalFound from ReconcileSupervisor.
// Production Domain/ReconcileProgram is the missing surface this file forces RED.

import assert from 'node:assert/strict'
import test from 'node:test'
import { listItems, reconcileProgram } from '../support/domain.mjs'

// ── pure classifiers moved out of ReconcileSupervisor ────────────────────────

test('RECONCILE_PROGRAM_001: isTerminalOutcome classifies terminal vs provisional', () => {
  assert.equal(typeof reconcileProgram.isTerminalOutcome, 'function')

  assert.equal(reconcileProgram.isTerminalOutcome('TurnCompleted'), true)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnAborted'), true)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnFailed'), true)

  assert.equal(reconcileProgram.isTerminalOutcome('TurnInProgress'), false)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnNeedsContinuation'), false)
  assert.equal(reconcileProgram.isTerminalOutcome('TurnUnknown'), false)
})

test('RECONCILE_PROGRAM_002: pickDelay clamps to budget and holds last sequence entry', () => {
  assert.equal(typeof reconcileProgram.pickDelay, 'function')

  const sequence = [50, 100, 250, 500]
  assert.equal(reconcileProgram.pickDelay(sequence, 0, 1000), 50)
  assert.equal(reconcileProgram.pickDelay(sequence, 1, 1000), 100)
  // Past end of sequence → last entry (production stays at 5s after final step).
  assert.equal(reconcileProgram.pickDelay(sequence, 99, 1000), 500)
  // Budget clamp.
  assert.equal(reconcileProgram.pickDelay(sequence, 0, 30), 30)
  // Empty sequence or non-positive budget → 0 (no delay).
  assert.equal(reconcileProgram.pickDelay([], 0, 1000), 0)
  assert.equal(reconcileProgram.pickDelay(sequence, 0, 0), 0)
  assert.equal(reconcileProgram.pickDelay(sequence, 0, -1), 0)
})

// ── decideStep: one snapshot observation → next pure decision ────────────────

test('RECONCILE_PROGRAM_003: decideStep maps evidence to reread / publish / stop', () => {
  assert.equal(typeof reconcileProgram.decideStep, 'function')
  assert.equal(typeof reconcileProgram.decisionName, 'function')

  const name = (evidence) => reconcileProgram.decisionName(reconcileProgram.decideStep(evidence))

  // Snapshot Error: escalate delay, do not reset backoff; still reread.
  assert.equal(name(reconcileProgram.evidence.snapshotError('transient')), 'RereadWithBackoff')

  // No matching turn material yet.
  assert.equal(name(reconcileProgram.evidence.noTurn()), 'RereadWithBackoff')

  // Provisional incomplete: keep candidate, reread.
  assert.equal(
    name(reconcileProgram.evidence.provisional('TurnInProgress')),
    'RereadWithBackoff',
  )
  assert.equal(
    name(reconcileProgram.evidence.provisional('TurnNeedsContinuation')),
    'RereadWithBackoff',
  )

  // Explicit Unknown: clear provisional candidate semantics, still reread.
  assert.equal(name(reconcileProgram.evidence.unknown()), 'RereadWithBackoff')
  assert.equal(
    reconcileProgram.clearsContinuationCandidate(reconcileProgram.decideStep(reconcileProgram.evidence.unknown())),
    true,
  )
  assert.equal(
    reconcileProgram.clearsContinuationCandidate(
      reconcileProgram.decideStep(reconcileProgram.evidence.provisional('TurnInProgress')),
    ),
    false,
  )

  // Terminal: publish and stop materialization loop.
  assert.equal(name(reconcileProgram.evidence.terminal('TurnCompleted')), 'Publish')
  assert.equal(name(reconcileProgram.evidence.terminal('TurnAborted')), 'Publish')
  assert.equal(name(reconcileProgram.evidence.terminal('TurnFailed')), 'Publish')

  // Budget exhausted: publish continuation candidate when present; else stop.
  assert.equal(
    name(reconcileProgram.evidence.budgetExhausted({ hasCandidate: true })),
    'Publish',
  )
  assert.equal(
    name(reconcileProgram.evidence.budgetExhausted({ hasCandidate: false })),
    'StopPass',
  )

  // Session cleared mid-pass: no publish.
  assert.equal(name(reconcileProgram.evidence.sessionCleared()), 'StopPass')
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

// ── Program AST + Trace: materialize pass order ──────────────────────────────

test('RECONCILE_PROGRAM_005: materialize pass trace is ReadBinding → ReadSnapshot → Delay → ReadSnapshot → Publish → Observe', () => {
  assert.equal(typeof reconcileProgram.programs.materializePass, 'function')
  assert.equal(typeof reconcileProgram.interpretWith, 'function')

  // Scripted replies: binding present → first snapshot provisional → delay →
  // second snapshot terminal → publish → observe.
  const terminalTurn = reconcileProgram.turnFixture({
    session: 'ses-trace',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnCompleted',
  })
  let snapshotN = 0
  const reply = (step) => {
    const name = reconcileProgram.stepName(step)
    switch (name) {
      case 'ReadActiveBinding':
        return reconcileProgram.reply.bindingPresent()
      case 'ReadSnapshot': {
        snapshotN += 1
        if (snapshotN === 1) {
          return reconcileProgram.reply.snapshotOk(
            reconcileProgram.evidence.provisional('TurnInProgress'),
          )
        }
        return reconcileProgram.reply.snapshotOk(
          reconcileProgram.evidence.observedTerminal(terminalTurn),
        )
      }
      case 'Delay':
        return reconcileProgram.reply.delayDone()
      case 'PublishTurn':
        return reconcileProgram.reply.publishDone()
      case 'StorePublishMaps':
        return reconcileProgram.reply.publishMapsStored()
      case 'ObserveSnapshot':
        return reconcileProgram.reply.observeDone()
      default:
        return reconcileProgram.reply.unitOk()
    }
  }

  const program = reconcileProgram.programs.materializePass({
    session: 'ses-trace',
    backoffDelaysMs: [5, 5, 5],
    maxBudgetMs: 400,
  })
  const trace = listItems(reconcileProgram.interpretWith(reply, program))

  assert.ok(trace.includes('ReadActiveBinding'), `trace: ${trace.join(' → ')}`)
  assert.ok(
    trace.filter((s) => s === 'ReadSnapshot' || s.startsWith('ReadSnapshot')).length >= 2,
    `need ≥2 ReadSnapshot; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((s) => s === 'Delay' || s.startsWith('Delay')),
    `Delay missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((s) => s === 'PublishTurn' || s.startsWith('PublishTurn')),
    `PublishTurn missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((s) => s === 'ObserveSnapshot' || s.startsWith('ObserveSnapshot')),
    `ObserveSnapshot (HOST-006) missing; trace: ${trace.join(' → ')}`,
  )

  const bindingAt = trace.findIndex((s) => s === 'ReadActiveBinding' || s.startsWith('ReadActiveBinding'))
  const firstSnap = trace.findIndex((s) => s === 'ReadSnapshot' || s.startsWith('ReadSnapshot'))
  const delayAt = trace.findIndex((s) => s === 'Delay' || s.startsWith('Delay'))
  const secondSnap = trace.findIndex(
    (s, i) => i > firstSnap && (s === 'ReadSnapshot' || s.startsWith('ReadSnapshot')),
  )
  const publishAt = trace.findIndex((s) => s === 'PublishTurn' || s.startsWith('PublishTurn'))
  const observeAt = trace.findIndex((s) => s === 'ObserveSnapshot' || s.startsWith('ObserveSnapshot'))

  assert.ok(bindingAt >= 0 && bindingAt < firstSnap, 'ReadActiveBinding before first ReadSnapshot')
  assert.ok(firstSnap < delayAt, 'first ReadSnapshot before Delay')
  assert.ok(delayAt < secondSnap, 'Delay before second ReadSnapshot')
  assert.ok(secondSnap < publishAt, 'second ReadSnapshot before PublishTurn')
  assert.ok(publishAt < observeAt, 'PublishTurn before ObserveSnapshot (HOST-006 order)')
})

test('RECONCILE_PROGRAM_006: SnapshotError does not reset backoff index in decideBackoff', () => {
  assert.equal(typeof reconcileProgram.nextBackoffIndex, 'function')

  // Ok snapshot → reset to 0 after a successful classify path.
  assert.equal(reconcileProgram.nextBackoffIndex({ previous: 3, snapshotOk: true }), 0)
  // Error snapshot → escalate (previous + 1), never reset.
  assert.equal(reconcileProgram.nextBackoffIndex({ previous: 3, snapshotOk: false }), 4)
  assert.equal(reconcileProgram.nextBackoffIndex({ previous: 0, snapshotOk: false }), 1)
})

test('RECONCILE_PROGRAM_007: non-positive delay configuration terminates at ObserveSnapshot', () => {
  const reply = (step) => {
    switch (reconcileProgram.stepName(step)) {
      case 'ReadActiveBinding':
        return reconcileProgram.reply.bindingPresent()
      case 'ReadSnapshot':
        return reconcileProgram.reply.snapshotOk(
          reconcileProgram.evidence.provisional('TurnInProgress'),
        )
      case 'ObserveSnapshot':
        return reconcileProgram.reply.observeDone()
      default:
        return reconcileProgram.reply.unitOk()
    }
  }

  const program = reconcileProgram.programs.materializePass({
    session: 'ses-zero-delay',
    backoffDelaysMs: [0],
    maxBudgetMs: 400,
  })
  const trace = listItems(reconcileProgram.interpretWith(reply, program))

  assert.deepEqual(trace, ['ReadActiveBinding', 'ReadSnapshot', 'ObserveSnapshot'])
})

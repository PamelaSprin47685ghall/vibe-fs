import assert from 'node:assert/strict'
import test from 'node:test'
import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'

const idleWake = ReconcileSurface.idleWake('s1', 1n)

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_idle_early_then_second_signal_completes', () => {
  // First pass: three Provisional (incomplete) snapshots exhaust the budget.
  // Under IdleWake, Provisional exhausted → Publish (not StopPass).
  // But with rereadsRemaining=3, the first two are Reread, the third is Publish.
  const first1 = ReconcileSurface.decideStep(idleWake, 3, ReconcileSurface.evidenceProvisional('TurnInProgress'))
  assert.equal(ReconcileSurface.decisionName(first1), 'Reread')
  const first2 = ReconcileSurface.decideStep(idleWake, 2, ReconcileSurface.evidenceProvisional('TurnInProgress'))
  assert.equal(ReconcileSurface.decisionName(first2), 'Reread')
  const first3 = ReconcileSurface.decideStep(idleWake, 1, ReconcileSurface.evidenceProvisional('TurnInProgress'))
  assert.equal(ReconcileSurface.decisionName(first3), 'Publish')

  // Second pass: a new IdleWake with Terminal evidence → Publish immediately.
  const second = ReconcileSurface.decideStep(idleWake, 3, ReconcileSurface.evidenceTerminal('TurnCompleted'))
  assert.equal(ReconcileSurface.decisionName(second), 'Publish')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_consecutive_errors_retry_until_ok_terminal', () => {
  // SnapshotError goes through bounded causal reread: with budget > 1 it
  // returns Reread (the snapshot is unstable, retry the read); only when
  // exhausted (budget = 1) does it StopPass. The next signal with Terminal
  // evidence → Publish.
  const errorReread = ReconcileSurface.decideStep(idleWake, 3, ReconcileSurface.evidenceSnapshotError('e1'))
  assert.equal(ReconcileSurface.decisionName(errorReread), 'Reread')
  const errorExhausted = ReconcileSurface.decideStep(idleWake, 1, ReconcileSurface.evidenceSnapshotError('e2'))
  assert.equal(ReconcileSurface.decisionName(errorExhausted), 'StopPass')
  // A subsequent signal with Terminal evidence publishes.
  const terminal = ReconcileSurface.decideStep(idleWake, 3, ReconcileSurface.evidenceTerminal('TurnCompleted'))
  assert.equal(ReconcileSurface.decisionName(terminal), 'Publish')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_persistent_errors_stop_pass_bounded', () => {
  // SnapshotError uses bounded causal reread: with budget > 1 it Rereads
  // (the snapshot is unstable); only when exhausted (budget = 1) does it
  // StopPass. The pass is bounded: it never loops infinitely.
  const reread = ReconcileSurface.decideStep(idleWake, 3, ReconcileSurface.evidenceSnapshotError('e1'))
  assert.equal(ReconcileSurface.decisionName(reread), 'Reread')
  // Budget decremented: 2 → still Reread.
  const reread2 = ReconcileSurface.decideStep(idleWake, 2, ReconcileSurface.evidenceSnapshotError('e2'))
  assert.equal(ReconcileSurface.decisionName(reread2), 'Reread')
  // Exhausted (budget = 1) → StopPass, not Reread or Publish.
  const exhausted = ReconcileSurface.decideStep(idleWake, 1, ReconcileSurface.evidenceSnapshotError('e4'))
  assert.equal(ReconcileSurface.decisionName(exhausted), 'StopPass')
})

// ── Mutation sensitivity ─────────────────────────────────────────────────

test('WHAT[HOST-BOUNDARY-005] mutation_canary_terminal_evidence_publishes_under_idle_wake', () => {
  // If someone changes Terminal evidence to StopPass, this canary fails.
  const decision = ReconcileSurface.decideStep(idleWake, 3, ReconcileSurface.evidenceTerminal('TurnCompleted'))
  assert.equal(ReconcileSurface.decisionName(decision), 'Publish',
    'mutation guard: Terminal evidence under IdleWake must Publish')
})

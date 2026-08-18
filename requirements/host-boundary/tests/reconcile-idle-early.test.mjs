import assert from 'node:assert/strict'
import test from 'node:test'
import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'

const idleWake = ReconcileSurface.idleWake('s1', 1n)

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_projection_edge_drives_exactly_one_additional_idle_read', async () => {
  const result = await ReconcileSurface.idleProjectionEdgeScenario()

  assert.deepEqual(result, {
    snapshotReads: 2,
    providerRun: 'projection-edge-current-run',
    outcome: 'TurnCompleted',
    hasQuiescence: true,
  })
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_projection_edge_delivers_the_next_failed_provider_run_to_AABB', async () => {
  const result = await ReconcileSurface.failureProjectionEdgeScenario()

  assert.deepEqual(result, {
    snapshotReads: 2,
    providerRun: 'projection-edge-current-run',
    outcome: 'TurnFailed',
    hasQuiescence: false,
  })
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_has_no_counter_driven_snapshot_polling', () => {
  for (const remaining of [8, 4, 2, 1]) {
    const error = ReconcileSurface.decideStep(
      idleWake,
      remaining,
      ReconcileSurface.evidenceSnapshotError('projection unavailable'),
    )
    assert.equal(ReconcileSurface.decisionName(error), 'StopPass')

    const noTurn = ReconcileSurface.decideStep(
      ReconcileSurface.failureWake(),
      remaining,
      ReconcileSurface.evidenceNoTurn(),
    )
    assert.equal(ReconcileSurface.decisionName(noTurn), 'StopPass')
  }
})

test('WHAT[HOST-BOUNDARY-005] EXEC_only_idle_can_publish_a_nonterminal_current_assistant', () => {
  const idle = ReconcileSurface.decideStep(
    idleWake,
    99,
    ReconcileSurface.evidenceProvisional('TurnInProgress'),
  )
  assert.equal(ReconcileSurface.decisionName(idle), 'Publish')

  for (const wake of [
    ReconcileSurface.retryWake(),
    ReconcileSurface.failureWake(),
    ReconcileSurface.abortWake(),
  ]) {
    const decision = ReconcileSurface.decideStep(
      wake,
      99,
      ReconcileSurface.evidenceProvisional('TurnInProgress'),
    )
    assert.equal(ReconcileSurface.decisionName(decision), 'StopPass')
  }
})

test('WHAT[HOST-BOUNDARY-005] mutation_canary_terminal_evidence_still_publishes', () => {
  const decision = ReconcileSurface.decideStep(
    ReconcileSurface.failureWake(),
    99,
    ReconcileSurface.evidenceTerminal('TurnFailed'),
  )
  assert.equal(ReconcileSurface.decisionName(decision), 'Publish')
})

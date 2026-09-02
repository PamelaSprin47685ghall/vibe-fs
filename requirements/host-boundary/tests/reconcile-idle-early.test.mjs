import assert from 'node:assert/strict'
import test from 'node:test'
import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'

const idleWake = ReconcileSurface.idleWake('s1', 1n)

test('WHAT[HOST-BOUNDARY-005] exact failure wake survives same-physical idle admission', () => {
  const failure = ReconcileSurface.failureWakeFor('msg-current')

  assert.equal(ReconcileSurface.mergeWakeKind('msg-current', failure, idleWake), 'FailureWake')
  assert.equal(
    ReconcileSurface.mergeWakeKind('', failure, ReconcileSurface.failureWake()),
    'FailureWake',
    'a coarse failure without identity cannot erase an exact failed physical',
  )
  assert.equal(
    ReconcileSurface.mergeWakeKind('', failure, idleWake),
    'FailureWake',
    'missing process-local binding is not evidence that the exact failed physical was superseded',
  )
  assert.equal(
    ReconcileSurface.mergeWakeKind('msg-next', failure, idleWake),
    'IdleWake',
    'a new physical user message must release the old failure wake',
  )
  assert.equal(ReconcileSurface.mergeWakeKind('msg-current', failure, ReconcileSurface.abortWake()), 'AbortWake')
})

test('WHAT[HOST-BOUNDARY-005] coarse failure without physical binding cannot publish a terminal turn', async () => {
  const result = await ReconcileSurface.unboundFailureScenario()
  assert.equal(result.snapshotReads, 0)
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_projection_edge_drives_exactly_one_additional_idle_read', async () => {
  const result = await ReconcileSurface.idleProjectionEdgeScenario()

  assert.deepEqual(result, {
    snapshotReads: 2,
    providerRun: 'projection-edge-current-run',
    outcome: 'TurnCompleted',
    hasQuiescence: true,
  })
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_without_projection_edge_reads_once_and_exposes_no_counter', async () => {
  const result = await ReconcileSurface.idleProvisionalWithoutProjectionEdgeScenario()
  assert.deepEqual(result, {
    snapshotReads: 1,
    observed: true,
    outcome: 'TurnInProgress',
  })

  const decision = ReconcileSurface.decideStep(
    ReconcileSurface.retryWake(),
    ReconcileSurface.evidenceProvisional('TurnInProgress'),
  )
  assert.deepEqual(decision, { name: 'StopPass' })
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

test('WHAT[HOST-BOUNDARY-005] EXEC_provider_failure_with_exact_current_assistant_does_not_wait_for_terminal_projection', async () => {
  const result = await ReconcileSurface.failureWitnessCurrentAssistantScenario()

  assert.deepEqual(result, {
    snapshotReads: 1,
    observed: true,
    providerRun: 'failure-witness-current-run',
    outcome: 'TurnFailed',
    reason: 'Bad Request: input_invalid',
    hasQuiescence: false,
  })
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_snapshot_error_and_no_turn_stop_current_pass', () => {
  const error = ReconcileSurface.decideStep(
    idleWake,
    ReconcileSurface.evidenceSnapshotError('projection unavailable'),
  )
  assert.equal(ReconcileSurface.decisionName(error), 'StopPass')

  const noTurn = ReconcileSurface.decideStep(
    ReconcileSurface.failureWake(),
    ReconcileSurface.evidenceNoTurn(),
  )
  assert.equal(ReconcileSurface.decisionName(noTurn), 'StopPass')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_only_idle_can_publish_a_nonterminal_current_assistant', () => {
  const idle = ReconcileSurface.decideStep(
    idleWake,
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
      ReconcileSurface.evidenceProvisional('TurnInProgress'),
    )
    assert.equal(ReconcileSurface.decisionName(decision), 'StopPass')
  }
})

test('WHAT[HOST-BOUNDARY-005] mutation_canary_terminal_evidence_still_publishes', () => {
  const decision = ReconcileSurface.decideStep(
    ReconcileSurface.failureWake(),
    ReconcileSurface.evidenceTerminal('TurnFailed'),
  )
  assert.equal(ReconcileSurface.decisionName(decision), 'Publish')
})

test('WHAT[HOST-BOUNDARY-005] terminal provider failure publishes only with matching typed physical witness', () => {
  const terminal = ReconcileSurface.evidenceTerminalFor('failed-physical', 'TurnFailed')

  assert.equal(
    ReconcileSurface.decisionName(
      ReconcileSurface.decideStep(ReconcileSurface.failureWakeFor('failed-physical'), terminal),
    ),
    'Publish',
  )
  assert.equal(
    ReconcileSurface.decisionName(
      ReconcileSurface.decideStep(ReconcileSurface.failureWakeFor('other-physical'), terminal),
    ),
    'StopPass',
  )
  assert.equal(
    ReconcileSurface.decisionName(ReconcileSurface.decideStep(ReconcileSurface.retryWake(), terminal)),
    'StopPass',
  )
})

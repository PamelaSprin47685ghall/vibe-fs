import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[CHGINT-010] review_rounds_run_outside_the_publish_gate', async () => {
  const observation = await change.observeProgramGateScope('fresh')

  assert.deepEqual(observation, {
    verdict: 'Published',
    reviewGateHeld: [false, false],
    repairGateHeld: [],
    ffMergeGateHeld: [true],
    gateAcquireCount: 1,
    gateReleaseCount: 1,
    gateHeldAfterRun: false,
  })
})

test('WHAT[CHGINT-010] conflict_repair_runs_outside_the_publish_gate', async () => {
  const observation = await change.observeProgramGateScope('conflict-recovery')

  assert.deepEqual(observation, {
    verdict: 'Published',
    reviewGateHeld: [false],
    repairGateHeld: [false],
    ffMergeGateHeld: [true],
    gateAcquireCount: 1,
    gateReleaseCount: 1,
    gateHeldAfterRun: false,
  })
})

test('WHAT[CHGINT-013] a_CAS_race_discards_the_old_witness_and_runs_a_fresh_round', async () => {
  const observation = await change.observeMovedTargetRecovery()

  assert.deepEqual(observation, {
    verdict: 'Published',
    postRebaseBarrierIds: ['chgint-013:post-rebase:0', 'chgint-013:post-rebase:1'],
    rebasedTargetSnapshots: ['target-head-1', 'target-head-2'],
    rebaseCount: 2,
    ffExpectedHeads: ['target-head-2'],
    gateAcquireCount: 2,
    gateReleaseCount: 2,
    gateHeldAfterRun: false,
  })
})

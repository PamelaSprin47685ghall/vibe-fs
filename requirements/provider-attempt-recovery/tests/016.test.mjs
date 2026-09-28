import assert from 'node:assert/strict'
import test from 'node:test'
import * as planner from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import * as turns from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'

test('WHAT[provider-attempt-recovery-016] actual RequestKind algebra clears only business-main success', () => {
  for (const [role, kind, clears] of [
    ['engineer', 'work-main', true], ['blogger', 'blogger-main', true],
    ['blogger', 'blogger-squash', false], ['engineer', 'interaction-repair', false],
    ['engineer', 'strength-replica', false],
  ]) {
    const plan = planner.plan({ role, kind })
    assert.equal(plan.ok, true)
    assert.equal(plan.requestKind, kind)
    assert.equal(plan.clearsFailureCountOnSuccess, clears, kind)
  }
})

test('WHAT[provider-attempt-recovery-016] tool-calls finish is a successful provider attempt while the turn continues', () => {
  assert.deepEqual(turns.classifyOutcome(true, 'tool-calls', null, []), { kind: 'TurnInProgress', reason: null })
})

test.todo('WHAT[provider-attempt-recovery-016] real ordinary and Blogger completion use durable kind evidence and write SuccessRecorded only for eligible successes (GAP-139)')

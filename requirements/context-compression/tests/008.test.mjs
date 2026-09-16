import assert from 'node:assert/strict'
import test from 'node:test'
import * as retryPolicy from '../../../dist/Context/Companion/RetryPolicySurface.js'
import * as attemptPlan from '../../../dist/Context/Companion/AttemptPlanProbeEligibilitySurface.js'

test('WHAT[CONTEXT-COMPRESSION-008] only work_main requests may carry prefix probe', () => {
  assert.equal(retryPolicy.requestKindMayCarryProbe('WorkMain'), true)
  assert.equal(retryPolicy.requestKindMayCarryProbe('BloggerMain'), false)
  assert.equal(retryPolicy.requestKindMayCarryProbe('BloggerSquash'), false)
})

test('WHAT[CONTEXT-COMPRESSION-008] attempt_plan_allows_probe_only_on_work_main', () => {
  assert.equal(attemptPlan.probeAllowedForKind('WorkMain'), true)
  assert.equal(attemptPlan.probeAllowedForKind('BloggerMain'), false)
})

test('WHAT[CONTEXT-COMPRESSION-008] attempt_plan_rejects_probe_when_forbidden', () => {
  assert.equal(attemptPlan.probeAllowed({ allowProbe: false }), false)
})

test('WHAT[CONTEXT-COMPRESSION-008] attempt_plan_freezes_probe_on_creation', () => {
  assert.ok(attemptPlan.freezePlan)
})

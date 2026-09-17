import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as attemptPurpose from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import * as reconcile from '../../../dist/Composition/Turn/ReconcileSurface.js'

const planner = compression.attemptPlanner

const { budget, providerFailureProjection } = failureOwner

const TOOL_CAPABILITIES = [
  'BashHoneypot',
  'Edit',
  'Fetch',
  'Fission',
  'Glob',
  'Grep',
  'Move',
  'Read',
  'Remove',
  'Write',
]

test('WHAT[PAR-016] success_accounting_requires_proven_request_kind', () => {
  // Only WorkMain-family attempts may carry a probe; maintenance and repair
  // kinds never do — so their success can never promote and never clear the
  // budget through the probe path. The kind label itself decides.
  assert.equal(attemptPurpose.ordinaryRequestPurpose('InteractionRepair', ''), 'interaction-repair')
  assert.equal(attemptPurpose.ordinaryRequestPurpose('HumanRoot', 'main'), 'blogger-main')
  assert.equal(attemptPurpose.ordinaryRequestPurpose('HumanRoot', 'squash'), 'blogger-squash')
  assert.equal(attemptPurpose.ordinaryRequestPurpose('HumanRoot', ''), 'work-main')

  // Clearing rule at the budget: only a business-main success resets; the
  // projection exposes recordSuccess for exactly that case while a squash or
  // repair success simply never calls it.
  const advanced = providerFailureProjection.applyFailure(
    budget.attemptIdentity('ses_a', 'run_L', 'msg_u1', 'run_1'),
    1,
    providerFailureProjection.forAuthority('run_L', 'msg_u1'),
  ).value
  assert.equal(providerFailureProjection.read(advanced).failures, 1)
  assert.equal(providerFailureProjection.read(providerFailureProjection.recordSuccess(advanced)).failures, 0)
})

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
  'Sphinx',
  'Write',
]

test('WHAT[provider-attempt-recovery-015] independent_sessions_keep_independent_budgets', () => {
  const a0 = providerFailureProjection.forAuthority('run_L', 'msg_u1')
  const b0 = providerFailureProjection.forAuthority('run_M', 'msg_u2')

  const idA = budget.attemptIdentity('ses_a', 'run_L', 'msg_u1', 'run_1')
  const idB = budget.attemptIdentity('ses_b', 'run_M', 'msg_u2', 'run_9')
  const a1 = providerFailureProjection.applyFailure(idA, 1, a0)
  const b1 = providerFailureProjection.applyFailure(idB, 1, b0)
  assert.equal(a1.ok, true)
  assert.equal(b1.ok, true)

  assert.deepEqual(providerFailureProjection.read(a1.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
  assert.deepEqual(providerFailureProjection.read(b1.value), {
    logicalRun: 'run_M',
    authorityRoot: 'msg_u2',
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
})

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

test('WHAT[PAR-006] retry_keeps_fixed_participant_with_decoupled_model_routing', () => {
  // Fixed participant: the same role plans twice with an identical identity,
  // system prompt and tool set — no retry ever switches the participant.
  const first = planner.plan({ role: 'coder', kind: 'work-main' })
  const second = planner.plan({ role: 'coder', kind: 'work-main' })
  assert.deepEqual(first.participantIdentity, second.participantIdentity)
  assert.equal(first.systemPromptId, second.systemPromptId)
  assert.deepEqual(first.toolCapabilities, second.toolCapabilities)

  // Decoupled model routing: the plan names the participant, never a
  // model/provider target — target selection is a scheduler backend choice
  // for the same participant, not a domain identity change.
  for (const key of ['model', 'provider', 'modelTarget', 'providerTarget', 'endpoint', 'scheduler']) {
    assert.equal(key in first, false, `plan must not carry ${key}`)
  }

  // At the consecutive-failure budget the run is exhausted at once: the
  // budget verdict flips exactly at the limit, so no over-budget request
  // is ever issued.
  assert.equal(budget.verdict(budget.defaultBudget, { failures: budget.defaultBudget - 1 }), 'MayRetry')
  assert.equal(budget.verdict(budget.defaultBudget, { failures: budget.defaultBudget }), 'Exhausted')
})

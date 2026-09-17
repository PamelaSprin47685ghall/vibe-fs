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

test('WHAT[PAR-013] participant_identity_role_and_persona_remain_immutable_across_retries', () => {
  const first = planner.plan({ role: 'engineer', kind: 'work-main' })
  const second = planner.plan({ role: 'engineer', kind: 'work-main' })

  assert.equal(first.participant, 'engineer')
  assert.equal(second.participant, 'engineer')
  assert.deepEqual(first.participantIdentity, second.participantIdentity)
  assert.equal(first.systemPromptId, second.systemPromptId)
  assert.deepEqual(first.toolCapabilities, second.toolCapabilities)

  assert.deepEqual(first.participantIdentity, {
    selectedAgent: 'engineer',
    canonicalRole: 'engineer',
    role: 'engineer',
    participant: 'engineer',
    selectedTier: 'deep',
    persona: 'Engineer',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  })
})

test('WHAT[PAR-013] plans_derive_system_prompt_and_tools_from_the_fixed_role', () => {
  const planned = attemptPurpose.plan({ role: 'engineer', kind: 'work-main' })

  assert.equal(planned.ok, true)
  assert.equal(planned.systemPromptId, planned.participantIdentity.canonicalRole)
  assert.deepEqual(planned.toolCapabilities, TOOL_CAPABILITIES)
})

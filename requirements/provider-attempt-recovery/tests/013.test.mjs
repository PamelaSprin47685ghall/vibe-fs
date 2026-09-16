// retry-policy.test.mjs — PAR-006/008/010/011/013/015/016/018.
//
// Retry policy and attempt plans in the one-retry world. Every assertion
// drives production: AttemptPlanner via PlannerSurface/CompressionSurface,
// BloggerRetryPolicy via CompressionSurface.nextBloggerRequest, TerminalValidity
// via CompressionSurface.terminalValidity, and the failure budget via
// Fallback/ProviderFailureSurface.js. No copied policy, no source-text reads.

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
  'Inspect',
  'Move',
  'Read',
  'Remove',
  'Write',
]

test('WHAT[PAR-013] participant_identity_role_and_persona_remain_immutable_across_retries', () => {
  const first = planner.plan({ role: 'coder', kind: 'work-main' })
  const second = planner.plan({ role: 'coder', kind: 'work-main' })

  assert.equal(first.participant, 'coder')
  assert.equal(second.participant, 'coder')
  assert.deepEqual(first.participantIdentity, second.participantIdentity)
  assert.equal(first.systemPromptId, second.systemPromptId)
  assert.deepEqual(first.toolCapabilities, second.toolCapabilities)

  assert.deepEqual(first.participantIdentity, {
    selectedAgent: 'coder',
    canonicalRole: 'coder',
    role: 'coder',
    participant: 'coder',
    selectedTier: 'deep',
    persona: 'Coder',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  })
})

test('WHAT[PAR-013] plans_derive_system_prompt_and_tools_from_the_fixed_role', () => {
  const planned = attemptPurpose.plan({ role: 'coder', kind: 'work-main' })

  assert.equal(planned.ok, true)
  assert.equal(planned.systemPromptId, planned.participantIdentity.canonicalRole)
  assert.deepEqual(planned.toolCapabilities, TOOL_CAPABILITIES)
})

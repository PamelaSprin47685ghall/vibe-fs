import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'

test('WHAT[DISPATCH-PROTOCOL-010] DP_010_authority_root_profile_cannot_express_a_model', () => {
  const profile = dispatch.createAuthorityRootProfile({
    participant: 'agent-1',
    role: 'coder',
  })
  assert.equal(profile.model, undefined)
})

test('WHAT[DISPATCH-PROTOCOL-010] send_format_preserves_participant_and_sets_model_null', () => {
  const formatted = dispatch.formatHostSendOptions({
    participant: 'p-1',
    role: 'manager',
  })
  assert.equal(formatted.agent, 'p-1')
  assert.equal(formatted.model, null)
})

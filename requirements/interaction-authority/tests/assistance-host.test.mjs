// INTERACTION-AUTHORITY proof — assistance remains same-session continuation authority.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`
const root = authority.createAuthorityRoot(hash, 'rt_assistance', 'ses_owner', 'HumanRoot', 'msg_root', 'fast-coder').value
const profile = (value) => ({
  session: value.session,
  logicalRun: value.logicalRun,
  authorityRoot: value.authorityRoot,
  authorityKind: value.authorityKind,
  selectedAgent: value.selectedAgent,
  peerAgent: value.peerAgent,
})

test('WHAT[INTERACTION-AUTHORITY-012] AGENT_031_needhelp_is_same_session_deep_peer_continuation', () => {
  let state = authority.registerAuthority(root, authority.empty)
  const claim = authority.claimContinuation(
    'pk-help',
    'ses_owner',
    'NeedHelpEscalation',
    root,
    'deep-coder',
    'needhelp|run-1',
  )
  state = authority.registerClaim(claim, state)
  state = authority.acceptClaim('pk-help', 'msg-help', state)

  assert.deepEqual(profile(state.activeLogicalRun), profile(root))
  assert.equal(state.acceptedContinuations.length, 1)
  assert.equal(state.acceptedContinuations[0].kind, 'NeedHelpEscalation')
  assert.equal(state.acceptedContinuations.some((item) => item.kind === 'ProviderRetryAttempt'), false)
  assert.equal(state.activeLogicalRun.selectedAgent, 'fast-coder')
  assert.equal(claim.effectiveAgent, 'deep-coder')
})

test('WHAT[INTERACTION-AUTHORITY-013] AGENT_031_deep_binding_uses_consultation_continuation_without_new_root', () => {
  const state = authority.registerAuthority(root, authority.empty)
  const claim = authority.claimContinuation('pk-advice', 'ses_owner', 'NeedHelpAdvice', root, 'deep-coder', 'advice|run-2')
  const after = authority.registerClaim(claim, state)
  assert.equal(claim.origin, 'Continuation')
  assert.equal(claim.logicalRun, root.logicalRun)
  assert.equal(claim.authorityRoot, root.authorityRoot)
  assert.deepEqual(profile(after.activeLogicalRun), profile(root))

})
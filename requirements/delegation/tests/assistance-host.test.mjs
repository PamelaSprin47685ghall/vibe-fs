import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const root = authority.createAuthorityRoot(
  (value) => `H(${value})`,
  'rt_assistance-delegation',
  'ses_owner',
  'HumanRoot',
  'msg_root',
  'fast-coder',
).value

test('WHAT[DELEG-018] ASSISTANCE_HOST_needhelp_escalation_keeps_the_same_authority_root', () => {
  const state = authority.registerAuthority(root, authority.empty)
  const claim = authority.claimContinuation(
    'pk-help',
    'ses_owner',
    'NeedHelpEscalation',
    root,
    'deep-coder',
    'needhelp|run-1',
  )
  const after = authority.acceptClaim('pk-help', 'msg-help', authority.registerClaim(claim, state))

  assert.equal(claim.origin, 'Continuation')
  assert.equal(claim.authorityRoot, root.authorityRoot)
  assert.equal(after.activeLogicalRun.authorityRoot, root.authorityRoot)
  assert.equal(after.activeLogicalRun.logicalRun, root.logicalRun)
  assert.equal(after.acceptedContinuations.length, 1)
})

test('WHAT[DELEG-018] ASSISTANCE_HOST_needhelp_advice_is_not_a_provider_retry', () => {
  const state = authority.registerAuthority(root, authority.empty)
  const claim = authority.claimContinuation(
    'pk-advice',
    'ses_owner',
    'NeedHelpAdvice',
    root,
    'deep-coder',
    'advice|run-2',
  )
  const after = authority.registerClaim(claim, state)

  assert.equal(claim.origin, 'Continuation')
  assert.equal(after.activeLogicalRun.authorityRoot, root.authorityRoot)
  assert.equal(after.acceptedContinuations.length, 0)
  assert.equal(after.acceptedContinuations.some((item) => item.kind === 'ProviderRetryAttempt'), false)
})

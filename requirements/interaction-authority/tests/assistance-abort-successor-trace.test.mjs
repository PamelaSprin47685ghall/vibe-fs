// INTERACTION-AUTHORITY proof — abort→idle→successor complete causal chain trace freeze.
// Phase 0 trace B: no existing test chains all three stages end-to-end.

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

const hash = (value) => `H(${value})`
const root = authority.createAuthorityRoot(hash, 'rt_trace', 'ses_trace', 'HumanRoot', 'msg_trace', 'fast-coder').value
const profile = (value) => ({
  session: value.session,
  logicalRun: value.logicalRun,
  authorityRoot: value.authorityRoot,
  authorityKind: value.authorityKind,
  selectedAgent: value.selectedAgent,
  peerAgent: value.peerAgent,
})

// 1. abort only arms, does not consume
test('WHAT[INTERACTION-AUTHORITY-012] trace_B_1_abort_only_arms_does_not_consume', () => {
  const sensor = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/NeedHelpSensor.fs')
  const host = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs')

  // Sensor: RequestAbort calls TryArm, not TryConsumeAssistanceClaim
  const requestAbort = sensor.match(/member private this\.RequestAbort([\s\S]*?)member/)
  assert.ok(requestAbort, 'RequestAbort must be inspectable')
  assert.match(requestAbort[1], /this\.TryArm/)
  assert.doesNotMatch(requestAbort[1], /TryConsumeAssistanceClaim/)

  // Host: handleOwnerSideTurn calls TryObserveAssistanceClaim, not TryConsumeAssistanceClaim
  const ownerSide = host.match(/let handleOwnerSideTurn([\s\S]*?)let activeConsultationAbort/)
  assert.ok(ownerSide, 'owner-side assistance routing must be inspectable')
  assert.match(ownerSide[1], /TryObserveAssistanceClaim/)
  assert.doesNotMatch(ownerSide[1], /TryConsumeAssistanceClaim/)
})

// 2. TurnAborted without fresh SessionIdle does not send
test('WHAT[INTERACTION-AUTHORITY-012] trace_B_2_no_fresh_idle_no_send', () => {
  const host = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs')

  const fence = host.match(/let withFreshAssistanceQuiescence([\s\S]*?)let escalateFastOwnerRequest/)
  assert.ok(fence, 'fresh-idle assistance fence must be inspectable')
  assert.match(fence[1], /match context\.Quiescence with/)
  assert.match(fence[1], /\| None -> Task\.FromResult AssistanceTurnDisposition\.Handled/)

  // None branch must not call continueAfterIdle
  const noneBranch = fence[1].slice(fence[1].indexOf('| None ->'), fence[1].indexOf('| Some _ ->'))
  assert.doesNotMatch(noneBranch, /continueAfterIdle/, 'None quiescence must not call continueAfterIdle')

  // Some branch must consume then continue
  assert.match(fence[1], /\| Some _ ->[\s\S]*?TryConsumeAssistanceClaim[\s\S]*?continueAfterIdle/)
})

// 3. fresh SessionIdle is the transport fence
test('WHAT[INTERACTION-AUTHORITY-012] trace_B_3_fresh_idle_is_transport_fence', () => {
  const host = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs')

  // TryConsumeAssistanceClaim does not appear in handleOwnerSideTurn
  const ownerSide = host.match(/let handleOwnerSideTurn([\s\S]*?)let activeConsultationAbort/)
  assert.ok(ownerSide)
  assert.doesNotMatch(ownerSide[1], /TryConsumeAssistanceClaim/)

  // TryConsumeAssistanceClaim appears inside withFreshAssistanceQuiescence
  const fence = host.match(/let withFreshAssistanceQuiescence([\s\S]*?)let escalateFastOwnerRequest/)
  assert.ok(fence)
  assert.match(fence[1], /sensor\.TryConsumeAssistanceClaim/)

  // Both successor paths go through withFreshAssistanceQuiescence
  const escalate = host.match(/let escalateFastOwnerRequest([\s\S]*?)let beginDeepOwnerConsultation/)
  assert.ok(escalate, 'escalate path must be inspectable')
  assert.match(escalate[1], /withFreshAssistanceQuiescence/)

  const consult = host.match(/let beginDeepOwnerConsultation([\s\S]*?)let handleParsedOwnerRequest/)
  assert.ok(consult, 'consultation path must be inspectable')
  assert.match(consult[1], /withFreshAssistanceQuiescence/)
})

// 4. claim consume produces exactly one successor
test('WHAT[INTERACTION-AUTHORITY-012] trace_B_4_consume_produces_exactly_one_successor', () => {
  const host = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs')

  // handleParsedOwnerRequest has exactly two successor branches
  const parsed = host.match(/let handleParsedOwnerRequest([\s\S]*?)let handleOwnerRequestForProfile/)
  assert.ok(parsed, 'parsed owner request must be inspectable')

  assert.match(parsed[1], /AgentTier\.Fast[\s\S]*?escalateFastOwnerRequest/)
  assert.match(parsed[1], /AgentTier\.Deep[\s\S]*?beginDeepOwnerConsultation/)

  const escalateCount = (parsed[1].match(/escalateFastOwnerRequest/g) || []).length
  const consultCount = (parsed[1].match(/beginDeepOwnerConsultation/g) || []).length
  assert.equal(escalateCount, 1, 'exactly one escalation branch')
  assert.equal(consultCount, 1, 'exactly one consultation branch')

  // Dist: both kinds are Continuation, not Root
  assert.deepEqual(authority.originForContinuation('NeedHelpEscalation'), { kind: 'Continuation', label: 'NeedHelpEscalation' })
  assert.deepEqual(authority.originForContinuation('NeedHelpAdvice'), { kind: 'Continuation', label: 'NeedHelpAdvice' })
  assert.deepEqual(authority.tryParseContinuationKind('NeedHelpEscalation'), { kind: 'NeedHelpEscalation' })
  assert.deepEqual(authority.tryParseContinuationKind('NeedHelpAdvice'), { kind: 'NeedHelpAdvice' })
  assert.equal(authority.tryParseContinuationKind('HumanRoot'), null)
})

// 5. successor is a Continuation — preserves Root and Profile
test('WHAT[INTERACTION-AUTHORITY-012] trace_B_5_successor_is_continuation_preserves_root_profile', () => {
  // Dist: NeedHelpEscalation preserves Root and Profile
  let state = authority.registerAuthority(root, authority.empty)
  const escalationClaim = authority.claimContinuation(
    'pk-trace-esc',
    'ses_trace',
    'NeedHelpEscalation',
    root,
    'deep-coder',
    'trace-escalation',
  )
  state = authority.registerClaim(escalationClaim, state)
  state = authority.acceptClaim('pk-trace-esc', 'msg-trace-esc', state)

  assert.deepEqual(profile(state.activeLogicalRun), profile(root))
  assert.equal(state.activeLogicalRun.selectedAgent, 'fast-coder')
  assert.equal(escalationClaim.effectiveAgent, 'deep-coder')
  assert.equal(escalationClaim.origin, 'Continuation')
  assert.equal(escalationClaim.logicalRun, root.logicalRun)
  assert.equal(escalationClaim.authorityRoot, root.authorityRoot)
  assert.equal(state.acceptedContinuations.some((item) => item.kind === 'ProviderRetryAttempt'), false)

  // Dist: NeedHelpAdvice also preserves Root and Profile
  const adviceClaim = authority.claimContinuation(
    'pk-trace-adv',
    'ses_trace',
    'NeedHelpAdvice',
    root,
    'deep-coder',
    'trace-advice',
  )
  const afterAdvice = authority.registerClaim(adviceClaim, state)
  assert.deepEqual(profile(afterAdvice.activeLogicalRun), profile(root))
  assert.equal(adviceClaim.origin, 'Continuation')
  assert.equal(adviceClaim.logicalRun, root.logicalRun)
  assert.equal(adviceClaim.authorityRoot, root.authorityRoot)

  // Source: sendEscalationContinuation uses ContinuationKind, does not create new authority
  const host = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs')
  const sendEscalation = host.match(/let sendEscalationContinuation([\s\S]*?)let withFreshAssistanceQuiescence/)
  assert.ok(sendEscalation, 'sendEscalationContinuation must be inspectable')
  assert.match(sendEscalation[1], /ContinuationKind\.NeedHelpEscalation/)
  assert.match(sendEscalation[1], /sendContinuation/)
  assert.doesNotMatch(sendEscalation[1], /createAuthorityRoot|registerAuthority|claimAgentOwnerRoot/)
})

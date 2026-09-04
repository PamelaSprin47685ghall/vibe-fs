// ENFORCER-153: Blogger recovery rejudge from semantic claim + transcript.
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const ROOT = new URL('../../../', import.meta.url).pathname
const recoverySrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Context/Companion/Blogger/BloggerCrashRecovery.fs'), 'utf8')
const enforcerSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Enforcer/Continuation.fs'), 'utf8')
const repairSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Enforcer/Repair.fs'), 'utf8')
const interactionRepairSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Interaction/Repair/InteractionRepair.fs'), 'utf8')
const fallbackWorkflowSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs'), 'utf8')
const sessionNudgeSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Interaction/Dispatch/OpenCode/SessionNudge.fs'), 'utf8')
const pluginTransformsSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs'), 'utf8')
const probeSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Enforcer/Cycle/BloggerProbe.fs'), 'utf8')
const runtimeSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Context/Companion/Blogger/Runtime/State.fs'), 'utf8')

const rejudge = (claimedRun, pairs) => blog.rejudgeFromEvidence(
  claimedRun,
  pairs.map(([id, hasChronicle]) => ({ id, hasChronicle })),
)
const snapshot = (claimedRun, rows) => blog.rejudgeChronicleEvidence(claimedRun, rows)
const state = (claimedRun, pairs) => rejudge(claimedRun, pairs).state

// ── ENFORCER-153 rejudge table (pure evidence → BloggerToolRecovery) ─────────

test('WHAT[BD-017] ENFORCER_153_no_claim_rejudges_to_NoRecovery', () => {
  const out = rejudge(undefined, [['asst-p1', false], ['asst-p2', false]])
  assert.equal(out.state, 'NoRecovery')
  assert.equal(out.run, null)
})

test('WHAT[BD-017] ENFORCER_153_claim_plus_pure_prose_terminal_rejudges_to_InteractionNudgeIssued', () => {
  const out = rejudge('asst-p1', [['asst-p1', false]])
  assert.equal(out.state, 'InteractionNudgeIssued')
  assert.equal(out.run, 'asst-p1')
})

test('WHAT[BD-017] ENFORCER_153_claim_plus_second_pure_prose_rejudges_to_InteractionNudgeIssued', () => {
  // A second pure prose terminal is the hot-path AABB trigger, not its receipt.
  const out = rejudge('asst-p1', [['asst-p1', false], ['asst-p2', false]])
  assert.equal(out.state, 'InteractionNudgeIssued')
  assert.equal(out.run, 'asst-p1')
})

test('WHAT[BD-017] ENFORCER_153_claim_plus_valid_blog_after_nudge_rejudges_to_NoRecovery', () => {
  const out = rejudge('asst-p1', [['asst-p1', false], ['asst-blog', true]])
  assert.equal(out.state, 'NoRecovery')
  assert.equal(out.run, null)
})

test('WHAT[BD-017] ENFORCER_153_snapshot_rejudge_recognizes_exactly_one_completed_chronicle', () => {
  const one = snapshot('asst-p1', [{ id: 'asst-p1', chronicleCount: 1, completedChronicleCount: 1 }])
  const two = snapshot('asst-p1', [{ id: 'asst-p1', chronicleCount: 2, completedChronicleCount: 2 }])
  const mixed = snapshot('asst-p1', [{ id: 'asst-p1', chronicleCount: 2, completedChronicleCount: 1 }])
  assert.equal(one.state, 'NoRecovery')
  assert.equal(two.state, 'InteractionNudgeIssued', '2+ completed chronicle calls must not prove protocol recovery')
  assert.equal(mixed.state, 'InteractionNudgeIssued', 'one completed plus one failed chronicle is still 2 raw calls')
})

test('WHAT[BD-017] ENFORCER_153_snapshot_rejudge_uses_named_chronicle_toolparts', () => {
  assert.match(probeSrc, /ToolParts/, 'snapshot recovery must use named SessionToolPart evidence')
  assert.doesNotMatch(probeSrc, /name = "blog"/, 'legacy blog tool alias must not drive recovery')
})

test('WHAT[BD-017] ENFORCER_153_claim_with_missing_terminal_in_transcript_keeps_nudge_not_aabb', () => {
  const out = rejudge('asst-claimed-missing', [])
  assert.equal(out.state, 'InteractionNudgeIssued')
  assert.equal(out.run, 'asst-claimed-missing')
})

test('WHAT[BD-017] ENFORCER_153_runtime_carries_no_recovery_mirror', () => {
  assert.doesNotMatch(runtimeSrc, /BloggerRuntimeState\b/, 'no BloggerRuntimeState DU')
  assert.doesNotMatch(runtimeSrc, /BloggerRuntimeCell\b/, 'no BloggerRuntimeCell')
  assert.doesNotMatch(runtimeSrc, /Recovery: BloggerToolRecovery/)
  assert.doesNotMatch(runtimeSrc, /markInteractionNudgeIssued/)
  assert.doesNotMatch(runtimeSrc, /markAabbRepairIssued/)
  assert.doesNotMatch(recoverySrc, /Recovery = recovery/)
})

test('WHAT[BD-017] ENFORCER_153_cold_rejudge_never_invents_AabbRepairIssued', () => {
  const pureRejudge = probeSrc.match(/let rejudgeFromEvidence[\s\S]*?let private isCompletedChronicle/)
  assert.ok(pureRejudge)
  assert.doesNotMatch(pureRejudge[0], /BloggerToolRecovery\.AabbRepairIssued/)
  assert.match(probeSrc, /BloggerAabbRepairKind = "blogger-aabb"/)
  assert.match(probeSrc, /aabbClaimedRun[\s\S]*?BloggerToolRecovery\.AabbRepairIssued run/)
})

test('WHAT[BD-017] ENFORCER_153_hot_path_aabb_preserves_target_terminal_identity', () => {
  assert.match(repairSrc, /requestKey[\s\S]*?repairTerminalRun[\s\S]*?interaction-repair/)
  assert.match(enforcerSrc, /BloggerToolRecovery\.InteractionNudgeIssued _[\s\S]*?aabbRepair/)
  assert.match(enforcerSrc, /BloggerToolRecovery\.AabbRepairIssued issuedRun when issuedRun = terminalRun/)
  assert.match(enforcerSrc, /BloggerToolRecovery\.AabbRepairIssued _[\s\S]*?aabbRepair ctx sessionKey currentCtx live false/)
  assert.match(enforcerSrc, /aabbRepair ctx sessionKey currentCtx live true \("nudge semantic failure;/)
  assert.match(
    interactionRepairSrc,
    /\| BloggerRecoveryProbe\.InvalidTerminalRepairState\.AabbRepairIssued _ ->\s+consumeThenSendBloggerAabb\s+host\s+quiescence\s+context\s+sessionPort\s+rootWorkspace\s+eventPort\s+durable\s+requestId\s+requestKind\s+false\s+"blogger invalid terminal after AABB"/,
  )
  assert.match(
    interactionRepairSrc,
    /isInteractionRepairContinuation[\s\S]*?ContinuationKind\.InteractionRepair[\s\S]*?bloggerProviderRequestKind[\s\S]*?BloggerRequestContext\.Main _ -> ProviderRequestKind\.BloggerMain[\s\S]*?BloggerRequestContext\.Squash _ -> ProviderRequestKind\.BloggerSquash[\s\S]*?repairRequestKind[\s\S]*?ProviderRequestKind\.InteractionRepair[\s\S]*?bloggerProviderRequestKind request/,
    'fallback authorization must classify the current repair turn or its active owned Blogger request',
  )
  assert.match(
    interactionRepairSrc,
    /ProviderRecoveryWorkflow\.admitPolicyAuthorizedFailure\s+journal\s+turn\s+ExecutionFailure\.ProviderTransient\s+requestKind\s+reason/,
    'AABB must pass exact current evidence to the fallback workflow owner',
  )
  assert.match(
    fallbackWorkflowSrc,
    /policyFallbackDecision[\s\S]*?recoveryDecision turn failure current requestKind[\s\S]*?FallbackDecision\.NoFallback -> PolicyFallbackDecision\.Exhausted[\s\S]*?FallbackDecision\.AdvanceFallback authorization -> PolicyFallbackDecision\.Authorized authorization[\s\S]*?admitAuthorizedFailure[\s\S]*?FallbackLedger\.recordAuthorizedFailure durable ownerSessionId authorization error[\s\S]*?admitCurrentFailure/,
    'the fallback workflow must consume its policy authorization through the canonical ledger operation',
  )
  assert.match(
    fallbackWorkflowSrc,
    /outcomeAfterRepeatedAdmission[\s\S]*?FallbackEvidence\.tryCurrentState sessionId \(AgentJournal\.snapshot durable\)[\s\S]*?Some latest when fallbackBudgetOf latest = ProviderRecoveryBudget\.Exhausted ->\s+Ok ConfirmedFailureOutcome\.RecoveryExhausted[\s\S]*?reconcileFailureAdmission[\s\S]*?Ok ConfirmedFailureOutcome\.AlreadyRecorded -> outcomeAfterRepeatedAdmission durable sessionId/,
    'the owner must return a typed exhausted outcome after a racing ledger admission',
  )
  assert.doesNotMatch(interactionRepairSrc, /ExecutionFailurePolicy\.decide|FallbackEvidence\.|FallbackLedger\./)
  assert.doesNotMatch(interactionRepairSrc + fallbackWorkflowSrc, /FallbackLedger\.recordConfirmedFailure/)
  assert.match(
    interactionRepairSrc,
    /\| Ok ConfirmedFailureOutcome\.RecoveryExhausted when guaranteedFirstAabb -> SendAabb/,
    'fallback exhaustion cannot steal the guaranteed first protocol AABB',
  )
  assert.match(
    interactionRepairSrc,
    /match decideBloggerAabbFailure guaranteedFirstAabb confirmedFailure with\s+\| SendAabb -> do! sendAabb \(\)\s+\| ExhaustProtocol ->\s+do! exhaustBloggerProtocol host eventPort journal context "blogger protocol repair exhausted"/,
    'the named AABB decision must own the physical send/exhaust operation',
  )
})

test('WHAT[BD-017] ENFORCER_153_repairState_request_isolation_and_abandon_lifecycle', () => {
  const claims = [
    { requestId: 'req-1', kind: 'blogger-missing-tool', run: 'asst-p1', status: 'Claimed' },
    { requestId: 'req-1', kind: 'blogger-aabb', run: 'asst-p1', status: 'Claimed' },
    { requestId: 'req-1', kind: 'blogger-aabb', run: 'asst-p1', status: 'Abandoned' },
  ]
  assert.deepEqual(blog.repairState({ requestId: 'req-1', claims }).state, 'AabbRepairIssued')
  assert.deepEqual(blog.repairState({ requestId: 'req-1', claims: [claims[0]] }).state, 'InteractionNudgeIssued')
  assert.deepEqual(blog.repairState({ requestId: 'req-2', claims }).state, 'NoRecovery')
  assert.deepEqual(blog.repairState({ requestId: 'req-1', claims: [claims[2], claims[0]] }).state, 'InteractionNudgeIssued')
})

test('WHAT[BD-017] ENFORCER_065_chronicle_tool_error_defers_to_the_host_tool_loop_instead_of_repairing', () => {
  assert.match(
    enforcerSrc,
    /hasErroredBlogAttempt[\s\S]{0,520}ctx\.Project ctx\.RawMessages/,
    'an errored chronicle call is still inside the Host tool loop and must not spend repair/fallback',
  )
  assert.doesNotMatch(
    enforcerSrc,
    /hasErroredBlogAttempt[\s\S]{0,220}decideErroredBlog/,
    'tool errors must not jump directly into Blogger repair',
  )
})

test('WHAT[BD-017] ENFORCER_066_first_protocol_nudge_is_idle_owned_never_sent_from_transform', () => {
  assert.match(
    enforcerSrc,
    /BloggerToolRecovery\.NoRecovery\s*->[\s\S]{0,520}ctx\.Project ctx\.RawMessages/,
    'transform must leave the first invalid terminal untouched so Host idle can own the physical nudge',
  )
  assert.doesNotMatch(
    enforcerSrc,
    /BloggerToolRecovery\.NoRecovery\s*->\s*interactionNudge/,
    'a transform-time interaction nudge can be queued behind the Host tool loop',
  )
  assert.doesNotMatch(
    pluginTransformsSrc,
    /trySendInteractionRepair/,
    'the provider transform wiring must not possess a physical interaction-repair sender',
  )
})

test('WHAT[BD-017] duplicate_nudge_admission_is_idempotent_not_AABB_failure', () => {
  assert.match(sessionNudgeSrc, /IdleContinuationOutcome\.AlreadyAdmitted/)
  assert.doesNotMatch(
    sessionNudgeSrc,
    /IdleContinuationOutcome\.Failed\s+"Interaction repair already claimed/,
    'a racing duplicate claim is admission evidence, not a failed repair',
  )
  assert.doesNotMatch(
    interactionRepairSrc,
    /IndexOf\("already claimed"/,
    'AABB must never recover typed idempotency by parsing error prose',
  )
})

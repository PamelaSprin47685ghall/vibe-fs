// ENFORCER-153: Blogger recovery facts from semantic claim + transcript.
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = new URL('../../../', import.meta.url).pathname
const enforcerSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Enforcer/Continuation.fs'), 'utf8')
const interactionRepairSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Interaction/Repair/InteractionRepair.fs'), 'utf8')
const sessionNudgeSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Interaction/Dispatch/OpenCode/SessionNudge.fs'), 'utf8')
const pluginTransformsSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs'), 'utf8')
const probeSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Enforcer/Cycle/BloggerProbe.fs'), 'utf8')

test('WHAT[BD-017] ENFORCER_153_snapshot_rejudge_uses_named_chronicle_toolparts', () => {
  assert.match(probeSrc, /ToolParts/, 'snapshot recovery must use named SessionToolPart evidence')
  assert.doesNotMatch(probeSrc, /name = "blog"/, 'legacy blog tool alias must not drive recovery')
})

test('WHAT[BD-017] ENFORCER_153_probe_exposes_pure_facts_not_stage_reconstruction', () => {
  assert.doesNotMatch(probeSrc, /type InvalidTerminalRepairState/, 'no InvalidTerminalRepairState DU')
  assert.doesNotMatch(probeSrc, /rejudgeFromEvidence/, 'no rejudgeFromEvidence in probe')
  assert.doesNotMatch(probeSrc, /rejudgeToolRecovery/, 'no rejudgeToolRecovery in probe')
  assert.doesNotMatch(probeSrc, /repairStateForInvalidTerminal/, 'no repairStateForInvalidTerminal in probe')
  assert.match(probeSrc, /BloggerMissingToolRepairKind = "blogger-missing-tool"/)
  assert.match(probeSrc, /BloggerAabbRepairKind = "blogger-aabb"/)
  assert.match(probeSrc, /val repairClaimedForKind|let repairClaimedForKind/)
  assert.match(probeSrc, /val terminalRequestOwnershipForPhysicalMessage|let terminalRequestOwnershipForPhysicalMessage/)
})

test('WHAT[BD-017] ENFORCER_153_idle_forwards_exact_blogger_repair_to_the_single_owner', () => {
  assert.match(interactionRepairSrc, /BloggerCoordinator\.observeIdleRepair/)
  assert.doesNotMatch(
    interactionRepairSrc,
    /ProviderRecoveryWorkflow|admitPolicyAuthorizedFailure|FailureAdmissionOutcome|sendAabb/,
    'InteractionRepair may submit the observation but cannot interpret Blogger repair or provider retry',
  )
})

test('WHAT[BD-017] ENFORCER_065_chronicle_tool_error_defers_to_the_host_tool_loop_instead_of_repairing', () => {
  assert.match(
    enforcerSrc,
    /hasErroredBlogAttempt[\s\S]{0,520}ctx\.Project ctx\.RawMessages/,
    'an errored chronicle call is still inside the Host tool loop and must not spend repair/retry budget',
  )
  assert.doesNotMatch(
    enforcerSrc,
    /hasErroredBlogAttempt[\s\S]{0,220}decideErroredBlog/,
    'tool errors must not jump directly into Blogger repair',
  )
})

test('WHAT[BD-017] ENFORCER_066_first_protocol_nudge_is_idle_owned_never_sent_from_transform', () => {
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

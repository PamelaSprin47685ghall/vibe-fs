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
  assert.match(interactionRepairSrc, /AabbRepairIssued _ ->[\s\S]*?consumeThenSendBloggerAabb[\s\S]*?false/)
  assert.match(interactionRepairSrc, /RecoveryExhausted when guaranteedFirstAabb[\s\S]*?sendAabb/)
  assert.match(interactionRepairSrc, /RecoveryExhausted ->[\s\S]*?exhaustBloggerProtocol/)
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

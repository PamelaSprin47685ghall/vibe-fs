// Split from tests/unit/enforcer/blogger-crash-recovery.test.mjs (cutover Wave 2a); owner: behavior-diagnosis.
//
// ENFORCER-153: BloggerToolRecovery rejudge from durable claim + transcript.
// The C5 crash-window half (classify/restore/wiring) moved to
// crash-reconciliation (blogger-crash-recovery.test.mjs).
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { caseOf } from '../../verification-system/tests/support/domain.mjs'
import {
  repairStateRequestIsolationAndAbandonLifecycle,
  snapshotRejudgeChronicleCardinality,
} from './support/blogger-recovery.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname
const recoverySrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Context/Companion/Blogger/BloggerCrashRecovery.fs'),
  'utf8',
)
const enforcerSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Enforcer/Continuation.fs'),
  'utf8',
)
const repairSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Enforcer/Repair.fs'),
  'utf8',
)

const probeModuleSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Enforcer/Cycle/BloggerProbe.fs'),
  'utf8',
)

const loadRecovery = async () => {
  // ENFORCER-153 derivation lives in BloggerRecoveryProbe; the crash window
  // classify/restore stays in BloggerCrashRecovery.
  const crash = await import(
    new URL('../../../dist/Context/Companion/Blogger/BloggerCrashRecovery.js', import.meta.url).pathname
  )
  const probe = await import(
    new URL('../../../dist/Enforcer/Cycle/BloggerProbe.js', import.meta.url).pathname
  )
  return { crash, probe }
}

const rejudgeFromEvidence = async () => {
  const { probe: mod } = await loadRecovery()
  const fn =
    mod.BloggerRecoveryProbe_rejudgeFromEvidence ||
    mod.rejudgeFromEvidence ||
    Object.values(mod).find((v) => typeof v === 'function' && v.name?.includes('rejudgeFromEvidence'))
  assert.ok(fn, 'rejudgeFromEvidence export present')
  return fn
}

/** Fable list from JS array for (string * bool) list. */
const toEvidenceList = async (pairs) => {
  const { toList } = await import('../../verification-system/tests/support/domain.mjs')
  // Fable tuple lists: each item is a 2-array [id, hasBlog]
  return toList(pairs.map(([id, hasBlog]) => [id, hasBlog]))
}

// ── ENFORCER-153 rejudge table (pure evidence → BloggerToolRecovery) ─────────

test('WHAT[BD-017] ENFORCER_153_no_claim_rejudges_to_NoRecovery', async () => {
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([
    ['asst-p1', false],
    ['asst-p2', false],
  ])
  const out = rejudge(undefined, terminals)
  assert.equal(caseOf(out), 'NoRecovery')
})

test('WHAT[BD-017] ENFORCER_153_claim_plus_pure_prose_terminal_rejudges_to_InteractionNudgeIssued', async () => {
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([['asst-p1', false]])
  const out = rejudge('asst-p1', terminals)
  assert.equal(caseOf(out), 'InteractionNudgeIssued')
  const run = out.fields?.[0]
  assert.ok(run, 'run identity present')
  const { idValue } = await import('../../verification-system/tests/support/domain.mjs')
  assert.equal(idValue.providerRun(run), 'asst-p1')
})

test('WHAT[BD-017] ENFORCER_153_claim_plus_second_pure_prose_rejudges_to_InteractionNudgeIssued', async () => {
  // Second pure prose after claim is the *trigger* for aabbRepair (ENFORCER-067),
  // not its receipt. Cold transcript rejudge therefore must not invent
  // AabbRepairIssued: deriving it here would let the hot path exhaust without ever
  // issuing an AABB repair (budget stolen across a crash).
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([
    ['asst-p1', false],
    ['asst-p2', false],
  ])
  const out = rejudge('asst-p1', terminals)
  assert.equal(caseOf(out), 'InteractionNudgeIssued')
  const run = out.fields?.[0]
  assert.ok(run, 'run identity present')
  const { idValue } = await import('../../verification-system/tests/support/domain.mjs')
  assert.equal(idValue.providerRun(run), 'asst-p1')
})

test('WHAT[BD-017] ENFORCER_153_claim_plus_valid_blog_after_nudge_rejudges_to_NoRecovery', async () => {
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([
    ['asst-p1', false],
    ['asst-blog', true],
  ])
  const out = rejudge('asst-p1', terminals)
  assert.equal(caseOf(out), 'NoRecovery')
})

test('WHAT[BD-017] ENFORCER_153_snapshot_rejudge_recognizes_exactly_one_completed_chronicle', async () => {
  const result = await snapshotRejudgeChronicleCardinality()

  assert.equal(result.one, 'NoRecovery')
  assert.equal(result.two, 'InteractionNudgeIssued', '2+ completed chronicle calls must not prove protocol recovery')
  assert.equal(result.mixed, 'InteractionNudgeIssued', 'one completed plus one failed chronicle is still 2 raw calls')
})

test('WHAT[BD-017] ENFORCER_153_snapshot_rejudge_uses_named_chronicle_toolparts', async () => {
  const probeSrc = readFileSync(join(ROOT, 'src/Wanxiangshu/Enforcer/Cycle/BloggerProbe.fs'), 'utf8')
  assert.match(probeSrc, /ToolParts/, 'snapshot recovery must use named SessionToolPart evidence')
  assert.doesNotMatch(probeSrc, /name = "blog"/, 'legacy blog tool alias must not drive recovery')
})

test('WHAT[BD-017] ENFORCER_153_claim_with_missing_terminal_in_transcript_keeps_nudge_not_aabb', async () => {
  // Claimed run not in Host snapshot: never invent AABB (conservative).
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([])
  const out = rejudge('asst-claimed-missing', terminals)
  assert.equal(caseOf(out), 'InteractionNudgeIssued')
})

test('WHAT[BD-017] ENFORCER_153_runtime_carries_no_recovery_mirror', () => {
  // DSL-003: the recovery stage is derived on every read (BloggerRecoveryProbe).
  // BloggerRuntimeState.fs must not define a cell/State DU or mark* writers;
  // restoreRuntime does not store rejudged recovery.
  const runtimeSrc = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Context/Companion/Blogger/Runtime/State.fs'),
    'utf8',
  )
  assert.doesNotMatch(runtimeSrc, /BloggerRuntimeState\b/, 'no BloggerRuntimeState DU')
  assert.doesNotMatch(runtimeSrc, /BloggerRuntimeCell\b/, 'no BloggerRuntimeCell')
  assert.doesNotMatch(runtimeSrc, /Recovery: BloggerToolRecovery/)
  assert.doesNotMatch(runtimeSrc, /markInteractionNudgeIssued/)
  assert.doesNotMatch(runtimeSrc, /markAabbRepairIssued/)
  // restoreRuntime no longer takes a rejudged recovery: nothing to store.
  assert.doesNotMatch(recoverySrc, /Recovery = recovery/)
})

test('WHAT[BD-017] ENFORCER_153_cold_rejudge_never_invents_AabbRepairIssued', () => {
  // Completed-assistant transcript evidence alone still cannot invent AABB. The
  // idle path writes a durable blogger-aabb InteractionRepair claim, so
  // rejudgeToolRecovery may restore AabbRepairIssued only with that claim's exact
  // terminal identity.
  const probeSrc = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Enforcer/Cycle/BloggerProbe.fs'),
    'utf8',
  )
  const pureRejudge = probeSrc.match(/let rejudgeFromEvidence[\s\S]*?let private isCompletedChronicle/)
  assert.ok(pureRejudge)
  assert.doesNotMatch(pureRejudge[0], /BloggerToolRecovery\.AabbRepairIssued/)
  assert.match(probeSrc, /BloggerAabbRepairKind = "blogger-aabb"/)
  assert.match(probeSrc, /aabbClaimedRun[\s\S]*?BloggerToolRecovery\.AabbRepairIssued run/)
})

test('WHAT[BD-017] ENFORCER_153_hot_path_aabb_preserves_target_terminal_identity', () => {
  // The hot path injects a synthetic repair message with requestKey plus the exact
  // terminal it repairs. Re-entry of that same terminal is idempotent; only a
  // different invalid terminal after AABB may exhaust the protocol.
  assert.match(repairSrc, /requestKey[\s\S]*?repairTerminalRun[\s\S]*?interaction-repair/)
  assert.match(enforcerSrc, /BloggerToolRecovery\.InteractionNudgeIssued _[\s\S]*?aabbRepair/)
  assert.match(enforcerSrc, /BloggerToolRecovery\.AabbRepairIssued issuedRun when issuedRun = terminalRun/)
  assert.match(enforcerSrc, /BloggerToolRecovery\.AabbRepairIssued _[\s\S]*?protocol-repair-exhausted/)
})

test('WHAT[BD-017] ENFORCER_153_repairState_old_claim_new_terminal_is_nudge_with_claimed_run', async () => {
  const result = await repairStateRequestIsolationAndAbandonLifecycle()

  assert.equal(result.sameRequest, 'InteractionNudgeIssued')
  assert.equal(result.sameRequestClaimedRun, 'asst-p1', 'payload is the CLAIMED run, not the new terminal')
  assert.equal(
    result.otherRequestAfterNudge,
    'NoRecovery',
    'a nudge claim from another BloggerRequestId on the same Session/LogicalRun must be invisible',
  )
  assert.equal(
    result.otherRequestAfterAabb,
    'NoRecovery',
    'an AABB claim from an older BloggerRequestId must not exhaust the new request',
  )
  assert.equal(
    result.afterAbandonedAabb,
    'InteractionNudgeIssued',
    'an abandoned AABB claim proves no AABB landed and must not restore AabbRepairIssued',
  )
})

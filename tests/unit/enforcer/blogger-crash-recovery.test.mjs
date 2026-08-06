/**
 * C5 item 20: pure decision table for Blogger crash windows A/B/C/D.
 * Family recovery interpreter owns startup timing; classify stays pure.
 *
 * ENFORCER-153: BloggerToolRecovery rejudge from durable claim + transcript.
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { caseOf } from '../support/domain.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname
const recoverySrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Application/Reconciliation/BloggerCrashRecovery.fs'),
  'utf8',
)
const spikeSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Infrastructure/OpenCode/Plugin/SpikePlugin.fs'),
  'utf8',
)
const scopeSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs'),
  'utf8',
)
const interpreterSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Application/Reconciliation/SessionRecoveryWorkflow.fs'),
  'utf8',
)
const enforcerSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Session/EnforcerHost.fs'),
  'utf8',
)

const loadRecovery = async () => {
  const mod = await import(
    new URL('../../../dist/Application/Reconciliation/BloggerCrashRecovery.js', import.meta.url).pathname
  )
  return mod
}

const rejudgeFromEvidence = async () => {
  const mod = await loadRecovery()
  const fn =
    mod.BloggerCrashRecovery_rejudgeFromEvidence ||
    mod.rejudgeFromEvidence ||
    Object.values(mod).find((v) => typeof v === 'function' && v.name?.includes('rejudgeFromEvidence'))
  assert.ok(fn, 'rejudgeFromEvidence export present')
  return fn
}

/** Fable list from JS array for (string * bool) list. */
const toEvidenceList = async (pairs) => {
  const { toList } = await import('../support/domain.mjs')
  // Fable tuple lists: each item is a 2-array [id, hasBlog]
  return toList(pairs.map(([id, hasBlog]) => [id, hasBlog]))
}

test('C5_crash_recovery_module_exists_with_window_outcomes', () => {
  assert.match(recoverySrc, /module BloggerCrashRecovery/)
  assert.match(recoverySrc, /AbandonedUnsent/)
  assert.match(recoverySrc, /Recommitted/)
  assert.match(recoverySrc, /RestoredParked/)
  assert.match(recoverySrc, /RestoredInFlight/)
  assert.match(recoverySrc, /crash-window-A/)
  assert.match(recoverySrc, /rejudgeFromEvidence/)
  assert.match(recoverySrc, /rejudgeToolRecovery/)
  assert.match(recoverySrc, /blogger-missing-tool/)
})

test('C5_crash_recovery_wired_through_family_ports', () => {
  assert.match(spikeSrc, /AttachFamilyRecoveryPorts/)
  assert.match(scopeSrc, /RequireFamilyRecovery/)
  assert.match(interpreterSrc, /BloggerCrashRecovery\.reconcile/)
  assert.doesNotMatch(spikeSrc, /AttachBloggerRecoveryGate/)
  assert.doesNotMatch(scopeSrc, /bloggerRecoveryGate/)
})

test('C5_classify_open_request_window_A_unsent', async () => {
  const mod = await loadRecovery()
  const classify =
    mod.BloggerCrashRecovery_classifyOpenRequest ||
    mod.classifyOpenRequest ||
    Object.values(mod).find((v) => typeof v === 'function' && v.length === 3)

  assert.ok(classify, 'classifyOpenRequest export present')
  const a = classify(false, false, false)
  assert.ok(a, 'window A decision')
  assert.equal(caseOf(a), 'AbandonedUnsent')
})

test('C5_classify_open_request_window_C_tool_present', async () => {
  const mod = await loadRecovery()
  const classify =
    mod.BloggerCrashRecovery_classifyOpenRequest ||
    mod.classifyOpenRequest ||
    Object.values(mod).find((v) => typeof v === 'function' && v.length === 3)

  const c = classify(true, true, false)
  assert.equal(caseOf(c), 'Recommitted')
})

test('C5_classify_open_request_window_B_inflight', async () => {
  const mod = await loadRecovery()
  const classify =
    mod.BloggerCrashRecovery_classifyOpenRequest ||
    mod.classifyOpenRequest ||
    Object.values(mod).find((v) => typeof v === 'function' && v.length === 3)

  const b = classify(true, false, false)
  assert.equal(caseOf(b), 'RestoredInFlight')
})

// ── ENFORCER-153 rejudge table (pure evidence → BloggerToolRecovery) ─────────

test('ENFORCER_153_no_claim_rejudges_to_NoRecovery', async () => {
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([
    ['asst-p1', false],
    ['asst-p2', false],
  ])
  const out = rejudge(undefined, terminals)
  assert.equal(caseOf(out), 'NoRecovery')
})

test('ENFORCER_153_claim_plus_pure_prose_terminal_rejudges_to_InteractionNudgeIssued', async () => {
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([['asst-p1', false]])
  const out = rejudge('asst-p1', terminals)
  assert.equal(caseOf(out), 'InteractionNudgeIssued')
  const run = out.fields?.[0]
  assert.ok(run, 'run identity present')
  const { idValue } = await import('../support/domain.mjs')
  assert.equal(idValue.providerRun(run), 'asst-p1')
})

test('ENFORCER_153_claim_plus_second_pure_prose_rejudges_to_InteractionNudgeIssued', async () => {
  // Second pure prose after claim is the *trigger* for aabbRepair (ENFORCER-067),
  // not its receipt. AABB is memory-only (markAabbRepairConsumed + transform
  // injection, no journal fact), so cold rejudge must not invent AabbRepairConsumed:
  // deriving it here would let the hot path fatalEnd without ever injecting the
  // AABB repair (budget stolen across a crash).
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([
    ['asst-p1', false],
    ['asst-p2', false],
  ])
  const out = rejudge('asst-p1', terminals)
  assert.equal(caseOf(out), 'InteractionNudgeIssued')
  const run = out.fields?.[0]
  assert.ok(run, 'run identity present')
  const { idValue } = await import('../support/domain.mjs')
  assert.equal(idValue.providerRun(run), 'asst-p1')
})

test('ENFORCER_153_claim_plus_valid_blog_after_nudge_rejudges_to_NoRecovery', async () => {
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([
    ['asst-p1', false],
    ['asst-blog', true],
  ])
  const out = rejudge('asst-p1', terminals)
  assert.equal(caseOf(out), 'NoRecovery')
})

test('ENFORCER_153_claim_with_missing_terminal_in_transcript_keeps_nudge_not_aabb', async () => {
  // Claimed run not in Host snapshot: never invent AABB (conservative).
  const rejudge = await rejudgeFromEvidence()
  const terminals = await toEvidenceList([])
  const out = rejudge('asst-claimed-missing', terminals)
  assert.equal(caseOf(out), 'InteractionNudgeIssued')
})

test('ENFORCER_153_restoreRuntime_wires_rejudge_not_hardcoded_NoRecovery', () => {
  // Source contract: restore path must call rejudgeToolRecovery for InFlight open.
  assert.match(recoverySrc, /rejudgeToolRecovery durable/)
  assert.match(recoverySrc, /Recovery = recovery/)
  // Hardcoded NoRecovery only on abandon / parked-success paths, not open InFlight.
  const inflightRestore = recoverySrc.match(
    /restoreRuntime[\s\S]*?InFlight ctx[\s\S]*?recovery/,
  )
  assert.ok(inflightRestore, 'InFlight restore must pass rejudged recovery')
})

test('ENFORCER_153_cold_rejudge_never_invents_AabbRepairConsumed', () => {
  // AABB is derived from the visible transcript, not a memory mark. Cold rejudge
  // (rejudgeFromEvidence / rejudgeToolRecovery) must restore InteractionNudgeIssued
  // at most, never AabbRepairConsumed.
  const coldRejudge = recoverySrc.match(/let rejudgeFromEvidence[\s\S]*?let rejudgeToolRecovery/)
  assert.ok(coldRejudge)
  assert.doesNotMatch(coldRejudge[0], /BloggerToolRecovery\.AabbRepairConsumed/)
})

test('ENFORCER_153_hot_path_aabb_infers_from_visible_transcript', () => {
  // The hot path injects a synthetic repair message with info.requestKey; the next
  // transform uses its presence (not a mutable flag) to decide AabbRepairConsumed.
  assert.match(enforcerSrc, /requestKey[\s\S]*?interaction-repair/)
  assert.match(enforcerSrc, /BloggerToolRecovery\.InteractionNudgeIssued _[\s\S]*?aabbRepair/)
  assert.match(enforcerSrc, /BloggerToolRecovery\.AabbRepairConsumed[\s\S]*?fatalEnd/)
})

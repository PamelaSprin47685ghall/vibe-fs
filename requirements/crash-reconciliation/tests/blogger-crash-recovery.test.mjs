// Split from tests/unit/enforcer/blogger-crash-recovery.test.mjs (cutover Wave 2a); owner: crash-reconciliation.
//
// C5 item 20 (CRASH-016): pure decision table for Blogger crash windows A/B/C/D.
// The classifier stays pure; ordinary plugin lifecycle must not invoke it.
// The ENFORCER-153 rejudge half moved to behavior-diagnosis
// (enforcer-153-rejudge.test.mjs).
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import * as crash from '../../../dist/Context/Companion/Blogger/BloggerCrashSurface.js'

const ROOT = new URL('../../../', import.meta.url).pathname
const recoverySrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Context/Companion/Blogger/BloggerCrashRecovery.fs'),
  'utf8',
)
const spikeSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/OpenCode/Plugin/PluginRecoveryWiring.fs'),
  'utf8',
)
const scopeSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs'),
  'utf8',
)
const interpreterSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Workflow.fs'),
  'utf8',
)

const probeModuleSrc = readFileSync(
  join(ROOT, 'src/Wanxiangshu/Enforcer/Cycle/BloggerProbe.fs'),
  'utf8',
)

test('WHAT[CRASH-016] C5_crash_recovery_module_exists_with_window_outcomes', () => {
  assert.match(recoverySrc, /module BloggerCrashRecovery/)
  assert.match(recoverySrc, /AbandonedUnsent/)
  assert.match(recoverySrc, /Recommitted/)
  assert.match(recoverySrc, /ReceiptedIdle/)
  assert.match(recoverySrc, /RestoredInFlight/)
  assert.match(recoverySrc, /crash-window-A/)
  assert.match(probeModuleSrc, /rejudgeFromEvidence/)
  assert.match(probeModuleSrc, /rejudgeToolRecovery/)
  assert.match(probeModuleSrc, /blogger-missing-tool/)
})

test('WHAT[CRASH-017] C5_crash_recovery_library_is_not_wired_into_ordinary_plugin_lifecycle', () => {
  assert.doesNotMatch(spikeSrc, /AttachFamilyRecoveryPorts|BloggerCrashRecovery|recoverFamilyDirect/)
  assert.match(scopeSrc, /Current-process join admission only/)
  assert.match(interpreterSrc, /BloggerCrashRecovery\.reconcile/, 'recovery library may remain for future explicit /continue')
})

test('WHAT[CRASH-016] C5_classify_open_request_window_A_unsent', () => {
  assert.equal(crash.classifyOpenRequest(false, false, false), 'AbandonedUnsent')
})

test('WHAT[CRASH-016] C5_classify_open_request_window_C_tool_present', () => {
  assert.equal(crash.classifyOpenRequest(true, true, false), 'Recommitted')
})

test('WHAT[CRASH-016] C5_classify_open_request_window_B_inflight', () => {
  assert.equal(crash.classifyOpenRequest(true, false, false), 'RestoredInFlight')
})

test('WHAT[CRASH-016] C5_window_D_never_forces_parked_without_a_waiter', () => {
  // DSL-003: forcing `Parked` at restore with no ParkedTransform and
  // NotArmed arming stages the next material as an un-resumable PendingOffer
  // (mayRecover is false after restart, so no squash path starts) — the
  // session stalls. Window D must leave flight clear; receipts are re-checked
  // by the drain path after the next commit.
  const windowD = recoverySrc.slice(
    recoverySrc.indexOf('let private receiptedIdleDecision'),
    recoverySrc.indexOf('let private companionBloggerId'),
  )
  assert.ok(
    !/restoreRuntime/.test(windowD),
    'window D must not call restoreRuntime (no state to restore)',
  )
  assert.ok(
    /ReceiptedIdle/.test(recoverySrc),
    'window D still reports the touched session as ReceiptedIdle',
  )
  assert.ok(
    !/BloggerRuntimeState\.Parked/.test(windowD),
    'window D must not mention Parked at all',
  )
})

test('WHAT[CRASH-016] C5_snapshot_tool_evidence_uses_latest_assistant_and_exactly_one_chronicle', () => {
  assert.match(recoverySrc, /List\.rev/, 'recovery must judge the latest assistant, not stale historical tool calls')
  assert.match(recoverySrc, /ToolParts/, 'recovery must preserve named tool identity')
  assert.match(recoverySrc, /Array\.filter[^\n]*ToolName = "chronicle"/, 'raw chronicle cardinality must be counted')
  assert.match(recoverySrc, /\[\| part \|\]/, 'only exact-one raw chronicle may prove completion')
})

test('WHAT[CRASH-016] C5_crash_recovery_reads_HasFlight_not_cell_State', () => {
  // PR7 Slice 3: window windows use physical flight ownership.
  // Forbidden: match live.State / BloggerRuntimeState.InFlight as restore authority.
  assert.doesNotMatch(
    recoverySrc,
    /match live\.State/,
    'reconcile must not match live.State for window decisions',
  )
  assert.doesNotMatch(
    recoverySrc,
    /BloggerRuntimeState\.(InFlight|Idle)/,
    'restoreRuntime must not take BloggerRuntimeState; write flight via SetCurrentRequest/ClearCurrentRequest',
  )
  assert.match(
    recoverySrc,
    /host\.HasFlight/,
    'window busy / AlreadyLive must prefer HasFlight',
  )
  assert.match(
    recoverySrc,
    /host\.SetCurrentRequest/,
    'restore InFlight rebuilds physical flight via SetCurrentRequest',
  )
  assert.match(
    recoverySrc,
    /host\.ClearCurrentRequest/,
    'abandon / clear path uses ClearCurrentRequest',
  )
})

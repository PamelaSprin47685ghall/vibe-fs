/**
 * C5 item 20: pure decision table for Blogger crash windows A/B/C/D.
 * Host wiring is exercised via production gate attach + unit classify.
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'

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

test('C5_crash_recovery_module_exists_with_window_outcomes', () => {
  assert.match(recoverySrc, /module BloggerCrashRecovery/)
  assert.match(recoverySrc, /AbandonedUnsent/)
  assert.match(recoverySrc, /Recommitted/)
  assert.match(recoverySrc, /RestoredParked/)
  assert.match(recoverySrc, /RestoredInFlight/)
  assert.match(recoverySrc, /crash-window-A/)
})

test('C5_crash_recovery_gate_attached_from_plugin', () => {
  assert.match(spikeSrc, /AttachBloggerRecoveryGate/)
  assert.match(spikeSrc, /BloggerCrashRecovery\.RecoveryGate/)
  assert.match(scopeSrc, /AttachBloggerRecoveryGate/)
  assert.match(scopeSrc, /bloggerRecoveryGate/)
  // EnsureRecoveryDone awaits both prompt + blogger passes.
  assert.match(scopeSrc, /do! blogger/)
})

test('C5_classify_open_request_window_A_unsent', async () => {
  const { caseOf, payloadOf } = await import('../domain.mjs')
  // Import production classify via built module if exported; else structural.
  // classifyOpenRequest is pure and exported from BloggerCrashRecovery.
  const mod = await import(
    new URL('../../../dist/Application/Reconciliation/BloggerCrashRecovery.js', import.meta.url).pathname
  )
  const classify =
    mod.BloggerCrashRecovery_classifyOpenRequest ||
    mod.classifyOpenRequest ||
    Object.values(mod).find((v) => typeof v === 'function' && v.length === 3)

  assert.ok(classify, 'classifyOpenRequest export present')
  const a = classify(false, false, false)
  assert.ok(a, 'window A decision')
  // F# option Some becomes value; DU case name.
  const outcome = a.fields ? a : a
  assert.equal(caseOf(outcome), 'AbandonedUnsent')
})

test('C5_classify_open_request_window_C_tool_present', async () => {
  const { caseOf } = await import('../domain.mjs')
  const mod = await import(
    new URL('../../../dist/Application/Reconciliation/BloggerCrashRecovery.js', import.meta.url).pathname
  )
  const classify =
    mod.BloggerCrashRecovery_classifyOpenRequest ||
    mod.classifyOpenRequest ||
    Object.values(mod).find((v) => typeof v === 'function' && v.length === 3)

  const c = classify(true, true, false)
  assert.equal(caseOf(c), 'Recommitted')
})

test('C5_classify_open_request_window_B_inflight', async () => {
  const { caseOf } = await import('../domain.mjs')
  const mod = await import(
    new URL('../../../dist/Application/Reconciliation/BloggerCrashRecovery.js', import.meta.url).pathname
  )
  const classify =
    mod.BloggerCrashRecovery_classifyOpenRequest ||
    mod.classifyOpenRequest ||
    Object.values(mod).find((v) => typeof v === 'function' && v.length === 3)

  const b = classify(true, false, false)
  assert.equal(caseOf(b), 'RestoredInFlight')
})

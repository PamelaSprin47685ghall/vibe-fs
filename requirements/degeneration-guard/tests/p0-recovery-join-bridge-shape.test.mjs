// Split from tests/unit/verify/p0-recovery-join-gate.test.mjs (cutover Wave 2a); owner: degeneration-guard
//
// DG-009（LOOP-006 桥接的静态形状）：armed abort 桥接 recordConfirmedFailure——门禁侧
// 的静态形状 = lifecycle-aborted-record（abort 不得写 completion 记录）与
// record-completion-single-owner（completion 单一 owner allowlist）的正负模式。
// aborted≠terminal 规则归 effect-accounting；recovery 规则归 crash-reconciliation。
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import {
  RULE_IDS,
  scanFiles,
  scanText,
} from '../../../scripts/checks/p0-recovery-join.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname

/** LOOP-006 桥接静态形状规则（degeneration-guard owner）。 */
const BRIDGE_RULES = new Set(['lifecycle-aborted-record', 'record-completion-single-owner'])

test('P0_RECOVERY_JOIN_GATE_exports_bridge_shape_rule_ids', () => {
  for (const id of BRIDGE_RULES) {
    assert.ok(RULE_IDS.includes(id), `missing bridge-shape rule id: ${id}`)
  }
})

test('P0_RECOVERY_JOIN_GATE_negative_lifecycle_aborted_record_goes_red', () => {
  // Abort 是控制面，不是业务数据：aborted 分支不得把 completion 记录进 journal。
  const source = [
    'module HostForkRunLifecycle',
    'match outcome with',
    '| Aborted reason ->',
    '    HandleController.recordCompletion journal parentId proof',
  ].join('\n')
  const hits = scanText(source, 'HostForkRunLifecycle.fs')
  assert.ok(
    hits.some((h) => h.id === 'lifecycle-aborted-record'),
    `expected lifecycle-aborted-record; got ${hits.map((h) => h.id).join(',')}`,
  )
})

test('P0_RECOVERY_JOIN_GATE_negative_record_completion_single_owner_goes_red', () => {
  // 只有 ChildRecoveryWorkflow 是 recordCompletion 的单一 owner；HostForkRunLifecycle
  // 直接调用即越权。
  const source = [
    'module HostForkRunLifecycle',
    'let deliver journal parentId proof =',
    '    HandleController.recordCompletion journal parentId proof',
  ].join('\n')
  const hits = scanText(source, 'HostForkRunLifecycle.fs')
  assert.ok(
    hits.some((h) => h.id === 'record-completion-single-owner'),
    `expected record-completion-single-owner; got ${hits.map((h) => h.id).join(',')}`,
  )
})

test('P0_RECOVERY_JOIN_GATE_record_completion_owner_allowlist_is_green', () => {
  const owner = [
    'module ChildRecoveryWorkflow',
    'let commitJoinable journal parentId proof =',
    '    HandleController.recordCompletion journal parentId proof',
  ].join('\n')
  const def = [
    'module HandleController',
    'let recordCompletion journal parentId completion =',
    '    Ok ()',
  ].join('\n')
  assert.equal(scanText(owner, 'ChildRecoveryWorkflow.fs').filter((h) => h.id === 'record-completion-single-owner').length, 0)
  assert.equal(scanText(def, 'HandleController.fs').filter((h) => h.id === 'record-completion-single-owner').length, 0)
})

test('P0_RECOVERY_JOIN_GATE_production_sources_are_green', () => {
  const files = [
    'src/Wanxiangshu/Session/HostForkRunLifecycle.fs',
    'src/Wanxiangshu/Session/ForkRecovery.fs',
    'src/Wanxiangshu/Session/HostForkRestart.fs',
    'src/Wanxiangshu/Session/ForkRuntime.fs',
    'src/Wanxiangshu/Session/HostForkRuntime.fs',
    'src/Wanxiangshu/Session/HostForkAgent.fs',
    'src/Wanxiangshu/Session/HandleController.fs',
    'src/Wanxiangshu/Session/AgentCompletion.fs',
    'src/Wanxiangshu/Session/HandleCompletionCodec.fs',
    'src/Wanxiangshu/Session/CompletionMailbox.fs',
    'src/Wanxiangshu/Session/JoinDrain.fs',
    'src/Wanxiangshu/Domain/ChildRecovery.fs',
    'src/Wanxiangshu/Execution/Delegation/Join.fs',
    'src/Wanxiangshu/Domain/SessionRecovery.fs',
    'src/Wanxiangshu/Kernel/Fact.fs',
    'src/Wanxiangshu/Execution/Delegation/ChildRecoveryWorkflow.fs',
    'src/Wanxiangshu/Execution/Session/SessionRecoveryWorkflow.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Plugin/SpikePlugin.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/JoinTool.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ExecutorTool.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/Distillation.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/DistillationRuntime.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Codec/JoinResultRenderer.fs',
  ]
  const entries = files.map((rel) => ({
    file: rel,
    text: readFileSync(join(ROOT, rel), 'utf8'),
  }))
  const hits = scanFiles(entries).filter((h) => BRIDGE_RULES.has(h.id))
  assert.deepEqual(
    hits,
    [],
    hits.map((h) => `${h.id}@${h.file}:${h.line}`).join('; '),
  )
})

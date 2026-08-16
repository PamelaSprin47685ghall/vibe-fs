// EFFECT-ACCOUNTING-005 contract test（本包 NEW）：
// 「先核对物理 effect identity，再决定重试」——崩溃/中断后处理 Requested-only 效果：
// 没有物理证据 → 保持 pending/等待（RecoveryIncomplete），绝不盲重试、绝不假完成。
// 只有 proven 的物理证据（snapshotTerminal）才产生 terminal。
//
// 各效果的具体 reconcile 算法归 domain 包；本文件钉「先证后重试」律本身。

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, childRecovery, handleId, sessionId } from '../../verification-system/tests/support/domain.mjs'

const AGENT = 'fast-coder'
const HANDLE = handleId.agent('h-reconcile')
const CHILD = sessionId('ses_child_reconcile')

test('WHAT[EFFECT-ACCOUNTING-005] requested_only_without_physical_evidence_stays_pending_not_blind_retry', () => {
  // durableActive（Requested-only，无 Accepted）+ snapshotMissing（无物理 terminal 证据）
  // → RecoveryIncomplete：未知 ≠ 未发生，不盲重试、不假完成。
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotMissing(),
    [],
  )
  assert.equal(caseOf(resolution), 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-005] outcome_unknown_without_physical_evidence_never_becomes_terminal', () => {
  // durableUnknown（连 Requested 都未见证）+ snapshotMissing → 同样保持等待；
  // 「函数没返回/没证据」不得被当作成功或失败。
  const resolution = childRecovery.resolveChild(
    childRecovery.durableUnknown(),
    childRecovery.snapshotMissing(),
    [],
  )
  assert.equal(caseOf(resolution), 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-005] terminal_issued_only_after_proven_physical_evidence', () => {
  // 只有证明效果已发生的物理证据（snapshotTerminal + proven body）才产生 terminal——
  // 证据门决定结果，不由计时器或猜测决定。
  const evidence = childRecovery.evidenceCompleted(AGENT, HANDLE, CHILD, '{"status":"ok"}')
  const proof = childRecovery.tryFromProvenTerminal(evidence)
  assert.equal(proof.ok, true)
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotTerminal(evidence),
    [],
  )
  assert.equal(caseOf(resolution), 'RecoveredTerminal')
})

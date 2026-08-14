// Split from tests/unit/execution/reconcile-idle-early.test.mjs (cutover Wave 2a);
// owner: causal-wait. 观测非权威面（CAUSAL-001 消费）：idle 信号先于 transcript
// 到达时，观测本身不构成 turn——只有 durable terminal transcript 经有界因果重读
// 物化后才 publish 一次（有界因果重读 machinery → host-boundary）。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  physicalUser,
  reconcileSupervisor,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'

// Causal rereads complete synchronously inside Kick's async task; one macrotask
// is enough for the promise chain. Short settle covers that hop.
const settle = () => new Promise((r) => setTimeout(r, 10))

// ── 1. terminal materializes inside the same Kick via causal rereads ─────────

test('EXEC_reconcile_idle_before_transcript_materializes_within_causal_rereads', async () => {
  const sid = sessionId('ses_idle_early_causal')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // Get#1 remaining=3 → Provisional → Reread(2)
  // Get#2 remaining=2 → Provisional → Reread(1)
  // Get#3 remaining=1 → Terminal → Publish
  const reads = [
    { ok: true, messages: inProgress },
    { ok: true, messages: inProgress },
    { ok: true, messages: terminal },
  ]
  const snapshot = reconcileSupervisor.createSnapshot(reads)
  const binding = reconcileSupervisor.createStore()
  const onTurn = (turn) => {
    turns.push(turn)
    return Promise.resolve()
  }
  const supervisor = reconcileSupervisor.create({
    snapshot,
    binding,
    onTurn,
    maxCausalRereads: 3,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  await settle()
  assert.equal(turns.length, 1, 'exactly one onTurn within same Kick')
  assert.equal(caseOf(turns[0].Outcome), 'TurnCompleted')
  assert.ok(snapshot.readCount >= 3, `expect ≥3 reads (initial + rereads); got ${snapshot.readCount}`)
})

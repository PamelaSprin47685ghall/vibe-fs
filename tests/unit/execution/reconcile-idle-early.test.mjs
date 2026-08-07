// Behavioral regression: idle signal arrives before transcript materializes
// (highest-probability completion-loss point). Bounded causal rereads within
// one Kick (maxCausalRereads); no wall-clock backoff/budget.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  physicalUser,
  reconcileSupervisor,
  sessionId,
} from '../support/domain.mjs'

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

// ── 2. causal rereads exhaust is not permanent loss; second Kick recovers ────

test('EXEC_reconcile_idle_early_then_second_signal_completes', async () => {
  const sid = sessionId('ses_idle_early_second')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // maxCausalRereads=2 → first Kick:
  //   Get#1 remaining=2 → Provisional → Reread(1)
  //   Get#2 remaining=1 → Provisional → Reread(0)
  //   Get#3 remaining=0 → Provisional → StopPass (Dirty held, no publish)
  // second Kick resets remaining=2:
  //   Get#4 remaining=2 → Terminal → Publish
  const reads = [
    { ok: true, messages: inProgress },
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
    maxCausalRereads: 2,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  await settle()
  assert.equal(snapshot.readCount, 3, `first Kick must consume 3 reads; got ${snapshot.readCount}`)
  const completedAfterFirst = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completedAfterFirst.length, 0, 'first Kick under exhausted rereads must not publish TurnCompleted')

  // Same supervisor: each Kick starts from maxCausalRereads again.
  reconcileSupervisor.kick(supervisor, sid)
  await settle()

  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 1, 'second Kick must surface exactly one TurnCompleted')
  assert.ok(snapshot.readCount >= 4, `need ≥4 reads total; got ${snapshot.readCount}`)
})

// ── 3. consecutive snapshot errors do not consume rereads; Ok resets ─────────

test('EXEC_reconcile_consecutive_errors_retry_until_ok_terminal', async () => {
  const sid = sessionId('ses_err_reread_reset')
  const physical = physicalUser('user-1')
  const turns = []
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // Error branch keeps rereadsRemaining unchanged and retries immediately.
  // Get#1..3 Error (no budget consume); Get#4 Terminal → Publish.
  const reads = [
    { ok: false, error: 'transient-1' },
    { ok: false, error: 'transient-2' },
    { ok: false, error: 'transient-3' },
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
  assert.equal(turns.length, 1, 'exactly one onTurn after errors then Ok terminal')
  assert.equal(caseOf(turns[0].Outcome), 'TurnCompleted')
  assert.ok(snapshot.readCount >= 4, `need ≥4 reads (3 errors + terminal); got ${snapshot.readCount}`)
})

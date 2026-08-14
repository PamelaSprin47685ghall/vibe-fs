// Split from tests/unit/execution/reconcile-idle-early.test.mjs (cutover Wave 2a);
// owner: host-boundary. Reconciler 有界因果重读 machinery（HOST-BOUNDARY-005）：
// reread 预算耗尽不是永久丢失（第二次 Kick 恢复）、SnapshotError 不消耗预算、
// 连续错误有界 StopPass。idle-observation 非权威面（idle 先于 transcript 到达）
// → causal-wait。

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

// ── 4. persistent SnapshotError: consecutive-error cap ends the pass ─────────
// Guards materializeActive Error branch against unbounded async recursion when
// snapshot stays ok:false (production path never builds SnapshotError evidence;
// GetMessages Error recurses outside decideStep). Default maxConsecutiveErrors=5.

test('EXEC_reconcile_persistent_errors_stop_pass_bounded', async () => {
  const sid = sessionId('ses_err_bounded')
  const physical = physicalUser('user-1')
  const turns = []
  // Single Error entry repeats forever → continuous SnapshotError path.
  const snapshot = reconcileSupervisor.createSnapshot([{ ok: false, error: 'persistent' }])
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
  const readsAfterPass = snapshot.readCount
  assert.equal(
    readsAfterPass,
    5,
    `persistent errors must stop after default maxConsecutiveErrors (5); got ${readsAfterPass}`,
  )
  await settle()
  assert.equal(
    snapshot.readCount,
    readsAfterPass,
    'StopPass: no further reads without a new host signal',
  )
  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 0, 'persistent SnapshotError must not publish TurnCompleted')
})

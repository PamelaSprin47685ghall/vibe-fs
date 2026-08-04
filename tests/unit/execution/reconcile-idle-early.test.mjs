// Behavioral regression: idle signal arrives before transcript materializes
// (highest-probability completion-loss point). HOST-004 causal budget = 3 Ok reads;
// error budget independent of causal attempt; second Kick re-runs after budget exhaust.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  physicalUser,
  reconcileSupervisor,
  sessionId,
} from '../support/domain.mjs'

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// Keep timer-driven waits under PER_TEST_TIMEOUT_MS (1000). Long delays suppress
// auto re-Kick so tests can inject a second SessionIdle Kick explicitly.
const SUPPRESS_REKICK_MS = [10_000]
const TEST_REKICK_DELAYS_MS = [5, 5, 5, 5, 5]

async function waitUntil(predicate, timeoutMs, stepMs = 10) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    if (predicate()) return true
    await sleep(stepMs)
  }
  return predicate()
}

// ── 1. terminal on 3rd causal Ok read (within HOST-004 budget, same Kick) ────

test('EXEC_reconcile_idle_before_transcript_materializes_within_causal_budget', async () => {
  const sid = sessionId('ses_idle_early_causal')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // Reads 1–2: in-progress (idle arrived before transcript); read 3: terminal.
  // All within the same Kick's 3-attempt HOST-004 causal budget.
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
    reKickDelaysMs: TEST_REKICK_DELAYS_MS,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  const ok = await waitUntil(() => turns.length > 0, 400)
  assert.equal(ok, true, 'onTurn must fire within causal budget')
  assert.equal(turns.length, 1, 'exactly one onTurn')
  assert.equal(caseOf(turns[0].Outcome), 'TurnCompleted')
  // 3 causal Ok reads in one pass; no delayed re-Kick required.
  assert.ok(
    snapshot.readCount >= 3 && snapshot.readCount <= 4,
    `expect 3 causal reads (optionally +1 re-Kick race); got ${snapshot.readCount}`,
  )
})

// ── 2. 3-budget exhaust is not permanent loss; second SessionIdle Kick recovers ─

test('EXEC_reconcile_idle_early_then_second_signal_completes', async () => {
  const sid = sessionId('ses_idle_early_second')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // First Kick: 3 in-progress → causal budget exhausted, no TurnCompleted.
  // Auto re-Kick suppressed (long delay). Second Kick (new SessionIdle): terminal.
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
    reKickDelaysMs: SUPPRESS_REKICK_MS,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  // First pass must exhaust 3 causal Ok reads without publishing TurnCompleted.
  const firstPass = await waitUntil(() => snapshot.readCount >= 3, 400)
  assert.equal(firstPass, true, 'first Kick must consume 3 causal reads')
  await sleep(20)
  const completedAfterFirst = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completedAfterFirst.length, 0, 'first Kick must not publish TurnCompleted')

  // Second SessionIdle signal → fresh Kick re-runs with remaining snapshot script.
  reconcileSupervisor.kick(supervisor, sid)

  const ok = await waitUntil(
    () => turns.some((t) => caseOf(t.Outcome) === 'TurnCompleted'),
    400,
  )
  assert.equal(ok, true, 'second Kick must surface TurnCompleted')
  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 1, 'exactly one TurnCompleted total (no duplicate)')
  assert.ok(snapshot.readCount >= 4, `need ≥4 reads (3+1); got ${snapshot.readCount}`)
})

// ── 3. error budget is per-pass; Ok on next Kick still publishes ──────────────

test('EXEC_reconcile_consecutive_errors_give_up_after_three_but_ok_resets', async () => {
  const sid = sessionId('ses_err_budget_reset')
  const physical = physicalUser('user-1')
  const turns = []
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // First Kick: 3 consecutive Errors → errorCount budget ends pass without onTurn.
  // Second Kick: Ok terminal → onTurn (error budget is not sticky across passes).
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
    reKickDelaysMs: SUPPRESS_REKICK_MS,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  const firstPass = await waitUntil(() => snapshot.readCount >= 3, 400)
  assert.equal(firstPass, true, 'first Kick must hit 3 Errors')
  await sleep(20)
  assert.equal(turns.length, 0, '3 consecutive Errors must end pass without onTurn')

  reconcileSupervisor.kick(supervisor, sid)

  const ok = await waitUntil(() => turns.length > 0, 400)
  assert.equal(ok, true, 'second Kick with Ok terminal must fire onTurn (error budget per-pass)')
  assert.equal(turns.length, 1, 'exactly one onTurn')
  assert.equal(caseOf(turns[0].Outcome), 'TurnCompleted')
})

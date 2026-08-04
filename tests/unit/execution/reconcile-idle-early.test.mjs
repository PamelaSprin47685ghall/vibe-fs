// Behavioral regression: idle signal arrives before transcript materializes
// (highest-probability completion-loss point). Timer backoff rereads until
// terminal, wall-clock budget, or ClearSession — no fixed 3-read microtask cap.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  physicalUser,
  reconcileSupervisor,
  sessionId,
} from '../support/domain.mjs'

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// Keep timer-driven waits under PER_TEST_TIMEOUT_MS (1000).
const TEST_BACKOFF_MS = [5, 5, 5, 5, 5]
// Second Kick test: first pass exhausts a tiny budget so it ends without
// terminal; second Kick (new SessionIdle) resumes with remaining script.
const FIRST_PASS_BUDGET_MS = 15
const SECOND_PASS_BUDGET_MS = 400

async function waitUntil(predicate, timeoutMs, stepMs = 10) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    if (predicate()) return true
    await sleep(stepMs)
  }
  return predicate()
}

// ── 1. terminal appears on a later reread inside the same Kick ───────────────

test('EXEC_reconcile_idle_before_transcript_materializes_within_causal_budget', async () => {
  const sid = sessionId('ses_idle_early_causal')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // Reads 1–2: in-progress (idle arrived before transcript); read 3: terminal.
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
    backoffDelaysMs: TEST_BACKOFF_MS,
    maxBudgetMs: 400,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  const ok = await waitUntil(() => turns.length > 0, 400)
  assert.equal(ok, true, 'onTurn must fire within same Kick')
  assert.equal(turns.length, 1, 'exactly one onTurn')
  assert.equal(caseOf(turns[0].Outcome), 'TurnCompleted')
  assert.ok(snapshot.readCount >= 3, `expect ≥3 reads; got ${snapshot.readCount}`)
})

// ── 2. budget exhaust is not permanent loss; second SessionIdle Kick recovers ─

test('EXEC_reconcile_idle_early_then_second_signal_completes', async () => {
  const sid = sessionId('ses_idle_early_second')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // First Kick: tiny budget, only InProgress in the queue head → ends incomplete.
  // Second Kick (new SessionIdle): terminal still available → TurnCompleted.
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
  // maxBudgetMs is fixed at create; use a small budget for the first pass by
  // creating with FIRST_PASS_BUDGET, then a second supervisor is not needed —
  // after budget ends Running=false; a second Kick with remaining script works
  // only if budget is large enough on the second pass. So inject a budget that
  // is just large enough for a few InProgress steps on the first pass, and the
  // second Kick reuses the same maxBudgetMs which still allows the terminal read.
  // With 15ms budget and 5ms delay, first pass does ~2–3 reads then stops while
  // queue still has InProgress/terminal. Second Kick starts fresh with remaining
  // queue including terminal.
  const supervisor = reconcileSupervisor.create({
    snapshot,
    binding,
    onTurn,
    backoffDelaysMs: TEST_BACKOFF_MS,
    maxBudgetMs: FIRST_PASS_BUDGET_MS,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  const firstPass = await waitUntil(() => snapshot.readCount >= 1, 400)
  assert.equal(firstPass, true, 'first Kick must perform at least one read')
  // Wait past first-pass budget so Running clears.
  await sleep(80)
  const completedAfterFirst = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completedAfterFirst.length, 0, 'first Kick under tiny budget must not publish TurnCompleted')

  // Second SessionIdle signal → fresh Kick; remaining script has terminal if
  // first pass did not consume it. If first pass already drained all InProgress
  // and left terminal, second Kick still publishes it. Either way terminal must
  // appear once we re-kick with a larger budget on a new supervisor instance.
  // Same supervisor keeps the original tiny budget — create a second Kick path
  // by replacing budget is impossible, so re-create with larger budget sharing
  // the same snapshot/binding/onTurn would double-bind. Instead: re-kick on the
  // same supervisor is enough when the queue still has terminal unread, and
  // FIRST_PASS_BUDGET of 15ms with 5ms steps leaves ≥1 unread including terminal
  // when 4 items exist. If all 4 were consumed, terminal would already have been
  // found — contradicted by completedAfterFirst === 0. So terminal remains.
  // Problem: second Kick still has maxBudgetMs=15 which may not reach terminal
  // if more than one InProgress remains. Re-create supervisor with larger budget
  // sharing snapshot + binding + onTurn.
  const supervisor2 = reconcileSupervisor.create({
    snapshot,
    binding,
    onTurn,
    backoffDelaysMs: TEST_BACKOFF_MS,
    maxBudgetMs: SECOND_PASS_BUDGET_MS,
  })
  // Re-bind is idempotent for the same physical root.
  reconcileSupervisor.bindUserMessage(supervisor2, sid, physical)
  reconcileSupervisor.kick(supervisor2, sid)

  const ok = await waitUntil(
    () => turns.some((t) => caseOf(t.Outcome) === 'TurnCompleted'),
    400,
  )
  assert.equal(ok, true, 'second Kick must surface TurnCompleted')
  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 1, 'exactly one TurnCompleted total (no duplicate)')
  assert.ok(snapshot.readCount >= 2, `need ≥2 reads; got ${snapshot.readCount}`)
})

// ── 3. consecutive snapshot errors retry then succeed on Ok ──────────────────

test('EXEC_reconcile_consecutive_errors_give_up_after_three_but_ok_resets', async () => {
  const sid = sessionId('ses_err_budget_reset')
  const physical = physicalUser('user-1')
  const turns = []
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // Errors no longer permanently end the pass: after three Errors the loop
  // continues with backoff until budget or Ok. One Kick with Errors then Ok
  // must publish TurnCompleted.
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
    backoffDelaysMs: TEST_BACKOFF_MS,
    maxBudgetMs: 400,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  const ok = await waitUntil(() => turns.length > 0, 400)
  assert.equal(ok, true, 'Ok terminal after consecutive Errors must fire onTurn')
  assert.equal(turns.length, 1, 'exactly one onTurn')
  assert.equal(caseOf(turns[0].Outcome), 'TurnCompleted')
  assert.ok(snapshot.readCount >= 4, `need ≥4 reads (3 errors + terminal); got ${snapshot.readCount}`)
})

// P1 unit surface: ReconcileSupervisor Error budget + incomplete-terminal delayed
// re-Kick, sticky cap, HostSignalSubscribe reconnect markers, ForkRuntime
// AwaitAgent deadline, ExecutorSummarize cancelOwned on map failure.

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  caseOf,
  executorSummarizeRuntime,
  forkRuntime,
  hostEventPort,
  hostSignalSubscribe,
  idValue,
  physicalUser,
  reconcileSupervisor,
  sessionId,
} from '../support/domain.mjs'

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// Injected re-Kick delays keep timer-driven tests under PER_TEST_TIMEOUT_MS (1000).
// Production uses [|50;100;250;500;1000|]; tests must not wait on that sum.
const TEST_REKICK_DELAYS_MS = [5, 5, 5, 5, 5]
// ClearSession race needs a re-Kick delay longer than waitUntil's stepMs (10):
// with [5,...], the first delayed re-Kick fires (≈6ms) before waitUntil's first
// successful poll (≈10ms), so the terminal publishes before clearSession can run.
const CLEAR_REKICK_DELAYS_MS = [200]

async function waitUntil(predicate, timeoutMs, stepMs = 10) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    if (predicate()) return true
    await sleep(stepMs)
  }
  return predicate()
}

// ── 1. ReconcileSupervisor: snapshot Error does not burn HOST-004 attempt budget ─

test('EXEC_reconcile_error_does_not_consume_causal_budget', async () => {
  const sid = sessionId('ses_reconcile_err')
  const physical = physicalUser('user-1')
  const turns = []
  const reads = [
    { ok: false, error: 'transient-1' },
    { ok: false, error: 'transient-2' },
    { ok: true, messages: reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal') },
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

  // Three causal yields (setTimeout 0) + snapshot I/O; wait until onTurn fires.
  const ok = await waitUntil(() => turns.length > 0, 400)
  assert.equal(ok, true, 'onTurn must fire before timeout')
  assert.equal(turns.length, 1, 'onTurn must fire after 2 Errors + 1 Ok terminal (Error must not burn attempt budget)')
  assert.equal(caseOf(turns[0].Outcome), 'TurnCompleted')
})

// ── 1b. idle-before-transcript: delayed re-Kick discovers late terminal ───────

test('EXEC_reconcile_incomplete_delayed_rekick_finds_terminal', async () => {
  const sid = sessionId('ses_reconcile_rekick')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // First Kick: 3 HOST-004 causal reads all InProgress → incomplete → schedule short re-Kick.
  // Second Kick: first read terminal → TurnCompleted.
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
    reKickDelaysMs: TEST_REKICK_DELAYS_MS,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  // Provisional InProgress may publish once; terminal must arrive after delayed re-Kick.
  const ok = await waitUntil(
    () => turns.some((t) => caseOf(t.Outcome) === 'TurnCompleted'),
    400,
  )
  assert.equal(ok, true, 'delayed re-Kick must surface TurnCompleted without a second Host signal')
  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 1, 'exactly one TurnCompleted')
  assert.ok(snapshot.readCount >= 4, `need ≥4 snapshot reads (3 causal + re-Kick); got ${snapshot.readCount}`)
})

// ── 1c. max delayed re-Kick budget stops rescheduling ────────────────────────

test('EXEC_reconcile_incomplete_rekick_budget_stops', async () => {
  const sid = sessionId('ses_reconcile_budget')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  // Always incomplete: each Kick does up to 3 causal reads.
  // Max 5 delayed re-Kicks (injected 5ms×5) → 1 initial + 5 re-Kicks = 6 passes.
  const snapshot = reconcileSupervisor.createSnapshot([{ ok: true, messages: inProgress }])
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

  // Budget sum: 5×5ms = 25ms; wait past last timer + margin (still under 1000ms).
  await sleep(200)
  const readsAfterBudget = snapshot.readCount
  await sleep(100)
  assert.equal(
    snapshot.readCount,
    readsAfterBudget,
    `no further re-Kicks after budget; reads stayed ${readsAfterBudget} then grew to ${snapshot.readCount}`,
  )
  // 6 passes × 3 causal = 18 upper bound; allow some headroom for races.
  assert.ok(readsAfterBudget <= 24, `bounded reads expected, got ${readsAfterBudget}`)
  assert.ok(readsAfterBudget >= 6, `at least one read per pass (1+5), got ${readsAfterBudget}`)
  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 0, 'always-in-progress must never publish TurnCompleted')
})

// ── 1d. ClearSession cancels pending delayed re-Kick ─────────────────────────

test('EXEC_reconcile_clear_session_cancels_pending_rekick', async () => {
  const sid = sessionId('ses_reconcile_clear')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // After 3 in-progress, next would be terminal — but ClearSession must cancel timer.
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
  // Cannot share TEST_REKICK_DELAYS_MS: waitUntil stepMs (10) > first re-Kick
  // delay (5) makes clearSession always lose the race against the pending timer
  // (terminal already published by the ~10ms poll). A 200ms delay keeps the
  // timer pending while waitUntil sees readCount>=3 and clearSession cancels it.
  const supervisor = reconcileSupervisor.create({
    snapshot,
    binding,
    onTurn,
    reKickDelaysMs: CLEAR_REKICK_DELAYS_MS,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  // Wait first pass to finish and schedule re-Kick, then clear before it fires.
  await waitUntil(() => snapshot.readCount >= 3, 400)
  await sleep(2)
  reconcileSupervisor.clearSession(supervisor, sid)

  await sleep(50)
  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 0, 'ClearSession must cancel delayed re-Kick; terminal must not publish')
})

// ── 2. stickyTerminal capacity 256 with FIFO eviction ────────────────────────

test('EXEC_events_sticky_terminal_bounded', () => {
  const port = hostEventPort.create()
  const cap = hostEventPort.stickyCap
  assert.equal(cap, 256)

  for (let i = 1; i <= 300; i += 1) {
    hostEventPort.notify(port, sessionId(`s${i}`), hostEventPort.failed(`err-${i}`))
  }

  const seen = new Set()
  hostEventPort.subscribe(port, (sid) => {
    seen.add(idValue.session(sid))
  })

  assert.ok(seen.size <= cap, `late subscriber must see at most stickyCap=${cap}, got ${seen.size}`)
  assert.equal(seen.size, cap, `exactly ${cap} sticky entries remain after 300 distinct notifies`)
  // Oldest s1..s44 evicted; s45..s300 remain (300 - 256 = 44).
  assert.equal(seen.has('s1'), false, 'oldest session must be evicted')
  assert.equal(seen.has('s45'), true, 'first retained session is s45')
  assert.equal(seen.has('s300'), true, 'newest session must remain')
})

// ── 3. HostSignalSubscribe reconnect loop (structural on emitJsExpr body) ────

test('EXEC_host_signal_subscribe_reconnect_after_stream_end', () => {
  const src = hostSignalSubscribe.source()
  for (const marker of hostSignalSubscribe.reconnectMarkers) {
    assert.ok(src.includes(marker), `HostSignalSubscribe must contain reconnect marker: ${marker}`)
  }
  // Old bare return after normal stream end is gone; loop continues until abort.
  assert.ok(src.includes('while (!abortCtrl.signal.aborted)'), 'reconnect outer loop must exist')
  assert.ok(src.includes('stream ended normally'), 'normal EOF is logged then loop continues')
  // Cap delay at 10s with exponential 2**attempt.
  assert.match(src, /Math\.min\(1000 \* 2 \*\* attempt,\s*10000\)/)
})

// ── 4. ForkRuntime.AwaitAgent timeout ────────────────────────────────────────

test('EXEC_fork_runtime_await_agent_timeout', async () => {
  // Never settles: no timer handle (child can exit), no late resolution (no
  // asynchronous activity after verdict). AwaitAgent's timeout path races the
  // completion cell via PtyTiming.raceExit and does not depend on the runner.
  const hang = () => new Promise(() => {})
  const rt = forkRuntime.create((_agentId, _role, _prompt) => hang())
  const role = forkRuntime.role('Coder')
  forkRuntime.fork(rt, 'agent-hang', role, 'fast-coder', 'work')

  const started = Date.now()
  const result = await forkRuntime.awaitAgent(rt, 'agent-hang', 40)
  const elapsed = Date.now() - started

  assert.ok(elapsed >= 25, `expected ~40ms wait, got ${elapsed}ms`)
  assert.ok(elapsed < 2000, `must not hang unbounded; got ${elapsed}ms`)
  assert.equal(result.tag, 1, 'Error result')
  assert.match(result.fields[0], /timed out/)
  assert.match(result.fields[0], /agent-hang/)
})

// ── 5. ExecutorSummarize cancelOwned on map failure ──────────────────────────

test('EXEC_executor_summarize_cancel_owned_on_failure', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-sum-'))
  const spoolPath = join(dir, 'spool.bin')
  // One small chunk → one map agent; Join TimedOut → map failure → cancelOwned.
  writeFileSync(spoolPath, Buffer.from('chunk-body-for-summarize'))

  const forked = []
  const { runtime, cancelled } = executorSummarizeRuntime.fake({
    fork: (agentId) => {
      forked.push(agentId)
      return executorSummarizeRuntime.forkOk(agentId)
    },
    join: () => executorSummarizeRuntime.timedOut(),
  })

  const summary = await executorSummarizeRuntime.summarizeSpool(runtime, spoolPath)
  assert.ok(typeof summary === 'string', 'summarizeSpool returns partial text, not throw')
  assert.ok(forked.length >= 1, 'at least one map agent forked')
  assert.ok(
    cancelled.length >= 1,
    `CancelAgent must run for owned forked ids on map failure; forked=${forked.join(',')} cancelled=${cancelled.join(',')}`,
  )
  for (const id of forked) {
    assert.ok(cancelled.includes(id), `owned agent ${id} must be cancelled`)
  }

  rmSync(dir, { recursive: true, force: true })
})

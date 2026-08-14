// Split from tests/unit/execution/reconcile-supervisor.test.mjs (cutover Wave 2a);
// owner: host-boundary. P1 unit surface：ReconcileSupervisor 因果重读 materialization、
// ClearSession mid-pass、generation fence（HOST-BOUNDARY-005）；HostSignalSubscribe
// 传输选择（HOST-BOUNDARY-002/003）；HostEventPort sticky 容量 256
// （HOST-BOUNDARY-016）。ForkRuntime AwaitAgent deadline → delegation；
// Distillation cancelOwned → output-distillation。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  hostEventPort,
  hostSignalSubscribe,
  idValue,
  isNone,
  physicalUser,
  reconcileSupervisor,
  resultOf,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
const settle = () => new Promise((r) => setTimeout(r, 10))

async function waitUntil(predicate, timeoutMs, stepMs = 10) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    if (predicate()) return true
    await sleep(stepMs)
  }
  return predicate()
}

// ── 1. Snapshot Error does not permanently end the pass ─────────────────────

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
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  const ok = await waitUntil(() => turns.length > 0, 400)
  assert.equal(ok, true, 'onTurn must fire before timeout')
  assert.equal(turns.length, 1, 'onTurn must fire after 2 Errors + 1 Ok terminal')
  assert.equal(caseOf(turns[0].Outcome), 'TurnCompleted')
})

// ── 1b. idle-before-transcript: causal rereads find late terminal ────────────

test('EXEC_reconcile_incomplete_delayed_rekick_finds_terminal', async () => {
  const sid = sessionId('ses_reconcile_rekick')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // Reads 1–3: InProgress; read 4: terminal — all inside one Kick's causal rereads.
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
    maxCausalRereads: 3,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  const ok = await waitUntil(
    () => turns.some((t) => caseOf(t.Outcome) === 'TurnCompleted'),
    400,
  )
  assert.equal(ok, true, 'causal rereads must surface TurnCompleted without a second Host signal')
  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 1, 'exactly one TurnCompleted')
  assert.ok(snapshot.readCount >= 4, `need ≥4 snapshot reads; got ${snapshot.readCount}`)
})

// ── 1c. causal rereads exhausted stops always-incomplete transcripts ─────────

test('EXEC_reconcile_incomplete_rereads_exhausted_stops', async () => {
  const sid = sessionId('ses_reconcile_rereads')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const snapshot = reconcileSupervisor.createSnapshot([{ ok: true, messages: inProgress }])
  const binding = reconcileSupervisor.createStore()
  const onTurn = (turn) => {
    turns.push(turn)
    return Promise.resolve()
  }
  // maxCausalRereads=3 → initial remaining=4 → 4 reads then StopPass.
  const maxCausalRereads = 3
  const supervisor = reconcileSupervisor.create({
    snapshot,
    binding,
    onTurn,
    maxCausalRereads,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  await settle()
  const readsAfterPass = snapshot.readCount
  assert.equal(
    readsAfterPass,
    maxCausalRereads + 1,
    `causal rereads exhausted at maxCausalRereads+1; got ${readsAfterPass}`,
  )
  await settle()
  assert.equal(
    snapshot.readCount,
    readsAfterPass,
    'StopPass: no further reads without a new host signal',
  )
  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 0, 'always-in-progress must never publish TurnCompleted')
})

// ── 1d. ClearSession during causal reread stops later publish ────────────────

test('EXEC_reconcile_clear_session_cancels_pending_rekick', async () => {
  const sid = sessionId('ses_reconcile_clear')
  const physical = physicalUser('user-1')
  const turns = []
  const inProgress = reconcileSupervisor.inProgressTranscript('user-1', 'asst-ip')
  const terminal = reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal')
  // First read InProgress → clear on next read so later terminal is never published.
  const reads = [
    { ok: true, messages: inProgress },
    { ok: true, messages: inProgress },
    { ok: true, messages: terminal },
  ]
  let supervisor
  const snapshot = reconcileSupervisor.createSnapshot(reads, (readCount) => {
    // Clear mid-pass: after first read, generation bumps; terminal must never publish.
    if (readCount === 1) reconcileSupervisor.clearSession(supervisor, sid)
  })
  const binding = reconcileSupervisor.createStore()
  supervisor = reconcileSupervisor.create({
    snapshot,
    binding,
    onTurn: (turn) => {
      turns.push(turn)
      return Promise.resolve()
    },
    maxCausalRereads: 3,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  await new Promise((r) => setTimeout(r, 20))
  const completed = turns.filter((t) => caseOf(t.Outcome) === 'TurnCompleted')
  assert.equal(completed.length, 0, 'ClearSession mid-reread must prevent terminal publish')
})

test('EXEC_reconcile_on_turn_failure_is_not_sealed_and_later_wake_retries_once', async () => {
  const sid = sessionId('ses_reconcile_retry_publish')
  const physical = physicalUser('user-1')
  const attempts = []
  const snapshot = reconcileSupervisor.createSnapshot([
    { ok: true, messages: reconcileSupervisor.terminalTranscript('user-1', 'asst-terminal') },
  ])
  const binding = reconcileSupervisor.createStore()
  const onTurn = (turn) => {
    attempts.push(turn)
    return attempts.length === 1 ? Promise.reject(new Error('throw-once')) : Promise.resolve()
  }
  const supervisor = reconcileSupervisor.create({
    snapshot,
    binding,
    onTurn,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  assert.equal(await waitUntil(() => attempts.length === 1, 300), true, 'first onTurn must be attempted')
  await sleep(10)
  reconcileSupervisor.kick(supervisor, sid)
  assert.equal(await waitUntil(() => attempts.length === 2, 300), true, 'later wake must retry unsealed turn')
  await sleep(10)
  assert.equal(attempts.length, 2, 'successful retry seals exactly once')
})

test('EXEC_reconcile_clear_rebind_drops_old_delayed_turn_and_runs_new_binding', async () => {
  const sid = sessionId('ses_reconcile_generation_fence')
  const oldPhysical = physicalUser('old-user')
  const newPhysical = physicalUser('new-user')
  const turns = []
  const snapshot = reconcileSupervisor.createSnapshot([
    { ok: true, messages: reconcileSupervisor.inProgressTranscript('old-user', 'asst-old-ip') },
    { ok: true, messages: reconcileSupervisor.terminalTranscript('new-user', 'asst-new-terminal') },
  ])
  const binding = reconcileSupervisor.createStore()
  const supervisor = reconcileSupervisor.create({
    snapshot,
    binding,
    onTurn: (turn) => {
      turns.push(turn)
      return Promise.resolve()
    },
    maxCausalRereads: 3,
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, oldPhysical)
  reconcileSupervisor.kick(supervisor, sid)
  assert.equal(await waitUntil(() => snapshot.readCount >= 1, 400), true, 'old pass must start materializing')

  reconcileSupervisor.clearSession(supervisor, sid)
  reconcileSupervisor.bindUserMessage(supervisor, sid, newPhysical)
  reconcileSupervisor.kick(supervisor, sid)

  assert.equal(await waitUntil(() => turns.length === 1, 400), true, 'new generation must complete')
  assert.equal(turns.length, 1, 'old delayed generation must never publish')
  assert.equal(idValue.physicalUser(turns[0].PhysicalUserMessageId), 'new-user')
})

test('EXEC_reconcile_clear_rebind_fences_post_on_turn_effects_from_old_binding', async () => {
  const sid = sessionId('ses_reconcile_inflight_generation_fence')
  const oldPhysical = physicalUser('old-user')
  const newPhysical = physicalUser('new-user')
  const turns = []
  const observed = []
  let resolveOldTurn
  const oldTurnFinished = new Promise((resolve) => {
    resolveOldTurn = resolve
  })
  const snapshot = reconcileSupervisor.createSnapshot([
    { ok: true, messages: reconcileSupervisor.terminalTranscript('old-user', 'asst-old-terminal') },
    { ok: true, messages: reconcileSupervisor.terminalTranscript('new-user', 'asst-new-terminal') },
  ])
  const binding = reconcileSupervisor.createStore()
  const supervisor = reconcileSupervisor.create({
    snapshot,
    binding,
    onTurn: (turn) => {
      turns.push(turn)
      return idValue.physicalUser(turn.PhysicalUserMessageId) === 'old-user'
        ? oldTurnFinished
        : Promise.resolve()
    },
    onSnapshot: (_session, messages) => {
      observed.push(messages)
      return Promise.resolve()
    },
  })
  reconcileSupervisor.bindUserMessage(supervisor, sid, oldPhysical)
  reconcileSupervisor.kick(supervisor, sid)
  assert.equal(await waitUntil(() => turns.length === 1, 400), true, 'old onTurn must have started')
  assert.equal(idValue.physicalUser(turns[0].PhysicalUserMessageId), 'old-user')

  reconcileSupervisor.clearSession(supervisor, sid)
  reconcileSupervisor.bindUserMessage(supervisor, sid, newPhysical)
  reconcileSupervisor.kick(supervisor, sid)
  assert.equal(await waitUntil(() => turns.length === 2, 400), true, 'new generation must complete')
  assert.equal(idValue.physicalUser(turns[1].PhysicalUserMessageId), 'new-user')
  assert.equal(observed.length, 1, 'only the new generation may observe after publication')

  resolveOldTurn()
  await Promise.resolve()
  await Promise.resolve()
  assert.equal(observed.length, 1, 'resolving stale onTurn must not observe or seal the old pass')
  assert.equal(turns.length, 2, 'resolving stale onTurn must not continue or republish the old pass')

  reconcileSupervisor.kick(supervisor, sid)
  await Promise.resolve()
  await Promise.resolve()
  assert.equal(turns.length, 2, 'new generation seal must prevent duplicate publication')
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

// ── 3. HostSignalSubscribe transport selection (local-event-hook default) ────

test('HOST_signal_subscribe_defaults_to_local_event_hook', async () => {
  const result = await hostSignalSubscribe.trySubscribe({ serverUrl: 'http://localhost:4096', client: null }, () => {})
  const decoded = resultOf(result)
  assert.equal(decoded.ok, true, 'local-event-hook must succeed with zero network probes')
  const [subscription, source] = decoded.value
  assert.ok(isNone(subscription), 'no subscription object needed for in-process hook')
  assert.equal(source, 'local-event-hook', 'local signals arrive through the Host event hook')
})

test('HOST_signal_subscribe_embedded_uses_legacy_listen_when_present', async () => {
  const fakeListen = { call: () => ({ disposed: false }) }
  const result = await hostSignalSubscribe.trySubscribe(
    { serverUrl: 'http://localhost:4096', client: null, events: { listen: fakeListen } },
    () => {},
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, true)
  const [subscription, source] = decoded.value
  assert.ok(subscription, 'legacy events.listen is used when explicitly provided')
  assert.equal(source, 'events.listen')
})

test('HOST_signal_subscribe_bad_listener_fails_closed', async () => {
  const result = await hostSignalSubscribe.trySubscribe(
    { events: { listen: () => null } },
    () => {},
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, false, 'broken events.listen must fail closed')
})

test('HOST_signal_subscribe_client_events_listen_supported', async () => {
  const fakeListen = { call: () => ({ disposed: false }) }
  const result = await hostSignalSubscribe.trySubscribe(
    { client: { events: { listen: fakeListen } } },
    () => {},
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, true)
  assert.equal(decoded.value[1], 'events.listen')
})

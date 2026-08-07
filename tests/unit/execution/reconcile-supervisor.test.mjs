// P1 unit surface: ReconcileSupervisor causal reread materialization,
// ClearSession mid-pass, HostSignalSubscribe reconnect markers,
// ForkRuntime AwaitAgent deadline, ExecutorSummarize cancelOwned on map failure.

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  caseOf,
  diagnostic,
  executorSummarizeRuntime,
  forkRuntime,
  hostEventPort,
  hostSignalSubscribe,
  idValue,
  isNone,
  physicalUser,
  reconcileSupervisor,
  resultOf,
  sessionId,
} from '../support/domain.mjs'

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

// ── 3. HostSignalSubscribe reconnect loop (structural on emitJsExpr body) ────

test('EXEC_host_signal_subscribe_reconnect_after_stream_end', () => {
  const src = hostSignalSubscribe.source()
  for (const marker of hostSignalSubscribe.reconnectMarkers) {
    assert.ok(src.includes(marker), `HostSignalSubscribe must contain reconnect marker: ${marker}`)
  }
  // Old bare return after normal stream end is gone; loop continues until disposed.
  assert.ok(src.includes('while (!state.disposed)'), 'reconnect outer loop must exist')
  assert.ok(src.includes('stream ended normally'), 'normal EOF is logged then loop continues')
  // Cap delay at 10s with exponential 2**attempt.
  assert.match(src, /Math\.min\(1000 \* 2 \*\* attempt,\s*10000\)/)
})

// ── 3b. HostSignalSubscribe transport selection (embedded-mode degradation) ──

// The SDK SSE client streams through the global fetch, never through the
// in-process custom fetch Host injects for other client calls. Selection must
// therefore probe whether a real HTTP listener answers the server URL; the
// probe is the only discriminator that survives a serve on any port (e2e runs
// the real server on 4096). Tests mock globalThis.fetch so no test touches
// the network.
const withFetch = async (impl, fn) => {
  const real = globalThis.fetch
  globalThis.fetch = impl
  try {
    return await fn()
  } finally {
    globalThis.fetch = real
  }
}
const refused = async () => {
  throw new Error('ECONNREFUSED')
}
const answers = async () => ({ ok: true, json: async () => ({ healthy: true }) })
const answersV2 = async () => ({ ok: true, json: async () => ({ healthy: true, pid: 1 }) })
const answersNonOpencode = async () => ({ ok: true, json: async () => ({ status: 'ok', app: 'random' }) })

test('HOST_signal_subscribe_embedded_fallback_degrades_to_local_event_hook', async () => {
  const result = await withFetch(refused, () =>
    hostSignalSubscribe.trySubscribe({ serverUrl: 'http://localhost:4096', client: null }, () => {}),
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, true, 'embedded mode must not hard-fail the plugin')
  const [subscription, source] = decoded.value
  assert.ok(isNone(subscription), 'no SSE subscription against a dead fallback address')
  assert.equal(source, 'local-event-hook', 'local signals arrive through the Host event hook')
})

test('HOST_signal_subscribe_non_opencode_server_degrades_to_local_event_hook', async () => {
  const result = await withFetch(answersNonOpencode, () =>
    hostSignalSubscribe.trySubscribe({ serverUrl: 'http://localhost:4096', client: null }, () => {}),
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, true, 'random non-OpenCode HTTP listener must degrade to local hook')
  const [subscription, source] = decoded.value
  assert.ok(isNone(subscription), 'no SSE subscription against a non-OpenCode HTTP server')
  assert.equal(source, 'local-event-hook')
})

test('HOST_signal_subscribe_embedded_uses_legacy_listen_when_present', async () => {
  const fakeListen = { call: () => ({ disposed: false }) }
  const result = await withFetch(refused, () =>
    hostSignalSubscribe.trySubscribe(
      { serverUrl: 'http://localhost:4096', client: null, events: { listen: fakeListen } },
      () => {},
    ),
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, true)
  const [subscription, source] = decoded.value
  assert.ok(subscription, 'legacy events.listen is preferred over silent degradation')
  assert.equal(source, 'events.listen')
})

test('HOST_signal_subscribe_real_server_url_keeps_global_sse', async () => {
  // Serve mode — a real listener answers the probe on whatever port it picked.
  const result = await withFetch(answers, () =>
    hostSignalSubscribe.trySubscribe({ serverUrl: 'http://127.0.0.1:4096', client: null }, () => {}),
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, false, 'no client.global → hard error, not silent degradation')
  assert.ok(decoded.error.includes('OPENCODE-SIGNAL-SUBSCRIBE'), decoded.error)
})

test('HOST_signal_subscribe_v2_server_url_keeps_global_sse', async () => {
  // OpenCode v2 serve mode — /api/health returns pid + healthy: true.
  const result = await withFetch(answersV2, () =>
    hostSignalSubscribe.trySubscribe({ serverUrl: 'http://127.0.0.1:4096', client: null }, () => {}),
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, false, 'no client.global → hard error, not silent degradation')
  assert.ok(decoded.error.includes('OPENCODE-SIGNAL-SUBSCRIBE'), decoded.error)
})

test('HOST_signal_subscribe_probe_refusal_degrades_not_fails', async () => {
  // A live-but-unanswering endpoint must degrade to the local hook, not crash.
  const result = await withFetch(refused, () =>
    hostSignalSubscribe.trySubscribe({ serverUrl: 'http://10.255.255.1:9', client: null }, () => {}),
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, true)
  assert.equal(decoded.value[1], 'local-event-hook')
})

test('HOST_signal_subscribe_legacy_host_without_server_url_keeps_legacy_verdict', async () => {
  const result = await hostSignalSubscribe.trySubscribe({ client: null }, () => {})
  const decoded = resultOf(result)
  assert.equal(decoded.ok, false, 'legacy host with no transport at all stays fail-fast')
})

// ── 3c. Heartbeat timeout is fatal, never a reconnect-noise loop ─────────────

test('HOST_signal_subscribe_heartbeat_watchdog_markers_present', () => {
  const src = hostSignalSubscribe.source()
  for (const marker of hostSignalSubscribe.heartbeatMarkers) {
    assert.ok(src.includes(marker), `HostSignalSubscribe must contain heartbeat marker: ${marker}`)
  }
})

test('HOST_signal_subscribe_heartbeat_timeout_is_fatal_not_reconnect', () => {
  const src = hostSignalSubscribe.source()
  // One timeout kills the process once via Diagnostic.fatal (SIGKILL) — no
  // abort + exponential-backoff noise loop over a dead link.
  assert.ok(src.includes('onHeartbeatTimeout(silent)'), 'heartbeat timeout must invoke the fatal path')
  assert.ok(src.includes('clearTimeout'), 'one-shot silence deadline cancels via clearTimeout')
  assert.ok(!src.includes('setInterval'), 'period scan removed; one-shot deadline only')
  assert.ok(!src.includes('clearInterval'), 'period scan removed; one-shot deadline only')
  assert.ok(!src.includes('heartbeat timeout recurring'), 'reconnect throttling noise is gone')
  assert.ok(!src.includes('heartbeatTimeouts'), 'consecutive-timeout throttle state is gone')
  assert.ok(!src.includes('state.connAbort.abort'), 'heartbeat no longer forces its own abort')
})

test('HOST_signal_subscribe_heartbeat_fatal_fields_are_whitelisted', () => {
  // onHeartbeatTimeout → Diagnostic.fatal "sse-heartbeat-timeout" ["duration", ms].
  // Must pass the CTX-014 schema gate and print exactly one JSON line (the
  // SIGKILL itself is gated off under node:test).
  const lines = []
  const e = console.error
  console.error = (line) => lines.push(String(line))
  try {
    assert.doesNotThrow(() => diagnostic.fatal('sse-heartbeat-timeout', [['duration', '39766']]))
    assert.equal(lines.length, 1, 'fatal prints exactly one line')
    const payload = JSON.parse(lines[0])
    assert.equal(payload.operation, 'sse-heartbeat-timeout')
    assert.equal(payload.duration, '39766')
  } finally {
    console.error = e
  }
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

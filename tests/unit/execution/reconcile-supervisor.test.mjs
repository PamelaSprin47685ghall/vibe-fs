// P1 five-fix unit surface: ReconcileSupervisor Error budget, sticky cap,
// HostSignalSubscribe reconnect markers, ForkRuntime AwaitAgent deadline,
// ExecutorSummarize cancelOwned on map failure.

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
  const supervisor = reconcileSupervisor.create({ snapshot, binding, onTurn })
  reconcileSupervisor.bindUserMessage(supervisor, sid, physical)
  reconcileSupervisor.kick(supervisor, sid)

  // Three causal yields (setTimeout 0) + snapshot I/O; wait until onTurn fires.
  const deadline = Date.now() + 2000
  while (turns.length === 0 && Date.now() < deadline) {
    await new Promise((r) => setTimeout(r, 20))
  }

  assert.equal(turns.length, 1, 'onTurn must fire after 2 Errors + 1 Ok terminal (Error must not burn attempt budget)')
  assert.equal(caseOf(turns[0].Outcome), 'TurnCompleted')
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

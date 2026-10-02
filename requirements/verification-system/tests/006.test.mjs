import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { createWatchdogHarness } from './support/watchdog-harness.mjs'
import { classifyVerdict } from './support/verdict-feed.mjs'
import { observeCausalProgress } from './e2e/support/causal-observation.js'
import { gatherDiagnostics } from './e2e/support/diagnostics-collect.js'
import { formatDiagnostics } from './e2e/support/diagnostics-format.js'
import { StrictMockProvider } from './e2e/support/strict-mock-provider.js'
import { StrictMockSignals } from './e2e/support/strict-mock-signals.js'
import { createScenarioTurn } from './e2e/support/scenario-turn.js'
import { DIAGNOSTIC_RACE_MS, WATCHDOG_TIMEOUT_MS } from './e2e/support/time-budget.js'
import { attachEventCeilings, eventCeilingSetupProblems, isCountedSseEvent, normalizeEventCeilings } from './e2e/support/event-ceiling.js'

test('WHAT[verification-system-006] event safety ceilings validate input and count actual non-heartbeat events', () => {
  assert.deepEqual(normalizeEventCeilings({}), {})
  assert.deepEqual(normalizeEventCeilings({ maxJournalEvents: 12, maxSseEvents: 34 }),
    { maxJournalEvents: 12, maxSseEvents: 34 })
  assert.throws(() => normalizeEventCeilings({ maxJournalEvents: 0 }), /positive integer/)
  assert.throws(() => normalizeEventCeilings({ maxSseEvents: 1.2 }), /positive integer/)
  assert.deepEqual(eventCeilingSetupProblems(undefined), [])
  assert.ok(eventCeilingSetupProblems({ maxJournalEvents: 0 })[0].includes('maxJournalEvents'))
  assert.ok(eventCeilingSetupProblems({ maxSseEvents: -3 })[0].includes('maxSseEvents'))
  assert.equal(isCountedSseEvent({ type: 'server.heartbeat' }), false)
  assert.equal(isCountedSseEvent({ type: 'message.updated' }), true)
  assert.equal(isCountedSseEvent({ type: '' }), false)
  assert.equal(isCountedSseEvent({}), false)
  const listeners = []
  const scenario = {
    host: { workDir: '/tmp/unused-for-sse-only' },
    events: {
      allEvents: [{ type: 'message.updated' }, { type: 'server.heartbeat' }, { type: 'sync' }],
      onEvent(callback) {
        listeners.push(callback)
        return () => listeners.splice(listeners.indexOf(callback), 1)
      },
      dump: () => '',
    },
    watchdog: { stop() {} },
  }
  let breached = null
  attachEventCeilings(scenario, { maxSseEvents: 2 }, {
    onBreach(detail) {
      breached = detail
      throw new Error('ceiling')
    },
  })
  assert.equal(breached, null)
  listeners[0]({ type: 'server.heartbeat' })
  assert.equal(breached, null)
  assert.throws(() => listeners[0]({ type: 'session.idle' }), /ceiling/)
  assert.deepEqual({ kind: breached.kind, observed: breached.observed, limit: breached.limit, sse: breached.sseEvents },
    { kind: 'maxSseEvents', observed: 3, limit: 2, sse: 3 })
})

const causalSnapshot = {
  pid: 1, sequence: '3',
  active: [{
    waitKind: 'provider-assessment',
    owner: { kind: 'RelayWorkflow', identity: [{ k: 'incumbency', v: 'I2' }] },
    subject: [{ k: 'road', v: 'road-manager.0' }],
    producer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
    escapes: [{ tag: 'processLifetime' }], source: 'test',
  }],
  history: [],
  frontiers: [{
    kind: 'ExternalProducerFrontier', detail: 'waiting for provider P81',
    chain: [], frontierProducer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
    cycle: [],
  }],
}

test('WHAT[verification-system-006] diagnostics preserve the actual wait and monitored subject', async () => {
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-gather-'))
  try {
    const directory = path.join(workDir, '.wanxiangshu', 'diagnostics')
    fs.mkdirSync(directory, { recursive: true })
    fs.writeFileSync(path.join(directory, 'causal-waits.json'), JSON.stringify(causalSnapshot))
    const diag = await gatherDiagnostics({
      host: { workDir },
      provider: { requests: [], unexpectedRequests: [], remainingExpectations: 1,
        blockedExpectations: [{ id: 'road-manager.0', lane: 'x', blocking: true }] },
    })
    assert.deepEqual(diag.causalWaitSnapshot, causalSnapshot)
    assert.deepEqual(diag.causalExpectationCorrelation, {
      matched: ['road-manager.0'], unmatched: [], divergence: false,
    })
    const output = formatDiagnostics(diag)
    assert.match(output, /provider-assessment/)
    assert.match(output, /I2/)
    assert.match(output, /P81/)
  } finally {
    fs.rmSync(workDir, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-006] missing and corrupt wait snapshots disclose the diagnostic gap', async () => {
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-missing-'))
  try {
    let diag = await gatherDiagnostics({ host: { workDir } })
    assert.match(formatDiagnostics(diag), /current waits unavailable.*snapshot.*missing/i)
    const directory = path.join(workDir, '.wanxiangshu', 'diagnostics')
    fs.mkdirSync(directory, { recursive: true })
    fs.writeFileSync(path.join(directory, 'causal-waits.json'), '{invalid')
    diag = await gatherDiagnostics({ host: { workDir } })
    assert.match(formatDiagnostics(diag), /current waits unavailable.*invalid/i)
  } finally {
    fs.rmSync(workDir, { recursive: true, force: true })
  }
})

async function assertSingleTermination(h, timeoutMs) {
  assert.equal(h.terminateCount, 1)
  h.watchdog.advance({ reason: 'late verdict', lane: 'worker', blocking: true })
  await h.advance(Math.max(DIAGNOSTIC_RACE_MS, timeoutMs))
  assert.equal(h.terminateCount, 1, 'late progress must not restart a terminated watchdog')
  h.watchdog.setWindow(timeoutMs)
  await h.advance(timeoutMs)
  assert.equal(h.terminateCount, 1, 'window changes must not restart a terminated watchdog')
}

test('WHAT[verification-system-006] the initial silence window covers execution before its first progress', async () => {
  const h = createWatchdogHarness({ timeoutMs: WATCHDOG_TIMEOUT_MS })
  await h.advance(WATCHDOG_TIMEOUT_MS - 1)
  assert.equal(h.terminated, false)
  assert.deepEqual(h.diagnostics, [])
  await h.advance(1)
  assert.equal(h.terminateCount, 1)
  assert.match(h.diagnostics.join('\n'), /test-target/)
  assert.match(h.diagnostics.join('\n'), /last progress/)
  assert.match(h.diagnostics.join('\n'), /current waits unavailable/i)
  assert.equal(h.trace.at(-1), 'terminate')
  await assertSingleTermination(h, WATCHDOG_TIMEOUT_MS)
})

test('WHAT[verification-system-006] continuing progress outlives a window and silence is measured from its last advance', async () => {
  const h = createWatchdogHarness({ timeoutMs: 300 })
  for (const reason of ['first verdict', 'second verdict', 'third verdict']) {
    await h.advance(200)
    assert.equal(h.terminated, false)
    h.watchdog.advance({ reason, lane: 'worker', blocking: true })
  }
  await h.advance(299)
  assert.equal(h.terminated, false)
  await h.advance(1)
  assert.equal(h.terminateCount, 1)
  assert.match(h.diagnostics.join('\n'), /third verdict.*worker/)
})

test('WHAT[verification-system-006] completed verdicts renew the window while repeated log traffic cannot', async () => {
  for (const type of ['test:pass', 'test:fail', 'test:complete']) {
    const h = createWatchdogHarness()
    await h.advance(200)
    h.watchdog.advance(classifyVerdict({ type, data: { name: 'completed case', file: 'example.mjs' } }))
    await h.advance(499)
    assert.equal(h.terminated, false, type)
    await h.advance(1)
    assert.equal(h.terminated, true, type)
  }
  for (const type of ['test:stdout', 'test:stderr', 'test:diagnostic']) {
    const h = createWatchdogHarness()
    for (let index = 0; index < 4; index++) {
      await h.advance(100)
      h.watchdog.advance(classifyVerdict({ type, data: { file: 'stuck.mjs' } }))
    }
    await h.advance(100)
    assert.equal(h.terminated, true, `${type} must not keep a stuck test alive`)
    assert.match(h.diagnostics.join('\n'), /0 blocking progress/)
  }
})

test('WHAT[verification-system-006] scheduling events do not renew the silence window', async () => {
  const h = createWatchdogHarness()
  await h.advance(400)
  for (const type of ['test:enqueue', 'test:dequeue', 'test:start', 'test:plan', 'inner:drained', 'runner:error']) {
    const progress = classifyVerdict({ type, data: { file: 'x.mjs' } })
    if (progress) h.watchdog.advance(progress)
  }
  await h.advance(100)
  assert.equal(h.terminated, true)
})

test('WHAT[verification-system-006] repeat observations of one causal state do not renew twice', async () => {
  const h = createWatchdogHarness()
  const scenario = { watchdog: h.watchdog }
  const observe = (token) => observeCausalProgress(scenario, {
    id: 'accepted-work', token, reason: 'work accepted', lane: 'worker',
  })
  assert.equal(observe('pending'), false)
  await h.advance(200)
  assert.equal(observe('accepted'), true)
  await h.advance(300)
  assert.equal(observe('accepted'), false)
  await h.advance(199)
  assert.equal(h.terminated, false)
  await h.advance(1)
  assert.equal(h.terminated, true)
})

test('WHAT[verification-system-006] stop cancels monitoring and late signals cannot resurrect it', async () => {
  const h = createWatchdogHarness()
  await h.advance(100)
  h.watchdog.stop()
  h.watchdog.advance({ reason: 'late verdict', lane: 'worker', blocking: true })
  h.watchdog.setWindow(50)
  await h.advance(1000)
  assert.equal(h.terminateCount, 0)
  assert.deepEqual(h.diagnostics, [])
})

test('WHAT[verification-system-006] changing the window does not invent new causal progress', async () => {
  const h = createWatchdogHarness()
  await h.advance(300)
  h.watchdog.setWindow(600)
  await h.advance(299)
  assert.equal(h.terminated, false)
  await h.advance(1)
  assert.equal(h.terminated, true)
})

test('WHAT[verification-system-006] restoring the default window retains the original last-progress time', async () => {
  const h = createWatchdogHarness()
  h.watchdog.setWindow(WATCHDOG_TIMEOUT_MS * 2)
  await h.advance(100)
  h.watchdog.setWindow(null)
  await h.advance(WATCHDOG_TIMEOUT_MS - 101)
  assert.equal(h.terminated, false)
  await h.advance(1)
  assert.equal(h.terminateCount, 1)
})

test('WHAT[verification-system-006] failed diagnostic collection discloses its cause before exit', async () => {
  const h = createWatchdogHarness({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    onTimeout: async () => { throw new Error('host unavailable') },
  })
  await h.advance(WATCHDOG_TIMEOUT_MS)
  assert.equal(h.terminateCount, 1)
  assert.match(h.diagnostics.join('\n'), /current waits unavailable.*host unavailable/i)
  assert.equal(h.trace.at(-1), 'terminate')
  await assertSingleTermination(h, WATCHDOG_TIMEOUT_MS)
})

test('WHAT[verification-system-006] hung diagnostic collection discloses missing information and cannot prevent exit', async () => {
  const h = createWatchdogHarness({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    onTimeout: () => new Promise(() => {}),
  })
  await h.advance(WATCHDOG_TIMEOUT_MS)
  assert.equal(h.terminated, false)
  await h.advance(DIAGNOSTIC_RACE_MS - 1)
  assert.equal(h.terminated, false)
  await h.advance(1)
  assert.equal(h.terminateCount, 1)
  assert.match(h.diagnostics.join('\n'), /current waits unavailable.*deadline/i)
  await assertSingleTermination(h, WATCHDOG_TIMEOUT_MS)
})

test('WHAT[verification-system-006] successful diagnostic collection completes before a single exit', async () => {
  const calls = []
  const h = createWatchdogHarness({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    onTimeout: async () => { calls.push('waits collected') },
  })
  await h.advance(WATCHDOG_TIMEOUT_MS)
  assert.deepEqual(calls, ['waits collected'])
  assert.equal(h.terminateCount, 1)
  assert.equal(h.trace.at(-1), 'terminate')
  assert.doesNotMatch(h.diagnostics.join('\n'), /unavailable/)
  await assertSingleTermination(h, WATCHDOG_TIMEOUT_MS)
})

test('WHAT[verification-system-006] a diagnostic output failure still terminates once and cannot restart monitoring', async () => {
  let collectionCount = 0
  const h = createWatchdogHarness({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    onTimeout: async () => { collectionCount++ },
    deps: { diagnostic: { write() { throw new Error('diagnostic sink unavailable') } } },
  })
  await h.advance(WATCHDOG_TIMEOUT_MS - 1)
  assert.equal(h.terminateCount, 0)
  await h.advance(1)
  assert.equal(collectionCount, 0, 'diagnostic output failed before collection could start')
  assert.equal(h.diagnostics.length, 1)
  assert.match(h.diagnostics[0], /WATCHDOG: 'test-target'/)
  assert.deepEqual(h.trace, ['diagnostic', 'terminate'])
  await assertSingleTermination(h, WATCHDOG_TIMEOUT_MS)
})

test('WHAT[verification-system-006] consumed expectation observations preserve the matched physical attempt', () => {
  const provider = new StrictMockProvider()
  let early
  provider.afterExpectation('orch.2', (observation) => { early = observation })
  provider._signals.consume({ id: 'orch.2', permanent: true })
  provider._runAfterExpectation('orch.2', { sessionId: 'ses-orch', parentSessionId: 'ses-parent' })
  assert.deepEqual(early, { id: 'orch.2', attempt: 1, sessionId: 'ses-orch', parentSessionId: 'ses-parent' })
  let late
  provider.afterExpectation('orch.2', (observation) => { late = observation })
  assert.deepEqual(late, early)
})

test('WHAT[verification-system-006] turn observations keep session identity and forget pre-restart cursors', async () => {
  const events = {
    lastSeq: 0, all: [],
    async awaitEvent(predicate) {
      const found = this.all.find(predicate)
      if (!found) throw new Error('no matching event')
      return found
    },
  }
  const turns = createScenarioTurn({ events })
  const root = turns.start('root')
  const child = turns.start('child')
  assert.equal(turns.current('root'), root)
  assert.equal(turns.current('child'), child)
  events.all.push(
    { seq: 1, type: 'message.updated', sessionID: 'root', finishReason: 'stop' },
    { seq: 2, type: 'session.idle', sessionID: 'root' },
  )
  events.lastSeq = 2
  await root.awaitTerminal({ requireAssistantTerminal: false })
  assert.equal(root.activitySeq, 1)
  turns.clear()
  assert.equal(turns.current('root'), null)
  assert.equal(turns.current('child'), null)
})

test('WHAT[verification-system-006] alternative progress waits resolve the observed branch and release siblings', async () => {
  for (const winner of ['original.1', 'guarded.0']) {
    const signals = new StrictMockSignals()
    const waiting = signals.waitForAnyExpectation(['original.1', 'guarded.0'])
    signals.consume({ id: winner, permanent: true })
    assert.equal(await waiting, winner)
    assert.equal(signals._expectationWaiters.size, 0)
  }
})

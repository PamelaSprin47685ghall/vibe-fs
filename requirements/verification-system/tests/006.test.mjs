import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { gatherDiagnostics } from './e2e/support/diagnostics-collect.js'
import { formatDiagnostics } from './e2e/support/diagnostics-format.js'
import { formatCausalSection } from './e2e/support/diagnostics-causal.js'
import { StrictMockProvider } from './e2e/support/strict-mock-provider.js'
import { createScenarioTurn } from './e2e/support/scenario-turn.js'
import { classifyVerdict } from './support/verdict-feed.mjs'
import { e2eTestCaseFiles, scanE2EWatchdogFeed } from './e2e/support/watchdog-feed-scan.mjs'
import { StrictMockSignals } from './e2e/support/strict-mock-signals.js'

// ── Causal diagnostics formatting ───────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-006] gather reads causal waits file', async () => {
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-gather-'))
  const dir = path.join(workDir, '.wanxiangshu', 'diagnostics')
  fs.mkdirSync(dir, { recursive: true })
  const payload = {
    pid: 1,
    sequence: '3',
    active: [{
      waitKind: 'provider-assessment',
      owner: { kind: 'RelayWorkflow', identity: [{ k: 'incumbency', v: 'I2' }] },
      subject: [{ k: 'road', v: 'road-manager.0' }],
      producer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
      escapes: [{ tag: 'processLifetime' }],
      source: 'test',
    }],
    history: [],
    frontiers: [{
      kind: 'ExternalProducerFrontier',
      detail: 'FRONTIER: waiting for external producer external:provider',
      chain: [{ owner: { kind: 'RelayWorkflow', identity: [{ k: 'incumbency', v: 'I2' }] }, waitKind: 'provider-assessment' }],
      frontierProducer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
      cycle: [],
    }],
  }
  fs.writeFileSync(path.join(dir, 'causal-waits.json'), JSON.stringify(payload), 'utf8')
  try {
    const diag = await gatherDiagnostics({
      host: { workDir },
      provider: {
        requests: [],
        unexpectedRequests: [],
        remainingExpectations: 1,
        blockedExpectations: [{ id: 'road-manager.0', lane: 'x', blocking: true }],
      },
    })
    assert.ok(diag.causalWaitSnapshot)
    assert.equal(diag.causalWaitSnapshot.active[0].waitKind, 'provider-assessment')
    assert.ok(Array.isArray(diag.causalFrontier))
    assert.equal(diag.causalExpectationCorrelation.matched.includes('road-manager.0'), true)
  } finally {
    fs.rmSync(workDir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-006] format puts frontier before e2e events', () => {
  const text = formatDiagnostics({
    events: [{ seq: 1, time: '00:00:00.000', type: 'session.idle' }],
    causalFrontier: [{
      kind: 'ExternalProducerFrontier',
      detail: 'FRONTIER: waiting for external producer external:provider',
      chain: [{ owner: { kind: 'RelayWorkflow', identity: [{ k: 'incumbency', v: 'I2' }] }, waitKind: 'provider-assessment' }],
      frontierProducer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
      cycle: [],
    }],
    causalWaitSnapshot: {
      active: [],
      history: [{ sequence: '1', kind: 'entered', wait: { waitKind: 'provider-assessment' }, exit: null }],
    },
  })
  const frontierAt = text.indexOf('════════════ CAUSAL FRONTIER ════════════')
  const e2eAt = text.indexOf('══════════════════════ E2E DIAGNOSTICS ══════════════════════')
  assert.ok(frontierAt >= 0, 'missing CAUSAL FRONTIER banner')
  assert.ok(e2eAt > frontierAt, 'CAUSAL FRONTIER must precede E2E DIAGNOSTICS')
})

test('WHAT[VERIFICATION-SYSTEM-006] formatCausalSection banner is first line', () => {
  const lines = formatCausalSection({
    causalFrontier: [{
      kind: 'ExternalProducerFrontier',
      detail: 'FRONTIER: waiting for external producer external:provider',
      chain: [],
      frontierProducer: { tag: 'external', kind: 'provider', identity: [] },
      cycle: [],
    }],
    causalWaitSnapshot: { active: [], history: [] },
  })
  assert.ok(lines.length > 0, 'formatCausalSection must emit lines')
  assert.equal(lines[0], '════════════ CAUSAL FRONTIER ════════════')
})

test('WHAT[VERIFICATION-SYSTEM-006] watchdog onTimeout prints frontier before event tail', () => {
  const source = fs.readFileSync(
    fileURLToPath(new URL('./e2e/support/scenario-parallel.js', import.meta.url)),
    'utf8',
  )
  const onTimeoutAt = source.indexOf('onTimeout: async () => {')
  assert.ok(onTimeoutAt >= 0, 'missing watchdog onTimeout')
  const body = source.slice(onTimeoutAt)
  const frontierAt = body.indexOf('CAUSAL FRONTIER')
  const eventTailAt = body.indexOf('watchdog event tail')
  assert.ok(frontierAt >= 0, 'onTimeout must print CAUSAL FRONTIER')
  assert.ok(eventTailAt >= 0, 'onTimeout must still print event tail')
  assert.ok(frontierAt < eventTailAt, 'CAUSAL FRONTIER must precede watchdog event tail')
  assert.ok(body.includes('collectCausalWaits(diag, scenario)'), 'onTimeout must collect via scenario')
})

// ── Expectation observation ─────────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-006] afterExpectation observation preserves physical session for early and late barriers', () => {
  const provider = new StrictMockProvider()
  let early = null
  provider.afterExpectation('orch.2', (observation) => { early = observation })

  provider._signals.consume({ id: 'orch.2', permanent: true })
  provider._runAfterExpectation('orch.2', { sessionId: 'ses-orch', parentSessionId: 'ses-parent' })

  assert.deepEqual(early, {
    id: 'orch.2',
    attempt: 1,
    sessionId: 'ses-orch',
    parentSessionId: 'ses-parent',
  })

  let late = null
  provider.afterExpectation('orch.2', (observation) => { late = observation })
  assert.deepEqual(late, early, 'late barrier must recover the observation from the matched physical attempt')
})

// ── Incident runbook fixture ────────────────────────────────────────────────

const incidentUrl = new URL('../../managed-chat-execution/fixtures/incidents/agent-028.json', import.meta.url)
const schemaUrl = new URL('../../managed-chat-execution/tests/fixtures/incident-evidence-v1.schema.json', import.meta.url)
const replayToolUrl = new URL('../../managed-chat-execution/tests/support/incident-evidence.mjs', import.meta.url)

test('WHAT[VERIFICATION-SYSTEM-006] incident fixture carries declared redaction and no secrets', () => {
  assert.equal(fs.existsSync(schemaUrl), true)
  assert.equal(fs.existsSync(replayToolUrl), true)
  assert.equal(fs.existsSync(incidentUrl), true)

  const incident = fs.readFileSync(incidentUrl, 'utf8')
  assert.doesNotMatch(incident, /Bearer\s+|api[_-]?key|password|stack trace|\/(?:home|Users)\//i)
  assert.deepEqual(JSON.parse(incident).redaction, {
    payloads: 'removed', credentials: 'removed', stacks: 'removed', paths: 'removed',
  })
})

// ── Scenario turn registry ──────────────────────────────────────────────────

const fakeEvents = () => ({
  lastSeq: 0,
  all: [],
  async awaitEvent(predicate) {
    const found = this.all.find(predicate)
    if (!found) throw new Error('no matching event')
    return found
  },
})

test('WHAT[VERIFICATION-SYSTEM-006] turn registry keeps physical cursor identity per session', async () => {
  const events = fakeEvents()
  const turns = createScenarioTurn({ events })

  const root = turns.start('root')
  const child = turns.start('child')

  assert.equal(turns.current('root'), root, 'starting child work must not overwrite root turn cursor')
  assert.equal(turns.current('child'), child)

  events.all.push(
    { seq: 1, type: 'message.updated', sessionID: 'root', finishReason: 'stop' },
    { seq: 2, type: 'session.idle', sessionID: 'root' },
  )
  events.lastSeq = 2

  await root.awaitTerminal({ requireAssistantTerminal: false })
  assert.equal(root.activitySeq, 1)
})

test('WHAT[VERIFICATION-SYSTEM-006] turn registry restart clear forgets all pre-restart cursors', () => {
  const turns = createScenarioTurn({ events: fakeEvents() })
  turns.start('root')
  turns.start('child')
  turns.clear()

  assert.equal(turns.current('root'), null)
  assert.equal(turns.current('child'), null)
})

// ── Verdict feed ────────────────────────────────────────────────────────────

const event = (type, data = {}) => ({ type, data })

test('WHAT[VERIFICATION-SYSTEM-006] a verdict renews the silence window', () => {
  assert.deepEqual(classifyVerdict(event('test:pass', { name: 'a', file: 'x.mjs' })), {
    blocking: true,
    reason: 'test:pass:a',
    lane: 'x.mjs',
  })

  assert.deepEqual(classifyVerdict(event('test:fail', { name: 'b', file: 'y.mjs' })), {
    blocking: true,
    reason: 'test:fail:b',
    lane: 'y.mjs',
  })

  assert.deepEqual(classifyVerdict(event('test:complete', { name: 'c', file: 'z.mjs' })), {
    blocking: true,
    reason: 'test:complete:c',
    lane: 'z.mjs',
  })
})

test('WHAT[VERIFICATION-SYSTEM-006] bytes moving is recorded and does not renew', () => {
  for (const type of ['test:stdout', 'test:stderr', 'test:diagnostic']) {
    assert.deepEqual(
      classifyVerdict(event(type, { file: 'x.mjs' })),
      { blocking: false, reason: type, lane: 'x.mjs' },
      `${type} must be recorded as background, never as progress`,
    )
  }
})

test('WHAT[VERIFICATION-SYSTEM-006] scheduling noise is not fed at all', () => {
  for (const type of ['test:enqueue', 'test:dequeue', 'test:start', 'test:plan', 'inner:drained', 'runner:error']) {
    assert.equal(classifyVerdict(event(type, { file: 'x.mjs' })), null, `${type} must not be fed`)
  }

  assert.equal(classifyVerdict(undefined), null)
  assert.equal(classifyVerdict({}), null)
  assert.equal(classifyVerdict({ type: 42 }), null)
})

test('WHAT[VERIFICATION-SYSTEM-006] a verdict without a name or file still carries attribution', () => {
  const classified = classifyVerdict(event('test:pass'))

  assert.deepEqual(classified, { blocking: true, reason: 'test:pass:(unnamed)', lane: '(no file)' })
  assert.ok(classified.reason.length > 0 && classified.lane.length > 0)
})

// ── Top-level e2e watchdog feeding restriction ──────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-006] top-level e2e tests never feed watchdog directly', () => {
  const files = e2eTestCaseFiles()

  const violations = []
  for (const file of files) {
    violations.push(...scanE2EWatchdogFeed([file]))
  }

  assert.equal(
    violations.length,
    0,
    'e2e top-level tests must not call watchdog.advance directly (VERIFY-004); they must use support causal primitives only. Violations: ' +
      JSON.stringify(violations),
  )
})

// ── StrictMockSignals waitAny ───────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-006] waitAny selects either exact branch and removes every sibling waiter', async () => {
  for (const winner of ['original.1', 'guarded.0']) {
    const signals = new StrictMockSignals()
    const waiting = signals.waitForAnyExpectation(['original.1', 'guarded.0'])

    signals.consume({ id: winner, permanent: true })

    assert.equal(await waiting, winner)
    assert.equal(signals._expectationWaiters.size, 0)
  }
})

import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fs } = await import("node:fs");
const { default: os } = await import("node:os");
const { default: path } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { gatherDiagnostics } = await import("./e2e/support/diagnostics-collect.js");
const { formatDiagnostics } = await import("./e2e/support/diagnostics-format.js");
const { formatCausalSection } = await import("./e2e/support/diagnostics-causal.js");


test('WHAT[verification-system-006] gather reads causal waits file', async () => {
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
test('WHAT[verification-system-006] format puts frontier before e2e events', () => {
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
test('WHAT[verification-system-006] formatCausalSection banner is first line', () => {
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
test('WHAT[verification-system-006] watchdog onTimeout prints frontier before event tail', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { existsSync, mkdtempSync, mkdirSync, writeFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const { E2E_ROOT_REL, SOLE_ENTRY, e2eTestCaseFiles, scanE2EWatchdogFeed } = await import("./e2e/support/watchdog-feed-scan.mjs");

const makeTempRoot = (layout) => {
  const root = mkdtempSync(join(tmpdir(), 'e2e-wdf-fc-'))
  const e2e = join(root, E2E_ROOT_REL)
  if (layout.e2eDir !== false) mkdirSync(e2e, { recursive: true })
  for (const name of layout.files ?? []) {
    writeFileSync(join(e2e, name), '// throwaway\n')
  }
  if (layout.e2eIsFile) {
    rmSync(e2e, { recursive: true, force: true })
    writeFileSync(e2e, 'not a directory\n')
  }
  return root
}
const cleanup = (root) => rmSync(root, { recursive: true, force: true })

test('WHAT[verification-system-006] top-level e2e tests never feed watchdog directly', () => {
  // watchdog 只由 support/ 因果原语投喂；顶层测试直接调用 watchdog.advance( 即违规。
  const files = e2eTestCaseFiles()

  const violations = []
  for (const file of files) {
    violations.push(...scanE2EWatchdogFeed([file]))
  }

  assert.equal(
    violations.length,
    0,
    'e2e top-level tests must not call watchdog.advance directly (verification-system-006); they must use support causal primitives only. Violations: ' +
      JSON.stringify(violations),
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { StrictMockProvider } = await import("./e2e/support/strict-mock-provider.js");


test('WHAT[verification-system-006] afterExpectation observation preserves physical session for early and late barriers', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: fs } = await import("node:fs");
const { default: test } = await import("node:test");

const runbookUrl = new URL('../../managed-chat-execution/OPERATOR-RUNBOOK.md', import.meta.url)
const incidentUrl = new URL('../../managed-chat-execution/fixtures/incidents/agent-028.json', import.meta.url)
const schemaUrl = new URL('../../managed-chat-execution/tests/fixtures/incident-evidence-v1.schema.json', import.meta.url)
const replayToolUrl = new URL('../../managed-chat-execution/tests/support/incident-evidence.mjs', import.meta.url)
const runbook = fs.readFileSync(runbookUrl, 'utf8')
const incident = fs.readFileSync(incidentUrl, 'utf8')

test('WHAT[verification-system-006] incident fixture carries declared redaction and no secrets', () => {
  assert.equal(fs.existsSync(schemaUrl), true)
  assert.equal(fs.existsSync(replayToolUrl), true)
  assert.equal(fs.existsSync(incidentUrl), true)

  assert.doesNotMatch(incident, /Bearer\s+|api[_-]?key|password|stack trace|\/(?:home|Users)\//i)
  assert.deepEqual(JSON.parse(incident).redaction, {
    payloads: 'removed', credentials: 'removed', stacks: 'removed', paths: 'removed',
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createScenarioTurn } = await import("./e2e/support/scenario-turn.js");

const fakeEvents = () => ({
  lastSeq: 0,
  all: [],
  async awaitEvent(predicate) {
    const found = this.all.find(predicate)
    if (!found) throw new Error('no matching event')
    return found
  },
})

test('WHAT[verification-system-006] turn registry keeps physical cursor identity per session', async () => {
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
test('WHAT[verification-system-006] turn registry restart clear forgets all pre-restart cursors', () => {
  const turns = createScenarioTurn({ events: fakeEvents() })
  turns.start('root')
  turns.start('child')
  turns.clear()

  assert.equal(turns.current('root'), null)
  assert.equal(turns.current('child'), null)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { StrictMockSignals } = await import("./e2e/support/strict-mock-signals.js");


test('WHAT[verification-system-006] waitAny selects either exact branch and removes every sibling waiter', async () => {
  for (const winner of ['original.1', 'guarded.0']) {
    const signals = new StrictMockSignals();
    const waiting = signals.waitForAnyExpectation(['original.1', 'guarded.0']);

    signals.consume({ id: winner, permanent: true });

    assert.equal(await waiting, winner);
    assert.equal(signals._expectationWaiters.size, 0);
  }
});
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { classifyVerdict } = await import("./support/verdict-feed.mjs");

const event = (type, data = {}) => ({ type, data })

test('WHAT[verification-system-006] a verdict renews the silence window', () => {
  // Whole objects, not truthiness. mjs has no compile-time rename protection, so `blocking` read as
  // `undefined` would be falsy and a truthiness assertion would report the opposite of the truth
  // while passing — the exact failure this repository measured four times in package K9.
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
test('WHAT[verification-system-006] bytes moving is recorded and does not renew', () => {
  // 「不算进展：…任何『有字节在动』的证据」. `test:stdout` is the load-bearing member: a test that
  // hangs while printing is what turns a verdict feed back into a wall-clock timer, and
  // `hangs-with-handle-and-chatter.fixture.mjs` is built from exactly that shape.
  for (const type of ['test:stdout', 'test:stderr', 'test:diagnostic']) {
    assert.deepEqual(
      classifyVerdict(event(type, { file: 'x.mjs' })),
      { blocking: false, reason: type, lane: 'x.mjs' },
      `${type} must be recorded as background, never as progress`,
    )
  }
})
test('WHAT[verification-system-006] scheduling noise is not fed at all', () => {
  // `null` rather than a background default. `test:enqueue` and `test:dequeue` fire per test before
  // anything has happened, so defaulting unknown events to background would fill the watchdog dump's
  // "last background progress" line with scheduling noise and point the reader at the wrong lane.
  for (const type of ['test:enqueue', 'test:dequeue', 'test:start', 'test:plan', 'inner:drained', 'runner:error']) {
    assert.equal(classifyVerdict(event(type, { file: 'x.mjs' })), null, `${type} must not be fed`)
  }

  assert.equal(classifyVerdict(undefined), null)
  assert.equal(classifyVerdict({}), null)
  assert.equal(classifyVerdict({ type: 42 }), null)
})
test('WHAT[verification-system-006] a verdict without a name or file still carries attribution', () => {
  // `Watchdog.advance` rejects an empty reason or lane by design — verification-system-006 makes both part of the
  // timeout dump, and W6 records that a default of 'unattributed' would keep every canary green
  // while the dump lost the one thing the clause requires it to carry. So the classifier must never
  // produce an empty field, even from an event missing both.
  const classified = classifyVerdict(event('test:pass'))

  assert.deepEqual(classified, { blocking: true, reason: 'test:pass:(unnamed)', lane: '(no file)' })
  assert.ok(classified.reason.length > 0 && classified.lane.length > 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { spawn } = await import("node:child_process");
const { default: path } = await import("node:path");
const { PassThrough } = await import("node:stream");
const { fileURLToPath } = await import("node:url");

const { createCompactReporter } = await import("./support/compact-reporter.mjs");
const {
  createRunState,
  applyEvent,
  summarize,
  isFileCompletionEvent,
  TestRunState,
} = await import("./support/test-run-state.mjs");
const { drainTestStream } = await import("./support/run-inner.mjs");
const { superviseNodeTest } = await import("./e2e/support/supervise-node-test.mjs");

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../..')
const innerRunner = path.join(here, 'support/run-inner.mjs')

function cleanEnv() {
  const env = { ...process.env }
  delete env.NODE_TEST_CONTEXT
  return env
}

test('WHAT[verification-system-006] P6-REPORTER-001: compact and verbose modes produce identical verdict counts matching exact expectation', async () => {
  const targetFile = path.join(
    root,
    'requirements/distribution/tests/integration/package/layout.test.mjs',
  )

  const runWithMode = async (verbose) => {
    const events = [
      { type: 'test:start', data: { name: 'test A', file: targetFile, nesting: 0 } },
      { type: 'test:pass', data: { name: 'test A', file: targetFile, nesting: 0, details: { duration_ms: 10 } } },
      { type: 'test:start', data: { name: 'test B', file: targetFile, nesting: 0 } },
      { type: 'test:fail', data: { name: 'test B', file: targetFile, nesting: 0, details: { duration_ms: 20, error: new Error('fail B') } } },
      { type: 'test:start', data: { name: 'test C', file: targetFile, nesting: 0 } },
      { type: 'test:pass', data: { name: 'test C', file: targetFile, nesting: 0, skip: true, details: { duration_ms: 5 } } },
      { type: 'test:start', data: { name: 'test D', file: targetFile, nesting: 0 } },
      { type: 'test:pass', data: { name: 'test D', file: targetFile, nesting: 0, todo: true, details: { duration_ms: 5 } } },
      { type: 'test:summary', data: { duration_ms: 50 } },
    ]

    async function* sourceGen() {
      for (const ev of events) yield ev
    }

    let summaryResult = null
    let stderrText = ''
    const noopStream = { write() {} }
    const errStream = {
      write(chunk) {
        stderrText += chunk
      },
    }
    const reporter = createCompactReporter({
      verbose,
      stdout: noopStream,
      stderr: errStream,
      onSummary: (s) => {
        summaryResult = s
      },
    })
    for await (const _ of reporter(sourceGen())) {}
    return { summaryResult, stderrText }
  }

  const compact = await runWithMode(false)
  const verbose = await runWithMode(true)

  const expected = {
    files: 1,
    passed: 1,
    failed: 1,
    skipped: 1,
    todo: 1,
    cancelled: 0,
  }

  for (const [key, val] of Object.entries(expected)) {
    assert.equal(compact.summaryResult[key], val, `compact summary.${key} must match expected`)
    assert.equal(verbose.summaryResult[key], val, `verbose summary.${key} must match expected`)
  }

  assert.deepEqual(
    compact.summaryResult.byFile.map((f) => ({ ...f, durationMs: 0 })),
    verbose.summaryResult.byFile.map((f) => ({ ...f, durationMs: 0 })),
  )

  // Failures detail in stderr contains test B name
  assert.match(compact.stderrText, /test B/)
  assert.match(verbose.stderrText, /test B/)
})

test('WHAT[verification-system-006] P6-REPORTER-002: single leaf completion does NOT remove file from outstanding set', async () => {
  // (a) Truth table against isFileCompletionEvent
  const fileA = path.resolve('/tmp/a.test.mjs')
  const fileB = path.resolve('/tmp/nested/b.test.mjs')

  // leaf complete -> false
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: 'leaf 1', file: fileA, nesting: 0 },
    }),
    false,
  )
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: 'leaf 2', file: fileA, nesting: 1 },
    }),
    false,
  )

  // wrapper (name is absolute path equal to file) -> true
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: fileA, file: fileA, nesting: 0 },
    }),
    true,
  )

  // style variant: relative path whose resolve() matches file -> true
  const relFileA = path.relative(process.cwd(), fileA)
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: relFileA, file: fileA, nesting: 0 },
    }),
    true,
  )

  // Leaf name happens to equal file basename/suffix (e.g. name='b.test.mjs', file='/tmp/nested/b.test.mjs')
  // resolve('b.test.mjs') resolves to <cwd>/b.test.mjs which is not /tmp/nested/b.test.mjs -> false.
  // This guards against suffix-ambiguity regression where endsWith or basename is misused.
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: 'b.test.mjs', file: fileB, nesting: 0 },
    }),
    false,
  )

  // (b) Real child running two-leaf.fixture.mjs via IPC
  const fixturePath = path.join(here, 'support/fixtures/two-leaf.fixture.mjs')
  const child = spawn(process.execPath, [innerRunner, fixturePath], {
    stdio: ['ignore', 'ignore', 'ignore', 'ipc'],
    env: cleanEnv(),
  })

  const completes = []
  child.on('message', (msg) => {
    if (msg?.type === 'test:complete') {
      completes.push(msg)
    }
  })

  const exitCode = await new Promise((res) => child.on('exit', res))
  assert.equal(exitCode, 0, 'child must exit 0')

  assert.ok(completes.length >= 3, `must receive at least 3 completes (2 leaves + 1 wrapper), got ${completes.length}`)

  const leafCompletes = completes.slice(0, -1)
  const lastComplete = completes[completes.length - 1]

  for (const leaf of leafCompletes) {
    assert.equal(
      isFileCompletionEvent(leaf),
      false,
      `leaf complete for '${leaf.data?.name}' must not be a file completion event`,
    )
  }

  assert.equal(
    isFileCompletionEvent(lastComplete),
    true,
    'last complete event must be a file completion event',
  )

  // Replay against outstanding set in order of arrival
  const resolvedFixture = path.resolve(fixturePath)
  const outstanding = new Set([resolvedFixture])

  for (let i = 0; i < completes.length; i++) {
    const event = completes[i]
    if (isFileCompletionEvent(event)) {
      outstanding.delete(path.resolve(event.data.file))
    }
    if (i < completes.length - 1) {
      assert.ok(
        outstanding.has(resolvedFixture),
        `file must remain in outstanding before the final wrapper complete (step ${i})`,
      )
    }
  }

  assert.equal(outstanding.size, 0, 'file must be removed from outstanding after the final wrapper complete')
})

test('WHAT[verification-system-006] P6-REPORTER-003: drainTestStream contract and inner:drained event delivery', async () => {
  // 1. Error branch: PassThrough destroyed with error
  const sendMessages = []
  const errorStream = new PassThrough()
  const drainErrorPromise = drainTestStream({
    stream: errorStream,
    send: (msg) => sendMessages.push(msg),
  })
  errorStream.destroy(new Error('forced stream explosion'))
  const errorResult = await drainErrorPromise

  assert.equal(errorResult.drained, false, 'must return drained:false on stream error')
  assert.ok(errorResult.error instanceof Error, 'must return error object')
  assert.equal(errorResult.error.message, 'forced stream explosion')
  assert.equal(sendMessages.length, 1, 'must send exactly one runner:error IPC message')
  assert.equal(sendMessages[0].type, 'runner:error')
  assert.equal(sendMessages[0].data?.message, 'forced stream explosion')
  assert.ok(
    !sendMessages.some((m) => m.type === 'inner:drained'),
    'must not send inner:drained on error',
  )

  // 2. End branch: PassThrough ended cleanly
  const endMessages = []
  const endStream = new PassThrough()
  endStream.resume() // Flowing mode so 'end' emits upon end()
  const drainEndPromise = drainTestStream({
    stream: endStream,
    send: (msg) => endMessages.push(msg),
  })
  endStream.end()
  const endResult = await drainEndPromise

  assert.equal(endResult.drained, true, 'must return drained:true on clean end')
  assert.equal(endResult.error, null, 'must return error:null on clean end')
  assert.equal(endMessages.length, 0, 'must not send error on clean end')

  // 3. Real child spawn: run-inner with fixture completes cleanly and delivers runner:summary before inner:drained
  const fixturePath = path.join(here, 'support/fixtures/all-pass.fixture.mjs')
  const child = spawn(process.execPath, [innerRunner, fixturePath], {
    stdio: ['ignore', 'ignore', 'ignore', 'ipc'],
    env: cleanEnv(),
  })

  const childMessages = []
  child.on('message', (msg) => childMessages.push(msg))

  const exitCode = await new Promise((res) => child.on('exit', res))
  assert.equal(exitCode, 0, 'child must exit 0 on clean run')

  const summaryIndex = childMessages.findIndex((m) => m.type === 'runner:summary')
  const drainedIndex = childMessages.findIndex((m) => m.type === 'inner:drained')

  assert.ok(summaryIndex !== -1, 'must receive runner:summary event from child')
  assert.ok(drainedIndex !== -1, 'must receive inner:drained event from child')
  assert.ok(summaryIndex < drainedIndex, 'runner:summary must precede inner:drained')

  const summaryData = childMessages[summaryIndex].data
  assert.equal(summaryData.filesCompleted, 1)
  assert.equal(summaryData.passed, 1)
  assert.equal(summaryData.failed, 0)
})

test('WHAT[verification-system-006] T6-STATE-001: TestRunState handles identical leaf names across different files without collision', () => {
  const state = createRunState()
  const file1 = path.resolve('/path/to/test-a.mjs')
  const file2 = path.resolve('/path/to/test-b.mjs')

  applyEvent(state, {
    type: 'test:pass',
    data: { name: 'common name', file: file1, nesting: 0, details: { duration_ms: 10 } },
  })
  applyEvent(state, {
    type: 'test:fail',
    data: { name: 'common name', file: file2, nesting: 0, details: { duration_ms: 20, error: new Error('err in b') } },
  })

  const sum = summarize(state)
  assert.equal(sum.files, 2)
  assert.equal(sum.passed, 1)
  assert.equal(sum.failed, 1)
  assert.equal(sum.leafDurations.length, 2)
  assert.equal(sum.failures.length, 1)
  assert.equal(sum.failures[0].file, file2)
})

test('WHAT[verification-system-006] T6-STATE-002: TestRunState differentiates nested subtests with same name under same file', () => {
  const state = createRunState()
  const file = path.resolve('/path/to/nested.mjs')

  // test 1 at nesting 0, subtest 1 at nesting 1
  applyEvent(state, {
    type: 'test:pass',
    data: { name: 'work item', file, nesting: 0, testId: 1, details: { duration_ms: 5 } },
  })
  applyEvent(state, {
    type: 'test:pass',
    data: { name: 'work item', file, nesting: 1, testId: 2, parentId: 1, details: { duration_ms: 3 } },
  })

  const sum = summarize(state)
  assert.equal(sum.files, 1)
  assert.equal(sum.passed, 2, 'both leaf tests must be counted individually without deduplication collision')
  assert.equal(sum.failed, 0)
  assert.equal(sum.leafDurations.length, 2)
})

test('WHAT[verification-system-006] T6-STATE-003: container suite failures count into containerFailures and not failed count', () => {
  const state = createRunState()
  const file = path.resolve('/path/to/suite.mjs')

  // 1 pass leaf
  applyEvent(state, {
    type: 'test:pass',
    data: { name: 'leaf 1', file, nesting: 1, details: { type: 'test', duration_ms: 5 } },
  })

  // 1 fail leaf
  applyEvent(state, {
    type: 'test:fail',
    data: {
      name: 'leaf 2',
      file,
      nesting: 1,
      details: {
        type: 'test',
        duration_ms: 10,
        error: { code: 'ERR_TEST_FAILURE', failureType: 'testCodeFailure', cause: new Error('leaf 2 fail') },
      },
    },
  })

  // Suite container failure (subtestsFailed)
  applyEvent(state, {
    type: 'test:fail',
    data: {
      name: 'outer suite',
      file,
      nesting: 0,
      details: {
        type: 'suite',
        duration_ms: 15,
        error: { code: 'ERR_TEST_FAILURE', failureType: 'subtestsFailed', cause: '1 subtest failed' },
      },
    },
  })

  const sum = summarize(state)
  assert.equal(sum.passed, 1, 'passed must be 1')
  assert.equal(sum.failed, 1, 'failed must be 1 (only the leaf fail, not the suite fail)')
  assert.equal(sum.containerFailures, 1, 'containerFailures must record the suite failure')
  assert.equal(sum.failures.length, 1, 'failures list only contains leaf failure')
  assert.equal(sum.failures[0].name, 'leaf 2')
})

test('WHAT[verification-system-006] T6-STATE-004: createCompactReporter shares state with TestRunState instance', async () => {
  const injectedState = createRunState()
  const reporter = createCompactReporter({
    state: injectedState,
    stdout: { write() {} },
    stderr: { write() {} },
  })

  assert.ok(reporter.state instanceof TestRunState, 'reporter must expose its state')
  assert.equal(reporter.state, injectedState, 'reporter must use the injected state instance')

  const targetFile = path.resolve('/test/shared-state.mjs')
  async function* eventSrc() {
    yield {
      type: 'test:pass',
      data: { name: 'leaf', file: targetFile, nesting: 0, details: { duration_ms: 12 } },
    }
    yield {
      type: 'test:complete',
      data: { name: targetFile, file: targetFile, nesting: 0 },
    }
  }

  for await (const _ of reporter(eventSrc())) {}

  const sum = summarize(injectedState)
  assert.equal(sum.passed, 1)
  assert.equal(sum.filesCompleted, 1)
})

test('WHAT[verification-system-006] T6-STATE-005: supervisor aborts when inner runner fails without summary or with runner:error', async () => {
  const fakeInner = path.join(here, 'support/fixtures/all-pass.fixture.mjs')

  // Running an inner that is NOT run-inner.mjs means it will not send runner:summary
  // superviseNodeTest will log error and exit(1)
  const child = spawn(process.execPath, [
    '-e',
    `
    import { superviseNodeTest } from './requirements/verification-system/tests/e2e/support/supervise-node-test.mjs';
    await superviseNodeTest({
      files: ['requirements/verification-system/tests/support/fixtures/all-pass.fixture.mjs'],
      label: 'test-no-summary',
      silenceMs: 2000,
      inner: '${fakeInner}',
    });
    `,
  ], {
    stdio: ['ignore', 'pipe', 'pipe'],
    env: cleanEnv(),
  })

  let stderr = ''
  child.stderr.on('data', (chunk) => {
    stderr += chunk.toString()
  })

  const exitCode = await new Promise((res) => child.on('exit', res))
  assert.equal(exitCode, 1, 'supervisor must exit 1 when inner runner emits no summary')
  assert.match(stderr, /failed to provide authoritative summary/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createVirtualClock } = await import("./support/temporal-harness.mjs");
const { Watchdog } = await import("./e2e/support/watchdog.js");
const { WATCHDOG_TIMEOUT_MS } = await import("./e2e/support/time-budget.js");

function createVirtualTimers(clock) {
  const handles = new Map()
  let nextId = 1
  return {
    schedule(ms, cb) {
      const id = nextId++
      const h = clock.port.delay(ms)
      let active = true
      handles.set(id, {
        cancel: () => {
          active = false
          h.cancel()
        },
      })
      h.delay().then(() => {
        if (active) {
          handles.delete(id)
          cb()
        }
      })
      return id
    },
    cancel(id) {
      const entry = handles.get(id)
      if (entry) {
        handles.delete(id)
        entry.cancel()
      }
    },
  }
}
function createHarness(opts = {}) {
  const clock = createVirtualClock()
  const timers = createVirtualTimers(clock)
  const diagnostics = []
  let terminated = false
  let terminateCount = 0

  const deps = {
    clock: { nowMs: () => clock.nowMs() },
    timers,
    diagnostic: {
      write: (msg) => {
        diagnostics.push(msg)
      },
    },
    terminate: () => {
      terminated = true
      terminateCount += 1
    },
  }

  const watchdog = new Watchdog({
    timeoutMs: opts.timeoutMs ?? 1000,
    label: opts.label ?? 'test-dog',
    onTimeout: opts.onTimeout,
    deps,
  })

  return {
    clock,
    timers,
    diagnostics,
    watchdog,
    get terminated() {
      return terminated
    },
    get terminateCount() {
      return terminateCount
    },
    async advance(ms, flushRounds = 3) {
      clock.advance(ms)
      for (let r = 0; r < flushRounds; r += 1) await Promise.resolve()
      await Promise.resolve()
    },
  }
}

test('WHAT[verification-system-006] watchdog fires on silence after exact timeoutMs', async () => {
  const h = createHarness({ timeoutMs: 500, label: 'dog-silence' })

  await h.advance(499)
  assert.equal(h.terminated, false, 'must not fire before timeout')
  assert.equal(h.diagnostics.length, 0)

  await h.advance(1)
  assert.equal(h.terminated, true, 'must terminate at timeout')
  assert.equal(h.terminateCount, 1)
  assert.ok(h.diagnostics.some((d) => d.includes("WATCHDOG: 'dog-silence' silent for 500ms")))
})
test('WHAT[verification-system-006] blocking renew pushes timeout forward indefinitely', async () => {
  const h = createHarness({ timeoutMs: 300, label: 'dog-renew' })

  await h.advance(200)
  assert.equal(h.terminated, false)
  h.watchdog.advance({ reason: 'step-1', lane: 'worker', blocking: true })

  // Another 200ms passed; total 400ms > initial 300ms, but reset at 200ms
  await h.advance(200)
  assert.equal(h.terminated, false)
  h.watchdog.advance({ reason: 'step-2', lane: 'worker', blocking: true })

  await h.advance(250)
  assert.equal(h.terminated, false)

  // Now let it stay silent for 300ms from last progress
  await h.advance(50)
  assert.equal(h.terminated, true)
  assert.ok(h.diagnostics.some((d) => d.includes('2 blocking progress update(s)')))
  assert.ok(h.diagnostics.some((d) => d.includes('last progress: step-2 lane=worker')))
})
test('WHAT[verification-system-006] background advance records info but does not renew timeout', async () => {
  const h = createHarness({ timeoutMs: 400, label: 'dog-background' })

  await h.advance(100)
  h.watchdog.advance({ reason: 'bg-step', lane: 'sidecar', blocking: false })
  assert.equal(h.terminated, false)

  await h.advance(200)
  h.watchdog.advance({ reason: 'bg-step-2', lane: 'sidecar', blocking: false })
  assert.equal(h.terminated, false)

  // Total elapsed 400ms: background advances did not push timeoutMs
  await h.advance(100)
  assert.equal(h.terminated, true)
  assert.ok(h.diagnostics.some((d) => d.includes('0 blocking progress update(s)')))
  assert.ok(h.diagnostics.some((d) => d.includes('background progress 100ms ago: bg-step-2 lane=sidecar (2 background update(s), none of them renewals)')))
})
test('WHAT[verification-system-006] stop permanently disarms watchdog with no subsequent fire', async () => {
  const h = createHarness({ timeoutMs: 200, label: 'dog-stopped' })

  await h.advance(100)
  h.watchdog.stop()

  await h.advance(500)
  assert.equal(h.terminated, false, 'stopped watchdog must never terminate')
  assert.equal(h.diagnostics.length, 0)

  // advance and setWindow after stop must be safe no-ops
  h.watchdog.advance({ reason: 'post-stop', lane: 'worker', blocking: true })
  h.watchdog.setWindow(50)
  await h.advance(500)
  assert.equal(h.terminated, false)
})
test('WHAT[verification-system-006] setWindow(null) restores centralized default WATCHDOG_TIMEOUT_MS', async () => {
  const h = createHarness({ timeoutMs: 500, label: 'dog-window' })

  // Widen to 2000ms
  h.watchdog.setWindow(2000)
  await h.advance(1000)
  assert.equal(h.terminated, false, 'widened window allows 1000ms')

  // setWindow(null) restores WATCHDOG_TIMEOUT_MS (e.g. 5000ms)
  h.watchdog.setWindow(null)
  assert.equal(h.watchdog._timeoutMs, WATCHDOG_TIMEOUT_MS)

  // Advance by WATCHDOG_TIMEOUT_MS - 1
  await h.advance(WATCHDOG_TIMEOUT_MS - 1)
  assert.equal(h.terminated, false)

  // 1ms more fires at the centralized default limit
  await h.advance(1)
  assert.equal(h.terminated, true)
  assert.ok(h.diagnostics.some((d) => d.includes(`(limit ${WATCHDOG_TIMEOUT_MS}ms)`)))
})
test('WHAT[verification-system-006] timeout fires at most once even if clock continues to advance', async () => {
  const h = createHarness({ timeoutMs: 300, label: 'dog-once' })

  await h.advance(300)
  assert.equal(h.terminateCount, 1)

  await h.advance(1000)
  assert.equal(h.terminateCount, 1, 'terminate must not be called repeatedly')
})
test('WHAT[verification-system-006] diagnostic is flushed before terminate and onTimeout is bounded', async () => {
  const executionOrder = []
  const clock = createVirtualClock()
  const timers = createVirtualTimers(clock)

  let onTimeoutResolved = false
  const onTimeout = async () => {
    executionOrder.push('onTimeout:start')
    onTimeoutResolved = true
    executionOrder.push('onTimeout:done')
  }

  const deps = {
    clock: { nowMs: () => clock.nowMs() },
    timers,
    diagnostic: {
      write: (msg) => {
        executionOrder.push(`diagnostic:${msg.slice(0, 8)}`)
      },
    },
    terminate: () => {
      executionOrder.push('terminate')
    },
  }

  const watchdog = new Watchdog({
    timeoutMs: 200,
    label: 'dog-order',
    onTimeout,
    deps,
  })

  clock.advance(200)
  // Flush microtask ticks through Promise.race and onTimeout async completion
  for (let i = 0; i < 5; i += 1) await Promise.resolve()
  await Promise.resolve()

  assert.equal(onTimeoutResolved, true)
  assert.deepEqual(executionOrder, [
    'diagnostic:WATCHDOG',
    'onTimeout:start',
    'onTimeout:done',
    'terminate',
  ])
})
}

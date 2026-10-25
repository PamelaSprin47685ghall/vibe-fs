import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const foldSurface = await import("../../../dist/Context/Companion/FoldSurface.js");
const obligation = await import("../../../dist/Persistence/Journal/ObligationJournalSurface.js");
const journalSurface = await import("../../../dist/Persistence/Journal/Surface.js");
const { acceptAuthorityRoot, bindManagedChild, lifecycleWorkView, openIncumbency, withExecutablePlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");
const { integrationTest } = await import("../../verification-system/tests/support/tier-gate.mjs");

// All envelopes cross the registered FoldSurface. The checkpoint fact carries
// only the call identity (context-compression-028: K is the fixed module
// constant, not per-fact data). Sequence fields use BigInt like the
// durable-events/015 fixtures they are modelled on.
const envelope = (seq, session, fact, run = null) => ({
  runtime: 'rt-004',
  seq,
  observedAt: '2026-01-01T00:00:00Z',
  id: `evt-004-${seq}`,
  session,
  run,
  fact,
})

const openingCaptured = (session, assignment) => ({
  family: 'Companion',
  case: 'OpeningPromptCaptured',
  payload: { SessionId: session, AssignmentText: assignment, AuthoritativeRequirements: ['req-004'], ProviderRun: null },
})

const tracePartAppended = (session) => ({
  family: 'Companion',
  case: 'XTracePartAppended',
  payload: {
    SessionId: session,
    CursorSequence: 1n,
    Role: 'assistant',
    Provenance: 'g:0/provider',
    Turn: 0,
    PartIndex: 0,
    Kind: 'text',
    ToolName: null,
    ProviderRun: 'msg-004',
    ToolCallId: null,
    HostToolPartId: null,
    TextRef: 'blob-004',
    TextDigest: 'digest-004',
  },
})

const terminalCaptured = (session) => ({
  family: 'Companion',
  case: 'TerminalOutputCaptured',
  payload: { SessionId: session, TextRef: 'blob-terminal-004', TextDigest: 'sha-terminal-004', ProviderRun: 'msg-terminal-004' },
})

const contextReanchor = (session) => ({
  family: 'Context',
  case: 'ContextReanchored',
  payload: {
    SessionId: session,
    PreviousEpochId: 0,
    NextEpochId: 1,
    ObservedCompactionRun: 'msg-reanchor-004',
  },
})

const checkpoint = (session, toolCallId) => ({
  family: 'Context',
  case: 'ViewCheckpointCommitted',
  payload: { SessionId: session, ToolCallId: toolCallId },
})

test('WHAT[obligation-ledger-004] ViewCheckpointCommitted preserves observable companion, trace, prefix-epoch and terminal state and isolates checkpoint windows', () => {
  // A non-empty, observable pre-state: the session already carries a linked
  // companion, a captured opening, a trace part, a terminal capture, and a
  // committed context reanchor (real state the checkpoint must not
  // touch). Folding the same prefix without the checkpoint gives the
  // reference state to compare against.
  const prefix = [
    envelope(1, 'ses-a', openingCaptured('ses-a', 'do the entrusted work')),
    envelope(2, 'ses-a', tracePartAppended('ses-a')),
    envelope(3, 'ses-a', terminalCaptured('ses-a'), 'msg-terminal-004'),
    envelope(4, 'ses-a', contextReanchor('ses-a'), 'msg-reanchor-004'),
  ]
  const before = foldSurface.fold(prefix)
  assert.equal(before.ok, true, 'the pre-state folds Ok')
  const beforeSession = before.value.sessions['ses-a']

  assert.deepEqual(
    beforeSession.XTrace.Opening,
    { AssignmentText: 'do the entrusted work', AuthoritativeRequirements: ['req-004'] },
    'the pre-state carries a captured opening',
  )
  assert.deepEqual(beforeSession.XTrace.Parts, [{
    CursorSequence: 1n,
    Provenance: 'g:0/provider',
    Role: 'assistant',
    Kind: 'text',
    TextRef: 'blob-004',
    TextDigest: 'digest-004',
  }], 'the pre-state carries a non-empty trace part')
  assert.deepEqual(
    beforeSession.XTrace.LatestTerminal,
    { FrontierSequence: 2n, ProviderRun: 'msg-terminal-004', TextDigest: 'sha-terminal-004', TextRef: 'blob-terminal-004' },
    'the pre-state carries a terminal capture',
  )
  if (beforeSession.PrefixEpoch) {
    assert.equal(Number(beforeSession.PrefixEpoch.EpochId), 1, 'the prefix epoch advanced to 1')
  }

  // ViewCheckpointCommitted is retired and fails closed
  assert.throws(
    () => foldSurface.fold([...prefix, envelope(7, 'ses-a', checkpoint('ses-a', 'call-1'))]),
    /unknown context fact 'ViewCheckpointCommitted'/,
  )
  assert.equal(before.value.viewCheckpoints, undefined, 'no checkpoint window exists')

  // The retired magic-todo surface cannot write or read a todo list from
  // facts: append refuses and snapshot yields nothing (obligation-ledger-007).
  const retired = obligation.appendMagicTodo({})
  assert.equal(retired.ok, false)
  assert.match(retired.error, /retired/)
  assert.equal(obligation.snapshotMagicTodo({}), null)
})

const terminalEvent = (sessionID, callID, status) => ({
  event: {
    type: 'message.part.updated',
    properties: {
      sessionID,
      part: {
        type: 'tool',
        tool: 'todowrite-for-engineer',
        callID,
        state: {
          status,
          input: {
            program: 'class Js extends JsProgram { async run() { return {}; } }',
            prefixUntil: 0,
            carry: [],
          },
        },
      },
    },
  },
})

const todoCall = (hooks, sessionID, callID) => {
  const output = {
    args: {
      program: 'class Js extends JsProgram { async run() { return {}; } }',
      prefixUntil: 0,
      carry: [],
    },
  }
  return hooks['tool.execute.before']({ tool: 'todowrite-for-engineer', sessionID, callID }, output).then(() => output)
}

// Real owner entry points build the non-empty pre-states (Manager ruling on
// 004): PromptAuthority via the dispatch surface's authority-root acceptance,
// Handles via the production HandleLinked journal append, Relay via the real
// RelayTransaction append, Companion and the XTrace terminal via the journal
// surface's Companion facts. The checkpoint itself crosses the same
// completed-terminal path 005 proved. The observation port forwards every
// session slice, so a deep comparison over all of them turns any non-checkpoint
// mutation — e.g. the handles being cleared — into a red.
integrationTest('WHAT[obligation-ledger-004] the completed-terminal checkpoint leaves every non-checkpoint projection slice untouched', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ol-004-slices'
    const childID = 'ol-004-child'
    const callID = 'ol-004-call'

    await acceptAuthorityRoot(runtime, sessionID, 'engineer')
    await bindManagedChild(runtime, sessionID, childID, 'engineer')
    await openIncumbency(runtime, sessionID)
    await journalSurface.JournalSurface_appendAgent(
      runtime.journal,
      { kind: 'Session', session: sessionID },
      null,
      {
        family: 'Companion',
        case: 'TerminalOutputCaptured',
        payload: { SessionId: sessionID, TextRef: 'blob-ol-004-terminal', TextDigest: 'digest-ol-004-terminal', ProviderRun: 'run-ol-004' },
      },
    )

    const before = journalSurface.JournalSurface_snapshot(runtime.journal)
    const beforeSlices = before.sessionProjections[sessionID]

    // The pre-states are real, not vacuous — an empty view would make the
    // later preservation claims vacuous.
    assert.ok(beforeSlices.promptAuthority?.activeLogicalRun, 'the pre-state carries an active logical run')
    assert.equal(beforeSlices.handles?.handleCount, 1, 'the pre-state carries one linked handle')
    assert.equal(beforeSlices.handles?.handles[0]?.childSessionId, childID, 'the handle is the linked child session')
    const lifecycle = await lifecycleWorkView(runtime, sessionID)
    assert.equal(lifecycle.found, true, 'the pre-state carries one lifecycle work')
    assert.equal(lifecycle.active, true, 'the lifecycle work carries an active incumbency')
    assert.ok(beforeSlices.xTrace?.latestTerminalPresent, 'the pre-state carries a captured terminal')

    // Completed terminal of todowrite-for-engineer emits no checkpoint window and leaves all slices untouched.
    await todoCall(hooks, sessionID, callID)
    await hooks.event(terminalEvent(sessionID, callID, 'completed'))

    const after = journalSurface.JournalSurface_snapshot(runtime.journal)

    assert.equal(before.viewCheckpoints, undefined, 'no checkpoint window existed before the completed terminal')
    assert.equal(after.viewCheckpoints, undefined, 'completed terminal emits no checkpoint window')

    // Every forwarded non-checkpoint slice keeps its exact value; slices with
    // no pre-state keep their empty shape too.
    const afterSlices = after.sessionProjections[sessionID]
    const sliceNames = Object.keys(afterSlices)
    assert.ok(sliceNames.includes('companion'))
    assert.ok(sliceNames.includes('xTrace'))
    assert.ok(sliceNames.includes('handles'))
    assert.ok(sliceNames.includes('prefixEpoch'))
    assert.deepEqual(Object.keys(afterSlices).sort(), sliceNames)
    for (const slice of sliceNames) {
      assert.deepEqual(afterSlices[slice], beforeSlices[slice], `slice ${slice} survives the checkpoint untouched`)
    }
    assert.deepEqual(await lifecycleWorkView(runtime, sessionID), lifecycle, 'the independent lifecycle work also stays unchanged')

    // Other sessions' projections are untouched as well.
    for (const [sessionId, slices] of Object.entries(before.sessionProjections)) {
      if (sessionId !== sessionID) {
        assert.deepEqual(after.sessionProjections[sessionId], slices, `session ${sessionId} survives the checkpoint untouched`)
      }
    }
  })
})
}


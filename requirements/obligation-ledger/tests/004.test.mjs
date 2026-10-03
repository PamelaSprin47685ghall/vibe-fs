import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const foldSurface = await import("../../../dist/Context/Companion/FoldSurface.js");
const obligation = await import("../../../dist/Persistence/Journal/ObligationJournalSurface.js");
const journalSurface = await import("../../../dist/Persistence/Journal/Surface.js");
const { acceptAuthorityRoot, bindManagedChild, openIncumbency, withExecutablePlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");
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

const bloggerLinked = (session, bloggerSession) => ({
  family: 'Companion',
  case: 'CompanionBloggerLinked',
  payload: { SessionId: session, BloggerSessionId: bloggerSession, BloggerAgent: 'blogger' },
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

const blogEntry = (session, bloggerSession) => ({
  family: 'Context',
  case: 'BlogObservationCommitted',
  payload: {
    SessionId: session,
    BloggerSessionId: bloggerSession,
    RequestId: 'req-004-entry',
    FrameEpochId: 0,
    PreviousIngestedThroughSequence: 0n,
    NextIngestedThroughSequence: 1n,
    PreviousCoverableTurnCutoffExclusive: 0,
    NextCoverableTurnCutoffExclusive: 1,
    NextCoveredPrefixDigest: 'digest-004',
    TextRef: 'blob-entry-004',
    TextDigest: 'sha-entry-004',
    ProviderRun: 'msg-entry-004',
    ToolCallIds: [],
    TipRuleId: 'tip-004',
    FieldNameAtCommit: 'field-004',
    EvidenceRef: null,
    ObservedPrefixEpochId: 0,
  },
})

const prefixRebase = (session) => ({
  family: 'Context',
  case: 'PrefixRebaseCommitted',
  payload: {
    SessionId: session,
    PreviousEpochId: 0,
    NextEpochId: 1,
    FrozenRecordPrefixRef: 'blob-frozen-004',
    FrozenRecordPrefixDigest: 'frozen-004',
    CutoffExclusive: 1,
    CoveredPrefixDigest: 'prefix-004',
    SealRoot: 'seal-004',
    SyntheticMessageId: 'synthetic-004',
    ProbeId: 'probe-004',
    SolvingProviderRun: 'msg-rebase-004',
  },
})

const checkpoint = (session, toolCallId) => ({
  family: 'Context',
  case: 'TodoCheckpointCommitted',
  payload: { SessionId: session, ToolCallId: toolCallId },
})

test('WHAT[obligation-ledger-004] TodoCheckpointCommitted preserves observable companion, trace, blog, prefix-epoch and terminal state and isolates checkpoint windows', () => {
  // A non-empty, observable pre-state: the session already carries a linked
  // companion, a captured opening, a trace part, a terminal capture, a blog
  // entry and a committed prefix rebase (real state the checkpoint must not
  // touch). Folding the same prefix without the checkpoint gives the
  // reference state to compare against.
  const prefix = [
    envelope(1, 'ses-a', bloggerLinked('ses-a', 'ses-blog')),
    envelope(2, 'ses-a', openingCaptured('ses-a', 'do the entrusted work')),
    envelope(3, 'ses-a', tracePartAppended('ses-a')),
    envelope(4, 'ses-a', terminalCaptured('ses-a'), 'msg-terminal-004'),
    envelope(5, 'ses-a', blogEntry('ses-a', 'ses-blog'), 'msg-entry-004'),
    envelope(6, 'ses-a', prefixRebase('ses-a'), 'msg-rebase-004'),
  ]
  const before = foldSurface.fold(prefix)
  assert.equal(before.ok, true, 'the pre-state folds Ok')
  const beforeSession = before.value.sessions['ses-a']

  // The pre-state really carries the expected content — an empty or null
  // view would make the later preservation claims vacuous.
  assert.equal(beforeSession.Companion.BloggerSessionId, 'ses-blog', 'the pre-state carries a linked companion')
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
  assert.ok(beforeSession.Blog, 'the pre-state carries a blog projection')
  assert.equal(beforeSession.Blog.FrameCount, 1, 'the blog projection has one frame')
  assert.deepEqual(
    beforeSession.Blog.Coverage,
    { CoverableTurnCutoffExclusive: 1, CoveredPrefixDigest: 'digest-004', IngestedThroughSequence: 1n },
    'the blog projection carries the entry coverage',
  )
  assert.ok(beforeSession.PrefixEpoch, 'the pre-state carries a prefix epoch')
  assert.ok(beforeSession.Enforcement, 'the enforcement pre-state is non-empty after the blog entry fold')
  assert.equal(beforeSession.Enforcement.cycleCount, 1, 'the enforcement projection carries one committed cycle')
  assert.equal(Number(beforeSession.PrefixEpoch.EpochId), 1, 'the prefix epoch advanced to 1')
  assert.equal(beforeSession.PrefixEpoch.Snapshot.CutoffExclusive, 1, 'the prefix snapshot carries the rebase cutoff')

  // Fold the checkpoint on top of the same prefix: the compression window
  // records the checkpoint call identity, and every protected slice keeps
  // its exact values — an implementation that cleared or overwrote the
  // existing state would make these comparisons fail.
  const after = foldSurface.fold([...prefix, envelope(7, 'ses-a', checkpoint('ses-a', 'call-1'))])
  assert.equal(after.ok, true, 'checkpoint fold is Ok')

  const window = after.value.todoCheckpoints['ses-a']
  assert.ok(window, 'the compression window exists for the session')
  assert.deepEqual(window.checkpoints, [{ callId: 'call-1' }], 'the window records only the checkpoint call identity')

  const afterSession = after.value.sessions['ses-a']
  assert.equal(afterSession.Companion.BloggerSessionId, 'ses-blog', 'the linked companion survives the checkpoint untouched')
  assert.deepEqual(afterSession.XTrace.Opening, beforeSession.XTrace.Opening, 'the captured opening survives the checkpoint untouched')
  assert.deepEqual(afterSession.XTrace.Parts, beforeSession.XTrace.Parts, 'the trace parts survive the checkpoint untouched')
  assert.deepEqual(afterSession.XTrace.LatestTerminal, beforeSession.XTrace.LatestTerminal, 'the terminal capture survives the checkpoint untouched')
  assert.deepEqual(afterSession.Blog, beforeSession.Blog, 'the blog projection survives the checkpoint untouched')
  assert.deepEqual(afterSession.PrefixEpoch, beforeSession.PrefixEpoch, 'the prefix epoch survives the checkpoint untouched')
  assert.deepEqual(afterSession.Enforcement, beforeSession.Enforcement, 'the enforcement projection survives the checkpoint untouched')

  // A checkpoint for another session writes only that session's window.
  const crossSession = foldSurface.fold([
    ...prefix,
    envelope(7, 'ses-a', checkpoint('ses-a', 'call-1')),
    envelope(8, 'ses-b', checkpoint('ses-b', 'call-b')),
  ])
  assert.equal(crossSession.ok, true)
  assert.deepEqual(crossSession.value.todoCheckpoints['ses-b'].checkpoints, [{ callId: 'call-b' }])
  assert.deepEqual(crossSession.value.todoCheckpoints['ses-a'], window, 'another session\'s checkpoint preserves the existing window')
  assert.deepEqual(crossSession.value.sessions['ses-a'].Companion, beforeSession.Companion, 'another session\'s checkpoint preserves the companion')
  assert.deepEqual(crossSession.value.sessions['ses-a'].XTrace, beforeSession.XTrace, 'another session\'s checkpoint preserves the trace')
  assert.deepEqual(crossSession.value.sessions['ses-a'].Blog, beforeSession.Blog, 'another session\'s checkpoint preserves the blog')
  assert.deepEqual(crossSession.value.sessions['ses-a'].PrefixEpoch, beforeSession.PrefixEpoch, 'another session\'s checkpoint preserves the prefix epoch')

  // Replaying the same checkpoint is idempotent on the window: it cannot
  // accumulate entries by repetition, and the protected state stays put.
  const replayed = foldSurface.replay([
    ...prefix,
    envelope(7, 'ses-a', checkpoint('ses-a', 'call-1')),
    envelope(8, 'ses-a', checkpoint('ses-a', 'call-1')),
  ])
  assert.equal(replayed.ok, true)
  assert.deepEqual(replayed.value.todoCheckpoints['ses-a'].checkpoints, [{ callId: 'call-1' }], 'replay keeps exactly one checkpoint entry')
  assert.deepEqual(replayed.value.sessions['ses-a'].Companion, beforeSession.Companion, 'replay keeps the protected companion state')
  assert.deepEqual(replayed.value.sessions['ses-a'].XTrace, beforeSession.XTrace, 'replay keeps the protected trace state')
  assert.deepEqual(replayed.value.sessions['ses-a'].Blog, beforeSession.Blog, 'replay keeps the protected blog state')
  assert.deepEqual(replayed.value.sessions['ses-a'].PrefixEpoch, beforeSession.PrefixEpoch, 'replay keeps the protected prefix epoch')

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
      part: { type: 'tool', tool: 'todowrite', callID, state: { status } },
    },
  },
})

const todoCall = (hooks, sessionID, callID) => {
  const output = { args: { todos: [{ content: 'native todo work', status: 'in_progress', priority: 'high' }] } }
  return hooks['tool.execute.before']({ tool: 'todowrite', sessionID, callID }, output).then(() => output)
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
    const bloggerSession = 'ol-004-blogger'

    await acceptAuthorityRoot(runtime, sessionID, 'engineer')
    await bindManagedChild(runtime, sessionID, childID, 'engineer')
    await openIncumbency(runtime, sessionID)
    await journalSurface.JournalSurface_appendAgent(
      runtime.journal,
      { kind: 'Session', session: sessionID },
      null,
      {
        family: 'Companion',
        case: 'CompanionBloggerLinked',
        payload: { SessionId: sessionID, BloggerSessionId: bloggerSession, BloggerAgent: 'blogger' },
      },
    )
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
    assert.equal(beforeSlices.relay?.roadCount, 1, 'the pre-state carries one relay road')
    assert.equal(beforeSlices.relay?.roads[0]?.iterationOrdinal, 1, 'the relay road has opened once')
    assert.ok(beforeSlices.relay?.roads[0]?.activeIncumbencyPresent, 'the relay road carries an active incumbency')
    assert.equal(beforeSlices.companion?.bloggerSessionId, bloggerSession, 'the pre-state carries a linked companion')
    assert.ok(beforeSlices.xTrace?.latestTerminalPresent, 'the pre-state carries a captured terminal')

    // The checkpoint: the same exact completed-terminal evidence 005 proved.
    await todoCall(hooks, sessionID, callID)
    await hooks.event(terminalEvent(sessionID, callID, 'completed'))

    const after = journalSurface.JournalSurface_snapshot(runtime.journal)

    // Only the checkpoint window changed for the session.
    const beforeWindow = before.todoCheckpoints.find((entry) => entry.sessionId === sessionID)
    const afterWindow = after.todoCheckpoints.find((entry) => entry.sessionId === sessionID)
    assert.equal(beforeWindow, undefined, 'no checkpoint window existed before the completed terminal')
    assert.deepEqual(afterWindow?.checkpoints, [{ callId: callID }], 'the completed terminal commits the checkpoint')

    // Every forwarded non-checkpoint slice keeps its exact value; slices with
    // no pre-state keep their empty shape too.
    const afterSlices = after.sessionProjections[sessionID]
    const sliceNames = Object.keys(beforeSlices)
    assert.ok(sliceNames.length >= 15, 'the observation port forwards every session slice')
    for (const slice of sliceNames) {
      assert.deepEqual(afterSlices[slice], beforeSlices[slice], `slice ${slice} survives the checkpoint untouched`)
    }

    // Other sessions' projections are untouched as well.
    for (const [sessionId, slices] of Object.entries(before.sessionProjections)) {
      if (sessionId !== sessionID) {
        assert.deepEqual(after.sessionProjections[sessionId], slices, `session ${sessionId} survives the checkpoint untouched`)
      }
    }
  })
})
}

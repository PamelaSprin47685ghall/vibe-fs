import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const foldSurface = await import("../../../dist/Context/Companion/FoldSurface.js");
const obligation = await import("../../../dist/Persistence/Journal/ObligationJournalSurface.js");

// All envelopes cross the registered FoldSurface. The checkpoint fact carries
// only the call identity (context-compression-028: K is the fixed module
// constant, not per-fact data).
const envelope = (seq, session, fact) => ({
  runtime: 'rt-004',
  seq,
  observedAt: '2026-01-01T00:00:00Z',
  id: `evt-004-${seq}`,
  session,
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

const checkpoint = (session, toolCallId) => ({
  family: 'Context',
  case: 'TodoCheckpointCommitted',
  payload: { SessionId: session, ToolCallId: toolCallId },
})

test('WHAT[obligation-ledger-004] TodoCheckpointCommitted preserves observable companion and trace state and isolates checkpoint windows', () => {
  // A non-empty, observable pre-state: the session already carries a linked
  // companion, a captured opening and a trace part (real state the checkpoint
  // must not touch). Folding the same prefix without the checkpoint gives
  // the reference state to compare against.
  const prefix = [
    envelope(1, 'ses-a', bloggerLinked('ses-a', 'ses-blog')),
    envelope(2, 'ses-a', openingCaptured('ses-a', 'do the entrusted work')),
    envelope(3, 'ses-a', tracePartAppended('ses-a')),
  ]
  const before = foldSurface.fold(prefix)
  assert.equal(before.ok, true, 'the pre-state folds Ok')
  const beforeSession = before.value.sessions['ses-a']
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

  // Fold the checkpoint on top of the same prefix: the compression window
  // records the checkpoint call identity, and the protected domain state
  // keeps its exact values — an implementation that cleared or overwrote
  // the existing state would make these comparisons fail.
  const after = foldSurface.fold([...prefix, envelope(4, 'ses-a', checkpoint('ses-a', 'call-1'))])
  assert.equal(after.ok, true, 'checkpoint fold is Ok')

  const window = after.value.todoCheckpoints['ses-a']
  assert.ok(window, 'the compression window exists for the session')
  assert.deepEqual(window.checkpoints, [{ callId: 'call-1' }], 'the window records only the checkpoint call identity')

  const afterSession = after.value.sessions['ses-a']
  assert.equal(afterSession.Companion.BloggerSessionId, 'ses-blog', 'the linked companion survives the checkpoint untouched')
  assert.deepEqual(
    afterSession.XTrace.Opening,
    beforeSession.XTrace.Opening,
    'the captured opening survives the checkpoint untouched',
  )
  assert.deepEqual(afterSession.XTrace.Parts, beforeSession.XTrace.Parts, 'the trace parts survive the checkpoint untouched')

  // A checkpoint for another session writes only that session's window.
  const crossSession = foldSurface.fold([
    ...prefix,
    envelope(4, 'ses-a', checkpoint('ses-a', 'call-1')),
    envelope(5, 'ses-b', checkpoint('ses-b', 'call-b')),
  ])
  assert.equal(crossSession.ok, true)
  assert.deepEqual(crossSession.value.todoCheckpoints['ses-b'].checkpoints, [{ callId: 'call-b' }])
  assert.deepEqual(crossSession.value.todoCheckpoints['ses-a'], window, 'another session\'s checkpoint preserves the existing window')
  assert.deepEqual(crossSession.value.sessions['ses-a'].Companion, beforeSession.Companion, 'another session\'s checkpoint preserves the companion')
  assert.deepEqual(crossSession.value.sessions['ses-a'].XTrace, beforeSession.XTrace, 'another session\'s checkpoint preserves the trace')

  // Replaying the same checkpoint is idempotent on the window: it cannot
  // accumulate entries by repetition, and the protected state stays put.
  const replayed = foldSurface.replay([
    ...prefix,
    envelope(4, 'ses-a', checkpoint('ses-a', 'call-1')),
    envelope(5, 'ses-a', checkpoint('ses-a', 'call-1')),
  ])
  assert.equal(replayed.ok, true)
  assert.deepEqual(replayed.value.todoCheckpoints['ses-a'].checkpoints, [{ callId: 'call-1' }], 'replay keeps exactly one checkpoint entry')
  assert.deepEqual(replayed.value.sessions['ses-a'].Companion, beforeSession.Companion, 'replay keeps the protected companion state')
  assert.deepEqual(replayed.value.sessions['ses-a'].XTrace, beforeSession.XTrace, 'replay keeps the protected trace state')

  // The retired magic-todo surface cannot write or read a todo list from
  // facts: append refuses and snapshot yields nothing (obligation-ledger-007).
  const retired = obligation.appendMagicTodo({})
  assert.equal(retired.ok, false)
  assert.match(retired.error, /retired/)
  assert.equal(obligation.snapshotMagicTodo({}), null)
})

test.todo('WHAT[obligation-ledger-004] the checkpoint fact leaves every remaining non-checkpoint projection slice untouched (GAP-190: blog, prefix epoch and terminal evidence still need non-empty fixtures; handles, enforcement, relay, guidelines and other state are not exposed by FoldSurface)')
}

import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const foldSurface = await import("../../../dist/Context/Companion/FoldSurface.js");
const obligation = await import("../../../dist/Persistence/Journal/ObligationJournalSurface.js");

// The wire envelope crosses the registered FoldSurface; the checkpoint fact
// carries only the call identity (context-compression-028: K is the fixed
// module constant, not per-fact data).
const checkpointEnvelope = (sessionId, toolCallId, seq) => ({
  runtime: 'rt-004',
  seq,
  observedAt: '2026-01-01T00:00:00Z',
  id: `evt-004-${seq}`,
  session: sessionId,
  fact: { family: 'Context', case: 'TodoCheckpointCommitted', payload: { SessionId: sessionId, ToolCallId: toolCallId } },
})

test('WHAT[obligation-ledger-004] TodoCheckpointCommitted cannot reconstruct or overwrite Host TodoTable contents', () => {
  // Fold the checkpoint fact through the registered surface: the only
  // projection field it can touch is the compression window, and the window
  // records call identity only — never todo list content.
  const folded = foldSurface.fold([checkpointEnvelope('ses-a', 'call-1', 1)])
  assert.equal(folded.ok, true, 'checkpoint fold is Ok')

  const window = folded.value.todoCheckpoints['ses-a']
  assert.ok(window, 'the compression window exists for the session')
  assert.deepEqual(window.checkpoints, [{ callId: 'call-1' }], 'the window records only the checkpoint call identity')
  assert.equal(
    Object.keys(window.checkpoints[0]).includes('RetainCheckpoints'),
    false,
    'the checkpoint entry carries no per-fact retain depth (K is the fixed module constant)',
  )
  // The exposed state has no TodoTable field to reconstruct or overwrite.
  assert.equal(
    Object.keys(folded.value).some((key) => /todo(?!checkpoints)/i.test(key)),
    false,
    'the exposed fold state has no TodoTable field',
  )
  assert.deepEqual(folded.value.sessions, {}, 'the checkpoint fact writes no session content')

  // Replaying the same checkpoint is idempotent on the window: it cannot
  // accumulate todo content by repetition either.
  const replayed = foldSurface.replay([checkpointEnvelope('ses-a', 'call-1', 1), checkpointEnvelope('ses-a', 'call-1', 2)])
  assert.equal(replayed.ok, true)
  assert.deepEqual(replayed.value.todoCheckpoints['ses-a'].checkpoints, [{ callId: 'call-1' }], 'replay keeps exactly one checkpoint entry')

  // The retired magic-todo surface cannot write or read a todo list from
  // facts: append refuses and snapshot yields nothing (obligation-ledger-007).
  const retired = obligation.appendMagicTodo({})
  assert.equal(retired.ok, false)
  assert.match(retired.error, /retired/)
  assert.equal(obligation.snapshotMagicTodo({}), null)
})
}

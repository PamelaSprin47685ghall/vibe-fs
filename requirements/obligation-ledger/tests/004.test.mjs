import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const projection = await import("../../../dist/Composition/Durable/Projection.js");
const bridge = await import("../../../dist/Composition/Durable/DomainFamilyBridge.js");
const { ContextFactCases } = await import("../../../dist/Context/Companion/Facts.js");
const obligation = await import("../../../dist/Persistence/Journal/ObligationJournalSurface.js");

const emptyProjection = () => projection.AgentProjection_empty

// context-compression-028: the fact carries only the call identity; the
// retain depth is the fixed module constant, not per-fact data.
const checkpointFact = (sessionId, toolCallId) =>
  new ContextFactCases(4, [{ SessionId: sessionId, ToolCallId: toolCallId }])

test('WHAT[obligation-ledger-004] TodoCheckpointCommitted cannot reconstruct or overwrite Host TodoTable contents', () => {
  // The durable projection carries no TodoTable at all: the only field the
  // checkpoint fact can touch is the compression window, and the window
  // records call identity only — never todo list content.
  const before = emptyProjection()
  const folded = bridge.ContextProjectionBridge_fold(before, checkpointFact('ses-a', 'call-1'))
  assert.equal(folded.tag, 0, 'checkpoint fold is Ok')

  const after = folded.fields[0]
  for (const key of Object.keys(after)) {
    if (key === 'TodoCheckpoints') continue
    assert.equal(after[key], before[key], `fold must not touch ${key}`)
  }
  assert.equal(after.TodoCheckpoints.size, 1)
  const window = [...after.TodoCheckpoints.values()][0]
  assert.equal(window.Checkpoints.head.ToolCallId, 'call-1')
  assert.equal(
    Object.keys(window.Checkpoints.head).includes('RetainCheckpoints'),
    false,
    'the checkpoint entry carries no per-fact retain depth (K is the fixed module constant)',
  )
  assert.equal(window.Checkpoints.tail.head, null, 'exactly one checkpoint entry')
  assert.equal(
    Object.keys(after).some((key) => /todo(?!checkpoints)/i.test(key)),
    false,
    'the durable projection has no TodoTable field to reconstruct or overwrite',
  )

  // Replaying the same checkpoint is idempotent on the window: it cannot
  // accumulate todo content by repetition either.
  const replayed = bridge.ContextProjectionBridge_fold(after, checkpointFact('ses-a', 'call-1'))
  assert.equal(replayed.tag, 0)
  assert.equal(replayed.fields[0].TodoCheckpoints.size, 1)

  // The retired magic-todo surface cannot write or read a todo list from
  // facts: append refuses and snapshot yields nothing (obligation-ledger-007).
  const retired = obligation.appendMagicTodo({})
  assert.equal(retired.ok, false)
  assert.match(retired.error, /retired/)
  assert.equal(obligation.snapshotMagicTodo({}), null)
})
}

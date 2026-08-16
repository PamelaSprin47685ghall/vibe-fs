// CAUSAL-001 — an idle observation becomes a turn only after a terminal reread.

import assert from 'node:assert/strict'
import test from 'node:test'

const terminal = (userId, assistantId) => ({
  userId,
  assistantId,
  outcome: 'TurnCompleted',
})

const inProgress = (userId, assistantId) => ({
  userId,
  assistantId,
  outcome: 'TurnInProgress',
})

const reconcileAfterIdle = async ({ reads, maxCausalRereads, onTurn }) => {
  let readCount = 0
  for (const snapshot of reads.slice(0, maxCausalRereads)) {
    readCount += 1
    if (snapshot.outcome === 'TurnCompleted') {
      await onTurn(snapshot)
      return readCount
    }
  }
  return readCount
}

test('WHAT[CAUSAL-001] EXEC_reconcile_idle_before_transcript_materializes_within_causal_rereads', async () => {
  const turns = []
  const reads = [
    inProgress('user-1', 'asst-ip'),
    inProgress('user-1', 'asst-ip'),
    terminal('user-1', 'asst-terminal'),
  ]
  const readCount = await reconcileAfterIdle({
    reads,
    maxCausalRereads: 3,
    onTurn: (turn) => {
      turns.push(turn)
      return Promise.resolve()
    },
  })

  assert.equal(turns.length, 1, 'exactly one onTurn within same causal reread')
  assert.equal(turns[0].outcome, 'TurnCompleted')
  assert.ok(readCount >= 3, `expect ≥3 reads (initial + rereads); got ${readCount}`)
})

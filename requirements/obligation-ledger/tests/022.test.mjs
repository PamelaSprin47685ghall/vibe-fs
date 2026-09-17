import assert from 'node:assert/strict'
import test from 'node:test'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

const sha256 = (value) => `digest:${value}`

const life = 'manager-life'

const firstCall = 'first-call'

const secondCall = 'second-call'

const obligation = (name, work, horizon = 'near') => ({ name, horizon, work })

const ok = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.value
}

const rejected = (result) => {
  assert.equal(result.ok, false, 'expected rejection')
  return result.error
}

const localized = (callId, ordinal, frontier, digest) => ({
  toolCallId: callId,
  toolPartOrdinal: ordinal,
  todowriteCallIds: [callId],
  reviewFrontier: frontier,
  providerInputDigest: digest,
})

const items = [
  obligation('implementation', 'Implement the requested behavior.'),
  obligation('verification', 'Verify the behavior with evidence.', 'far'),
]

test('WHAT[OBLIGATION-LEDGER-022] blocks retirement suicide until plan commitment, not merely until any checkpoint', () => {
  assert.equal(todo.requirePlanCommitmentBeforeFirstSuicide(false).error.code, 'FirstSuicideWithoutCheckpoint')
  assert.equal(ok(todo.requirePlanCommitmentBeforeFirstSuicide(true)), null)
})

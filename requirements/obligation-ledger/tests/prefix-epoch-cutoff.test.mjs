import assert from 'node:assert/strict'
import test from 'node:test'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

const sha256 = (value) => `digest:${value}`
const life = 'manager-life'
const write = (call) => todo.todoWriteId(sha256, life, call)

test('WHAT[OBLIGATION-LEDGER-021] committed cutoff is supplied by one previous locator, never by scanning Accepted history', () => {
  const t1 = write('t1-call')
  assert.equal(todo.requiresLag1Rebase(null), false, 'T1 has no committed predecessor')
  assert.equal(todo.requiresLag1Rebase(t1), true, 'a later committed checkpoint has exactly one lag-1 predecessor locator')
})

test('WHAT[OBLIGATION-LEDGER-021] TodoCheckpoint evidence binds trigger plus O(1) previous committed locator', () => {
  const t1 = write('t1-call')
  const t2 = write('t2-call')
  const evidence = todo.todoCheckpointEvidence(t2, t1)
  assert.equal(evidence.kind, 'TodoCheckpoint')
  assert.equal(evidence.triggerTodoWriteId, t2)
  assert.equal(evidence.coveredBeforeTodoWriteId, t1)
})

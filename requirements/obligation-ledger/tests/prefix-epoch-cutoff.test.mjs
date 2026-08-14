// OBLIGATION-LEDGER-021: desired TodoCheckpoint cutoff consumes the O(1)
// PreviousCommittedCheckpoint locator from MagicTodoProjection. Pre-T1 planning
// checkpoints never enter this API; T1 has no predecessor; every later accepted
// checkpoint is effective committed even if its raw planComplete says false.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  requiresLag1Rebase,
  todoCheckpointEvidence,
} from '../../../dist/Domain/MagicTodoPrefixEpoch.js'
import { magicTodo, managerLifeId, toolCallId } from '../../verification-system/tests/support/domain.mjs'

const sha256 = (value) => `digest:${value}`
const life = managerLifeId('manager-life')
const write = (call) => magicTodo.todoWriteId(sha256, life, toolCallId(call))

test('OBLIGATION-LEDGER-021 committed cutoff is supplied by one previous locator, never by scanning Accepted history', () => {
  const t1 = write('t1-call')
  assert.equal(requiresLag1Rebase(undefined), false, 'T1 has no committed predecessor')
  assert.equal(requiresLag1Rebase(t1), true, 'a later committed checkpoint has exactly one lag-1 predecessor locator')
})

test('OBLIGATION-LEDGER-021 TodoCheckpoint evidence binds trigger plus O(1) previous committed locator', () => {
  const t1 = write('t1-call')
  const t2 = write('t2-call')
  const evidence = todoCheckpointEvidence(t2, t1)
  assert.equal(evidence.cases()[evidence.tag], 'TodoCheckpoint')
  assert.equal(magicTodo.todoWriteIdValue(evidence.fields[0]), magicTodo.todoWriteIdValue(t2))
  assert.equal(magicTodo.todoWriteIdValue(evidence.fields[1]), magicTodo.todoWriteIdValue(t1))
})

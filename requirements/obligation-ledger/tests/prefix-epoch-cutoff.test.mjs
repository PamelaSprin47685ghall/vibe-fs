// requirements/obligation-ledger/tests/prefix-epoch-cutoff.test.mjs
//
// OBLIGATION-LEDGER-021: the desired lag-1 cutoff is derived ONLY from the
// Accepted chain (todoCheckpointEvidence / coveredBefore / requiresLag1Rebase
// in Domain/MagicTodoPrefixEpoch.fs). No Requested Stage, no wall clock, no
// Host table: given the durable Accepted order, the previous checkpoint is
// the trigger of the next rebase; T1 has no prior.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  coveredBefore,
  requiresLag1Rebase,
  todoCheckpointEvidence,
} from '../../../dist/Domain/MagicTodoPrefixEpoch.js'
import { magicTodo, managerLifeId, toList, toolCallId } from '../../../tests/unit/support/domain.mjs'

const sha256 = (value) => `digest:${value}`
const life = managerLifeId('manager-life')
const write = (call) => magicTodo.todoWriteId(sha256, life, toolCallId(call))

test('OBLIGATION-LEDGER-021 desired cutoff is the previous Accepted checkpoint, derived purely from the chain', () => {
  const t1 = write('t1-call')
  const t2 = write('t2-call')
  const t3 = write('t3-call')
  // T1 has no prior replacement.
  assert.equal(coveredBefore(toList([t1]), t1), undefined)
  // desiredCutoff(Tk) = Before(T(k-1) tool-call) → the previous checkpoint identity.
  assert.equal(coveredBefore(toList([t1, t2, t3]), t3), t2)
  assert.equal(coveredBefore(toList([t1, t2, t3]), t2), t1)
  // Lag-1 rebase only exists once the chain has at least two Accepted checkpoints.
  assert.equal(requiresLag1Rebase(toList([t1])), false)
  assert.equal(requiresLag1Rebase(toList([t1, t2])), true)
})

test('OBLIGATION-LEDGER-021 the rebase evidence kind is TodoCheckpoint', () => {
  const t1 = write('t1-call')
  const t2 = write('t2-call')
  const evidence = todoCheckpointEvidence(toList([t1, t2]), t2)
  assert.equal(evidence.cases()[evidence.tag], 'TodoCheckpoint')
})

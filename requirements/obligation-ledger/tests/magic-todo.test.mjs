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

test('WHAT[OBLIGATION-LEDGER-001] canonical obligation wire carries no provider-visible cold state', () => {
  const wire = todo.canonicalObligationListWire(items)
  assert.doesNotMatch(wire, /"id"|"status"|"priority"/)
})

test('WHAT[OBLIGATION-LEDGER-002] canonical obligation wire is exactly name/horizon/work with stable digest input', () => {
  const wire = todo.canonicalObligationListWire(items)
  assert.equal(
    wire,
    '[{"name":"implementation","horizon":"near","work":"Implement the requested behavior."},{"name":"verification","horizon":"far","work":"Verify the behavior with evidence."}]',
  )
  assert.equal(todo.obligationListDigest(sha256, items), `digest:${wire}`)
})

test('WHAT[OBLIGATION-LEDGER-027] horizon is planning resolution, not provider-visible lifecycle state', () => {
  const wire = todo.canonicalObligationListWire([
    obligation('now', 'Close the directly actionable unit.', 'near'),
    obligation('next', 'Preserve the next meaningful outcome.', 'mid'),
    obligation('later', 'Cover the remaining outcome without premature steps.', 'far'),
  ])
  assert.match(wire, /"horizon":"near"/)
  assert.match(wire, /"horizon":"mid"/)
  assert.match(wire, /"horizon":"far"/)
  assert.doesNotMatch(wire, /"status"|"phase"|"priority"/)
})

test('WHAT[OBLIGATION-LEDGER-006] rejects blank and duplicate obligation names as call syntax', () => {
  assert.equal(rejected(todo.validateObligations([obligation('   ', 'work')])).code, 'EmptyObligationName')
  assert.equal(
    rejected(todo.validateObligations([obligation('same', 'first'), obligation('same', 'second')])).code,
    'DuplicateObligationName',
  )
})

test('WHAT[OBLIGATION-LEDGER-007] rejects different todowrite calls in one assistant message as syntax/protocol error', () => {
  assert.equal(todo.admitTodowriteBatch([firstCall, secondCall]).error.code, 'MultipleTodowriteInMessage')
  assert.equal(ok(todo.admitTodowriteBatch([firstCall, firstCall])), null)
})

test('WHAT[OBLIGATION-LEDGER-008] pure replay identity checker detects corruption for the Host fatal boundary', () => {
  const expected = {
    managerLifeId: life,
    providerInputDigest: 'provider-a',
    baseTodoDigest: 'base-a',
    toolPartOrdinal: 3,
  }
  const matching = { ...expected }
  const changed = { ...expected, providerInputDigest: 'provider-b' }

  assert.equal(ok(todo.checkPreparedReplay(expected, matching)), null)
  const error = rejected(todo.checkPreparedReplay(expected, changed))
  assert.equal(error.code, 'IdentityCorruption')
  assert.equal(error.field, 'ProviderInputDigest')
})

test('WHAT[OBLIGATION-LEDGER-012] replays an identical obligation checkpoint even while its review is outstanding (no new review from replay)', () => {
  const current = [obligation('implementation', 'Implement the requested behavior.')]
  const write = todo.todoWriteId(sha256, life, firstCall)
  const existing = {
    managerLifeId: life,
    providerInputDigest: 'provider-input',
    baseTodoDigest: todo.obligationListDigest(sha256, current),
    toolPartOrdinal: 1,
    todoWriteId: write,
  }

  const outcome = todo.admitObligations(
    sha256,
    life,
    current,
    { ok: false },
    existing,
    localized(firstCall, 1, 4, 'provider-input'),
    current,
  )
  assert.equal(outcome.kind, 'IdempotentReplay')
})

test('WHAT[OBLIGATION-LEDGER-010] fresh admission freezes Base and Submitted without a merge preview', () => {
  const current = [obligation('implementation', 'Implement the requested behavior.')]
  const submitted = [...current, obligation('verification', 'Verify the behavior with evidence.')]

  const outcome = todo.admitObligations(
    sha256,
    life,
    current,
    { ok: true },
    null,
    localized(secondCall, 2, 8, 'provider-input-2'),
    submitted,
  )
  assert.equal(outcome.kind, 'FreshPrepare')
  const prepared = outcome.value
  assert.equal(prepared.baseObligations.length, 1)
  assert.equal(prepared.proposed.length, 2)
  assert.equal('revisePreview' in prepared, false)
  assert.equal(prepared.baseDigest, todo.obligationListDigest(sha256, current))
  assert.equal(prepared.proposedDigest, todo.obligationListDigest(sha256, submitted))
})

test('WHAT[OBLIGATION-LEDGER-022] blocks Finality until plan commitment, not merely until any checkpoint', () => {
  assert.equal(todo.requirePlanCommitmentBeforeFirstSuicide(false).error.code, 'FirstSuicideWithoutCheckpoint')
  assert.equal(ok(todo.requirePlanCommitmentBeforeFirstSuicide(true)), null)
})

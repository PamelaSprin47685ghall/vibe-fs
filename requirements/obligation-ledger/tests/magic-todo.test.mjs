import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  errorResult,
  magicTodo,
  magicTodoAdmission,
  managerLifeId,
  okResult,
  toList,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'

const sha256 = (value) => `digest:${value}`
const ok = (result) => {
  assert.equal(result.tag, 0, `expected Ok, got ${result.fields?.[0]?.cases?.()[result.fields?.[0]?.tag]}`)
  return result.fields[0]
}
const error = (result) => {
  assert.equal(result.tag, 1, 'expected Error')
  return result.fields[0]
}
const obligation = (name, work) => new magicTodo.Obligation(name, work)

const life = managerLifeId('manager-life')
const firstCall = toolCallId('first-call')
const secondCall = toolCallId('second-call')

test('WHAT[OBLIGATION-LEDGER-001] canonical obligation wire carries no provider-visible cold state (no id/status/priority/reviewing)', () => {
  const items = toList([
    obligation('implementation', 'Implement the requested behavior.'),
    obligation('verification', 'Verify the behavior with evidence.'),
  ])

  const wire = magicTodo.canonicalObligationListWire(items)
  assert.doesNotMatch(wire, /"id"|"status"|"priority"|reviewing/)
})

test('WHAT[OBLIGATION-LEDGER-002] canonical obligation wire is exactly name/work with stable digest input', () => {
  const items = toList([
    obligation('implementation', 'Implement the requested behavior.'),
    obligation('verification', 'Verify the behavior with evidence.'),
  ])

  const wire = magicTodo.canonicalObligationListWire(items)
  assert.equal(
    wire,
    '[{"name":"implementation","work":"Implement the requested behavior."},{"name":"verification","work":"Verify the behavior with evidence."}]',
  )
  assert.equal(magicTodo.obligationListDigest(sha256, items), `digest:${wire}`)
})

test('WHAT[OBLIGATION-LEDGER-006] rejects blank and duplicate obligation names as call syntax', () => {
  const blank = error(magicTodo.validateObligations(toList([obligation('   ', 'work')])))
  const duplicate = error(
    magicTodo.validateObligations(
      toList([
        obligation('same', 'first'),
        obligation('same', 'second'),
      ]),
    ),
  )

  assert.equal(blank.cases()[blank.tag], 'EmptyObligationName')
  assert.equal(duplicate.cases()[duplicate.tag], 'DuplicateObligationName')
})

test('WHAT[OBLIGATION-LEDGER-007] rejects different todowrite calls in one assistant message as syntax/protocol error', () => {
  const rejected = error(magicTodo.admitTodowriteBatch(toList([firstCall, secondCall])))
  const replay = ok(magicTodo.admitTodowriteBatch(toList([firstCall, firstCall])))

  assert.equal(rejected.cases()[rejected.tag], 'MultipleTodowriteInMessage')
  assert.equal(replay, undefined)
})

test('WHAT[OBLIGATION-LEDGER-008] pure replay identity checker detects corruption for the Host fatal boundary', () => {
  const expected = new magicTodo.PreparedIdentity(life, 'provider-a', 'base-a', 3)
  const matching = new magicTodo.PreparedIdentity(life, 'provider-a', 'base-a', 3)
  const changed = new magicTodo.PreparedIdentity(life, 'provider-b', 'base-a', 3)

  assert.equal(ok(magicTodo.checkPreparedReplay(expected, matching)), undefined)
  const rejected = error(magicTodo.checkPreparedReplay(expected, changed))
  assert.equal(rejected.cases()[rejected.tag], 'IdentityCorruption')
  assert.equal(rejected.fields[0], 'ProviderInputDigest')
})

test('WHAT[OBLIGATION-LEDGER-012] replays an identical obligation checkpoint even while its review is outstanding (no new review from replay)', () => {
  const current = toList([obligation('implementation', 'Implement the requested behavior.')])
  const write = magicTodo.todoWriteId(sha256, life, firstCall)
  const existing = new magicTodoAdmission.ExistingPrepared(
    new magicTodo.PreparedIdentity(life, 'provider-input', magicTodo.obligationListDigest(sha256, current), 1),
    write,
  )
  const localized = new magicTodoAdmission.LocalizedToolCall(
    firstCall,
    1,
    toList([firstCall]),
    { Sequence: 4n },
    'provider-input',
  )

  const outcome = magicTodoAdmission.admitObligations(
    sha256,
    life,
    current,
    errorResult(undefined),
    existing,
    localized,
    current,
  )
  assert.equal(caseOf(outcome), 'IdempotentReplay')
})

test('WHAT[OBLIGATION-LEDGER-010] fresh admission freezes Base and Submitted without a merge preview', () => {
  const current = toList([obligation('implementation', 'Implement the requested behavior.')])
  const submitted = toList([
    obligation('implementation', 'Implement the requested behavior.'),
    obligation('verification', 'Verify the behavior with evidence.'),
  ])
  const localized = new magicTodoAdmission.LocalizedToolCall(
    secondCall,
    2,
    toList([secondCall]),
    { Sequence: 8n },
    'provider-input-2',
  )

  const outcome = magicTodoAdmission.admitObligations(
    sha256,
    life,
    current,
    okResult(undefined),
    undefined,
    localized,
    submitted,
  )
  assert.equal(caseOf(outcome), 'FreshPrepare')
  const prepared = outcome.fields[0]
  assert.equal(Array.from(prepared.Base).length, 1)
  assert.equal(Array.from(prepared.Proposed).length, 2)
  assert.equal('RevisePreview' in prepared, false)
  assert.equal(prepared.BaseDigest, magicTodo.obligationListDigest(sha256, current))
  assert.equal(prepared.ProposedDigest, magicTodo.obligationListDigest(sha256, submitted))
})

test('WHAT[OBLIGATION-LEDGER-022] blocks Finality until plan commitment, not merely until any checkpoint', () => {
  const missing = error(magicTodo.requirePlanCommitmentBeforeFirstSuicide(false))
  assert.equal(missing.cases()[missing.tag], 'FirstSuicideWithoutCheckpoint')
  assert.equal(ok(magicTodo.requirePlanCommitmentBeforeFirstSuicide(true)), undefined)
})

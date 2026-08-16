// EFFECT-ACCOUNTING-011 contract test（本包 NEW）：
// TodoWriteAccepted 必须携带精确的 PreparedFactRef 指名它接受的 TodoWritePrepared
// envelope；Prepared 缺失 / PreparedFactRef 失配 → 拒绝（PreparedMissingForAccept /
// IdentityCorruption）。Accepted 后 Current 立即切换，REVISE 结论不得回滚。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
  caseOf,
  eventId,
  magicTodo,
  magicTodoJournal,
  managerLifeId,
  sessionId,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'

const sha256 = (value) => `digest:${value}`
const life = managerLifeId('manager-life-011')
const managerSession = sessionId('manager-session-011')
const call = toolCallId('todo-call-011')
const cursor = (sequence) => new magicTodoJournal.XTraceCursor(BigInt(sequence))
const write = magicTodo.todoWriteId(sha256, life, call)
const preparedFactRef = eventId('prepared-fact-ref-011')

const prepared = new magicTodoJournal.TodoWritePrepared(
  managerSession,
  life,
  write,
  call,
  2,
  blobRef('base-list-011'),
  blobDigest('base-digest-011'),
  blobRef('proposal-list-011'),
  blobDigest('proposal-digest-011'),
  false,
  'provider-input-digest-011',
  cursor(10),
  'magic-v1',
)

const accepted = (ref = preparedFactRef) =>
  new magicTodoJournal.TodoWriteAccepted(
    life,
    write,
    call,
    ref,
    'provider-input-digest-011',
    'output-digest-011',
    magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
    'magic-v1',
  )

const fact = (caseName, payload) => magicTodoJournal.MagicTodoFact(caseName, [payload])
const ok = (result) => {
  assert.equal(result.tag, 0, `expected Ok, got ${JSON.stringify(result.fields?.[0])}`)
  return result.fields[0]
}
const error = (result) => {
  assert.equal(result.tag, 1, 'expected Error')
  return result.fields[0]
}

test('WHAT[EFFECT-ACCOUNTING-011] accepted_without_any_prepared_is_rejected', () => {
  // 没有 TodoWritePrepared 就 Accept：Prepared 缺失 → 拒绝，绝不静默接受。
  const rejected = error(
    magicTodoJournal.fold(eventId('env-011-1'), magicTodoJournal.empty, fact('TodoWriteAccepted', accepted())),
  )
  assert.equal(rejected.cases()[rejected.tag], 'PreparedMissingForAccept')
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_another_prepared_envelope_is_identity_corruption', () => {
  // Prepared 存在但 Accepted 指名另一个 envelope：PreparedFactRef 失配 → IdentityCorruption。
  let state = magicTodoJournal.empty
  state = ok(magicTodoJournal.fold(preparedFactRef, state, fact('TodoWritePrepared', prepared)))
  const rejected = error(
    magicTodoJournal.fold(
      eventId('env-011-2'),
      state,
      fact('TodoWriteAccepted', accepted(eventId('different-prepared-fact-ref-011'))),
    ),
  )
  assert.equal(rejected.cases()[rejected.tag], 'IdentityCorruption')
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_exact_prepared_switches_current_immediately', () => {
  // 精确指名 Prepared 的 Accepted 通过，且 Current 立即切换为 proposal list。
  let state = magicTodoJournal.empty
  state = ok(magicTodoJournal.fold(preparedFactRef, state, fact('TodoWritePrepared', prepared)))
  state = ok(
    magicTodoJournal.fold(
      eventId('env-011-3'),
      state,
      fact('TodoWriteAccepted', accepted(preparedFactRef)),
    ),
  )
  const lifeState = state.ByLife.get(managerLifeIdValue(life))
  assert.equal(lifeState.CurrentObligationsRef[0].fields[0], 'proposal-list-011')
  assert.equal(lifeState.CurrentObligationsRef[1].fields[0], 'proposal-digest-011')
})

// facade 的 managerLifeId 值提取（Fable union 单 case 包装）。
const managerLifeIdValue = (value) => value.fields?.[0] ?? value

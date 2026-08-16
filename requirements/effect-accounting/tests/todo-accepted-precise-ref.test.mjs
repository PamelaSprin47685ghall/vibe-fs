// EFFECT-ACCOUNTING-011 contract test（本包 NEW）：
// TodoWriteAccepted 必须携带精确的 PreparedFactRef 指名它接受的 TodoWritePrepared
// envelope；Prepared 缺失 / PreparedFactRef 失配 → 拒绝（PreparedMissingForAccept /
// IdentityCorruption）。Accepted 后 Current 立即切换，REVISE 结论不得回滚。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
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
const cursor = (sequence) => magicTodoJournal.cursor(sequence)
const write = magicTodo.todoWriteId(sha256, life, call)
const preparedFactRef = eventId('prepared-fact-ref-011')

const prepared = magicTodoJournal.prepared([
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
])

const accepted = (ref = preparedFactRef) =>
  magicTodoJournal.accepted([
    life,
    write,
    call,
    ref,
    'provider-input-digest-011',
    'output-digest-011',
    magicTodoJournal.physicalSuccess('LiveAfterSuccess'),
    'magic-v1',
  ])

const fact = (caseName, payload) => magicTodoJournal.fact(caseName, payload)
const fold = (event, state, value) => magicTodoJournal.foldView(event, state, value)
const acceptedState = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.state
}

test('WHAT[EFFECT-ACCOUNTING-011] accepted_without_any_prepared_is_rejected', () => {
  // 没有 TodoWritePrepared 就 Accept：Prepared 缺失 → 拒绝，绝不静默接受。
  const rejected = fold(eventId('env-011-1'), magicTodoJournal.empty, fact('TodoWriteAccepted', accepted()))
  assert.deepEqual(rejected, { ok: false, error: 'PreparedMissingForAccept' })
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_another_prepared_envelope_is_identity_corruption', () => {
  // Prepared 存在但 Accepted 指名另一个 envelope：PreparedFactRef 失配 → IdentityCorruption。
  let state = magicTodoJournal.empty
  state = acceptedState(fold(preparedFactRef, state, fact('TodoWritePrepared', prepared)))
  const rejected = fold(
    eventId('env-011-2'),
    state,
    fact('TodoWriteAccepted', accepted(eventId('different-prepared-fact-ref-011'))),
  )
  assert.deepEqual(rejected, { ok: false, error: 'IdentityCorruption' })
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_exact_prepared_switches_current_immediately', () => {
  // 精确指名 Prepared 的 Accepted 通过，且 Current 立即切换为 proposal list。
  let state = magicTodoJournal.empty
  state = acceptedState(fold(preparedFactRef, state, fact('TodoWritePrepared', prepared)))
  state = acceptedState(
    fold(
      eventId('env-011-3'),
      state,
      fact('TodoWriteAccepted', accepted(preparedFactRef)),
    ),
  )
  assert.deepEqual(magicTodoJournal.currentObligationRefs(state, life), {
    ref: 'proposal-list-011',
    digest: 'proposal-digest-011',
  })
})

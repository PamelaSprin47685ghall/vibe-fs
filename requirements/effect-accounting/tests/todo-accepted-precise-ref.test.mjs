// EFFECT-ACCOUNTING-011: TodoWriteAccepted must name the exact
// TodoWritePrepared envelope. The test crosses the obligation-ledger-owned
// Magic Todo projection surface with plain payloads.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

const sha256 = (value) => `digest:${value}`
const life = 'effect-life-011'
const incumbency = 'effect-incumbency-011'
const managerSession = 'effect-session-011'
const call = 'effect-call-011'
const preparedFactRef = 'effect-prepared-fact-ref-011'
const ids = todo.todoWriteId(sha256, incumbency, call)
const write = ids.todoWriteId ?? ids

const cursor = (sequence) => ({ Sequence: sequence })
const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
const ok = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result
}
const foldError = (result) => {
  assert.equal(result.ok, false, 'expected the projection to reject this fact')
  return result.error.code
}
let nextEvent = 0
const foldMagic = (handle, magicFact, eventId = undefined) => {
  nextEvent += 1
  return projection.MagicTodoProjectionSurface_fold(handle, eventId ?? `effect-todo-${nextEvent}`, magicFact)
}

const prepared = fact('TodoWritePrepared', {
  ManagerSessionId: managerSession,
  IncumbencyId: incumbency,
  TodoWriteId: write,
  ToolCallId: call,
  ToolPartOrdinal: 2,
  BaseTodoRef: 'effect-base-list-011',
  BaseTodoDigest: 'effect-base-digest-011',
  ProposedTodoRef: 'effect-proposal-list-011',
  ProposedTodoDigest: 'effect-proposal-digest-011',
  PlanCompleteDeclared: false,
  ProviderInputDigest: 'effect-provider-input-digest-011',
  ReviewFrontier: cursor(10),
  SemanticVersion: 'magic-v1',
})

const accepted = (preparedRef = preparedFactRef) => fact('TodoWriteAccepted', {
  IncumbencyId: incumbency,
  TodoWriteId: write,
  ToolCallId: call,
  PreparedFactRef: preparedRef,
  InputDigest: 'effect-provider-input-digest-011',
  OutputDigest: 'effect-output-digest-011',
  PhysicalSuccessEvidence: 'LiveAfterSuccess',
  SemanticVersion: 'magic-v1',
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_without_any_prepared_is_rejected', () => {
  // No TodoWritePrepared means Accept is rejected, never silently accepted.
  const handle = projection.MagicTodoProjectionSurface_create()
  assert.equal(foldError(foldMagic(handle, accepted())), 'PreparedMissingForAccept')
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_another_prepared_envelope_is_identity_corruption', () => {
  // A Prepared exists, but Accepted names another envelope: identity corruption.
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, preparedFactRef))
  assert.equal(foldError(foldMagic(handle, accepted('different-prepared-fact-ref-011'))), 'IdentityCorruption')
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_exact_prepared_switches_current_immediately', () => {
  // Naming the exact Prepared envelope succeeds and immediately switches Current
  // to the proposal list; a later review conclusion cannot roll it back.
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, preparedFactRef))
  ok(foldMagic(handle, accepted(preparedFactRef)))

  assert.deepEqual(projection.MagicTodoProjectionSurface_view(handle, incumbency).currentObligations, {
    reference: 'effect-proposal-list-011',
    digest: 'effect-proposal-digest-011',
  })
})

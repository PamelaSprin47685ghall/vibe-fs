// EFFECT-ACCOUNTING-011 §九: TodoWriteAccepted must name the exact
// TodoWritePrepared envelope. The test crosses the Review-owned Magic Todo
// surface with plain payloads and an opaque projection handle.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as todo from '../../../dist/Mission/Review/ReviewTodoSurface.js'

const sha256 = (value) => `digest:${value}`
const life = 'manager-life-011'
const managerSession = 'manager-session-011'
const call = 'todo-call-011'
const preparedFactRef = 'prepared-fact-ref-011'
const ids = todo.ids(sha256, life, call)
const write = ids.todoWriteId

const prepared = {
  ManagerSessionId: managerSession,
  ManagerLifeId: life,
  TodoWriteId: write,
  ToolCallId: call,
  ToolPartOrdinal: 2,
  BaseTodoRef: 'base-list-011',
  BaseTodoDigest: 'base-digest-011',
  ProposedTodoRef: 'proposal-list-011',
  ProposedTodoDigest: 'proposal-digest-011',
  PlanCompleteDeclared: false,
  ProviderInputDigest: 'provider-input-digest-011',
  ReviewFrontier: { Sequence: 10 },
  SemanticVersion: 'magic-v1',
}

const accepted = (preparedRef = preparedFactRef) => ({
  ManagerLifeId: life,
  TodoWriteId: write,
  ToolCallId: call,
  PreparedFactRef: preparedRef,
  InputDigest: 'provider-input-digest-011',
  OutputDigest: 'output-digest-011',
  PhysicalSuccessEvidence: 'LiveAfterSuccess',
  SemanticVersion: 'magic-v1',
})

const foldError = (result) => {
  assert.equal(result.ok, false, 'expected the fold to reject this fact')
  return result.error.code
}

const acceptedState = (projection, eventId, caseName, payload) => {
  const result = todo.fold(projection, eventId, caseName, payload)
  assert.equal(result.ok, true, result.ok ? '' : result.error.code)
  return projection
}

test('WHAT[EFFECT-ACCOUNTING-011] accepted_without_any_prepared_is_rejected', () => {
  // No TodoWritePrepared means Accept is rejected, never silently accepted.
  const projection = todo.newProjection()
  const rejected = todo.fold(projection, 'env-011-1', 'TodoWriteAccepted', accepted())
  assert.equal(foldError(rejected), 'PreparedMissingForAccept')
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_another_prepared_envelope_is_identity_corruption', () => {
  // A Prepared exists, but Accepted names another envelope: identity corruption.
  const projection = todo.newProjection()
  acceptedState(projection, preparedFactRef, 'TodoWritePrepared', prepared)

  const rejected = todo.fold(
    projection,
    'env-011-2',
    'TodoWriteAccepted',
    accepted('different-prepared-fact-ref-011'),
  )
  assert.equal(foldError(rejected), 'IdentityCorruption')
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_exact_prepared_switches_current_immediately', () => {
  // Naming the exact Prepared envelope succeeds and immediately switches Current
  // to the proposal list; a later review conclusion cannot roll it back.
  const projection = todo.newProjection()
  acceptedState(projection, preparedFactRef, 'TodoWritePrepared', prepared)
  acceptedState(projection, 'env-011-3', 'TodoWriteAccepted', accepted(preparedFactRef))

  assert.deepEqual(todo.view(projection, life).currentObligations, {
    reference: 'proposal-list-011',
    digest: 'proposal-digest-011',
  })
})

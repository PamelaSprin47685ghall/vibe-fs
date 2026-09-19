import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const projection = await import("../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js");
const codec = await import("../../../dist/Mission/Obligation/Todo/MagicTodoProjectionCodecSurface.js");
const envelope = await import("../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

const sha256 = (value) => `digest:${value}`
const life = 'manager-life'
const managerSession = 'manager-session'
const call = 'todo-call'
const write = todo.todoWriteId(sha256, life, call)
const cursor = (sequence) => ({ Sequence: sequence })
const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
const ok = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result
}
const error = (result) => {
  assert.equal(result.ok, false, 'expected projection rejection')
  return result.error
}
let nextEvent = 0
const foldMagic = (handle, magicFact, eventId = undefined) => {
  nextEvent += 1
  return projection.MagicTodoProjectionSurface_fold(handle, eventId ?? `magic-todo-${nextEvent}`, magicFact)
}
const preparedFact = ({
  managerSessionId = managerSession,
  managerLifeId = life,
  incumbencyId = life,
  todoWriteId = write,
  toolCallId = call,
  toolPartOrdinal = 2,
  baseTodoRef = 'base-list',
  baseTodoDigest = 'base-digest',
  proposedTodoRef = 'proposal-list',
  proposedTodoDigest = 'proposal-digest',
  planCompleteDeclared = false,
  providerInputDigest = 'provider-input-digest',
  reviewFrontier = 10,
  semanticVersion = 'magic-v1',
} = {}) => fact('TodoWritePrepared', {
  ManagerSessionId: managerSessionId,
  IncumbencyId: incumbencyId,
  TodoWriteId: todoWriteId,
  ToolCallId: toolCallId,
  ToolPartOrdinal: toolPartOrdinal,
  BaseTodoRef: baseTodoRef,
  BaseTodoDigest: baseTodoDigest,
  ProposedTodoRef: proposedTodoRef,
  ProposedTodoDigest: proposedTodoDigest,
  PlanCompleteDeclared: planCompleteDeclared,
  ProviderInputDigest: providerInputDigest,
  ReviewFrontier: cursor(reviewFrontier),
  SemanticVersion: semanticVersion,
})
const acceptedFact = ({
  managerLifeId = life,
  incumbencyId = life,
  todoWriteId = write,
  toolCallId = call,
  preparedFactRef = 'prepared-fact-ref',
  inputDigest = 'provider-input-digest',
  outputDigest = 'output-digest',
  physicalSuccessEvidence = 'LiveAfterSuccess',
  semanticVersion = 'magic-v1',
} = {}) => fact('TodoWriteAccepted', {
  IncumbencyId: incumbencyId,
  TodoWriteId: todoWriteId,
  ToolCallId: toolCallId,
  PreparedFactRef: preparedFactRef,
  InputDigest: inputDigest,
  OutputDigest: outputDigest,
  PhysicalSuccessEvidence: physicalSuccessEvidence,
  SemanticVersion: semanticVersion,
})
const prepared = preparedFact()
const accepted = acceptedFact()
const acceptedState = () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  ok(foldMagic(handle, accepted))
  return handle
}

test('WHAT[obligation-ledger-008] rejects Accepted when it names another Prepared envelope', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  const mismatched = acceptedFact({ preparedFactRef: 'different-prepared-fact-ref' })
  assert.equal(error(foldMagic(handle, mismatched)).code, 'IdentityCorruption')
})
test('WHAT[obligation-ledger-008] rejects a replay whose frozen prepared identity differs', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  const collision = preparedFact({ providerInputDigest: 'different-provider-input-digest' })
  assert.equal(error(foldMagic(handle, collision, 'prepared-fact-ref-2')).code, 'IdentityCorruption')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

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

test('WHAT[obligation-ledger-008] pure replay identity checker detects corruption for the Host fatal boundary', () => {
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
}

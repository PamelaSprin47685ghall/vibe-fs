import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js'
import * as codec from '../../../dist/Mission/Obligation/Todo/MagicTodoProjectionCodecSurface.js'
import * as envelope from '../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

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

test('WHAT[obligation-ledger-019] rejects a legacy seed after the first Magic provider request', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  const legacySeed = fact('LegacyTodoSeedAdopted', {
    ManagerSessionId: managerSession,
    IncumbencyId: life,
    SeedTodoRef: 'legacy-list',
    SeedTodoDigest: 'legacy-digest',
  })
  assert.equal(error(foldMagic(handle, legacySeed)).code, 'LegacySeedAfterCheckpoint')
})

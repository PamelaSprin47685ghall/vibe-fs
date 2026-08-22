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
  ManagerLifeId: managerLifeId,
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
  todoWriteId = write,
  toolCallId = call,
  preparedFactRef = 'prepared-fact-ref',
  inputDigest = 'provider-input-digest',
  outputDigest = 'output-digest',
  physicalSuccessEvidence = 'LiveAfterSuccess',
  semanticVersion = 'magic-v1',
} = {}) => fact('TodoWriteAccepted', {
  ManagerLifeId: managerLifeId,
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

test('WHAT[OBLIGATION-LEDGER-010] Accepted supersedes Current immediately', () => {
  const lifeState = projection.MagicTodoProjectionSurface_view(acceptedState(), life)
  assert.equal(lifeState.currentObligations.reference, 'proposal-list')
  assert.equal(lifeState.currentObligations.digest, 'proposal-digest')
  assert.deepEqual(lifeState.checkpoints[0].lifecycle, {
    kind: 'Accepted',
    inputDigest: 'provider-input-digest',
    outputDigest: 'output-digest',
  })
})

test('WHAT[OBLIGATION-LEDGER-018] checkpoint lifecycle is tagged Prepared and Accepted', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  assert.deepEqual(projection.MagicTodoProjectionSurface_view(handle, life).checkpoints[0].lifecycle, { kind: 'Prepared' })

  ok(foldMagic(handle, accepted))
  assert.equal(projection.MagicTodoProjectionSurface_view(handle, life).checkpoints[0].lifecycle.kind, 'Accepted')
})

test('WHAT[OBLIGATION-LEDGER-008] rejects Accepted when it names another Prepared envelope', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  const mismatched = acceptedFact({ preparedFactRef: 'different-prepared-fact-ref' })
  assert.equal(error(foldMagic(handle, mismatched)).code, 'IdentityCorruption')
})

test('WHAT[OBLIGATION-LEDGER-013] successive checkpoints can be prepared and accepted without review blockage', () => {
  const nextWrite = todo.todoWriteId(sha256, life, 'todo-call-2')
  const handle = acceptedState()
  const nextPrepared = preparedFact({
    todoWriteId: nextWrite,
    toolCallId: 'todo-call-2',
    toolPartOrdinal: 1,
    baseTodoRef: 'proposal-list',
    baseTodoDigest: 'proposal-digest',
    proposedTodoRef: 'next-proposal-list',
    proposedTodoDigest: 'next-proposal-digest',
    planCompleteDeclared: true,
    providerInputDigest: 'next-provider-input-digest',
    reviewFrontier: 12,
  })
  ok(foldMagic(handle, nextPrepared, 'next-prepared'))
  const nextAccepted = acceptedFact({
    todoWriteId: nextWrite,
    toolCallId: 'todo-call-2',
    preparedFactRef: 'next-prepared',
    inputDigest: 'next-provider-input-digest',
    outputDigest: 'next-output-digest',
  })
  ok(foldMagic(handle, nextAccepted))
  const lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.currentObligations.reference, 'next-proposal-list')
  assert.equal(lifeState.checkpoints.length, 2)
})

test('WHAT[OBLIGATION-LEDGER-019] rejects a legacy seed after the first Magic provider request', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  const legacySeed = fact('LegacyTodoSeedAdopted', {
    ManagerSessionId: managerSession,
    ManagerLifeId: life,
    SeedTodoRef: 'legacy-list',
    SeedTodoDigest: 'legacy-digest',
  })
  assert.equal(error(foldMagic(handle, legacySeed)).code, 'LegacySeedAfterCheckpoint')
})

test('WHAT[OBLIGATION-LEDGER-018] stores typed Magic Todo bytes in the canonical Fact envelope', () => {
  const typed = ok(codec.encode(accepted)).value
  const encoded = envelope.serializeMagicTodoEnvelope(typed)
  const decoded = envelope.deserializeMagicTodoEnvelope(encoded)

  assert.equal(decoded.ok, true)
  assert.equal(decoded.case, 'MagicTodo')
  assert.equal(decoded.payload, typed)
})

test('WHAT[OBLIGATION-LEDGER-018] legacy Prepared without planComplete decodes as committed true', () => {
  const legacy = prepared.replace(/,"PlanCompleteDeclared":false/, '')
  const decoded = codec.decode(legacy)
  assert.equal(decoded.ok, true)
  assert.equal(decoded.planCompleteDeclared, true)
})

test('WHAT[OBLIGATION-LEDGER-018] rejects forward Magic Todo payloads without throwing through boot fold', () => {
  const forward = prepared.replace('TodoWritePrepared', 'FutureMagicTodoCase')
  assert.doesNotThrow(() => codec.decode(forward))
  assert.equal(codec.decode(forward).ok, false)
})

test('WHAT[OBLIGATION-LEDGER-018] folds a typed Magic Todo envelope into the one canonical projection', () => {
  const typed = ok(codec.encode(prepared)).value
  const folded = envelope.foldMagicEnvelope(managerSession, 'manager-provider-run', typed)
  assert.equal(folded.ok, true, folded.ok ? '' : folded.error)
  assert.equal(folded.lives.length, 1)
  assert.equal(folded.lives[0].checkpoints, 1)
  assert.equal(folded.lives[0].proposedDigests[0], 'proposal-digest')
})

test('WHAT[OBLIGATION-LEDGER-008] rejects a replay whose frozen prepared identity differs', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  const collision = preparedFact({ providerInputDigest: 'different-provider-input-digest' })
  assert.equal(error(foldMagic(handle, collision, 'prepared-fact-ref-2')).code, 'IdentityCorruption')
})

test('WHAT[OBLIGATION-LEDGER-016] projection latches the first true commitment and never reopens it', () => {
  const makeCheckpoint = (suffix, declared, baseName, proposalName, frontier) => {
    const cpCall = `todo-${suffix}`
    const cpWrite = todo.todoWriteId(sha256, life, cpCall)
    const cpPreparedRef = `prepared-${suffix}`
    const inputDigest = `provider-${suffix}`
    return {
      suffix,
      write: cpWrite,
      frontier,
      prepared: preparedFact({
        todoWriteId: cpWrite,
        toolCallId: cpCall,
        toolPartOrdinal: 1,
        baseTodoRef: baseName,
        baseTodoDigest: `${baseName}-digest`,
        proposedTodoRef: proposalName,
        proposedTodoDigest: `${proposalName}-digest`,
        planCompleteDeclared: declared,
        providerInputDigest: inputDigest,
        reviewFrontier: frontier,
      }),
      accepted: acceptedFact({
        todoWriteId: cpWrite,
        toolCallId: cpCall,
        preparedFactRef: cpPreparedRef,
        inputDigest,
        outputDigest: `output-${suffix}`,
      }),
      preparedRef: cpPreparedRef,
    }
  }

  const planning = makeCheckpoint('planning', false, 'base-0', 'plan-1', 10)
  const commitment = makeCheckpoint('commit', true, 'plan-1', 'mission-1', 20)
  const laterFalse = makeCheckpoint('later-false', false, 'mission-1', 'mission-2', 30)
  const handle = projection.MagicTodoProjectionSurface_create()

  ok(foldMagic(handle, planning.prepared, planning.preparedRef))
  ok(foldMagic(handle, planning.accepted))
  let lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.firstPlanCommitment, null)
  assert.equal(lifeState.latestCommittedCheckpoint, null)
  assert.equal(lifeState.firstAcceptedCheckpoint, planning.write)

  ok(foldMagic(handle, commitment.prepared, commitment.preparedRef))
  ok(foldMagic(handle, commitment.accepted))
  lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.firstPlanCommitment, commitment.write)
  assert.equal(lifeState.latestCommittedCheckpoint, commitment.write)
  assert.equal(lifeState.previousCommittedCheckpoint, null)

  ok(foldMagic(handle, laterFalse.prepared, laterFalse.preparedRef))
  ok(foldMagic(handle, laterFalse.accepted))
  lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.firstPlanCommitment, commitment.write)
  assert.equal(lifeState.previousCommittedCheckpoint, commitment.write)
  assert.equal(lifeState.latestCommittedCheckpoint, laterFalse.write)
})

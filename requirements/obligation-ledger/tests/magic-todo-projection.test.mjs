import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js'
import * as codec from '../../../dist/Mission/Obligation/Todo/MagicTodoProjectionCodecSurface.js'
import * as envelope from '../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

const sha256 = (value) => `digest:${value}`
const life = 'manager-life'
const managerSession = 'manager-session'
const reviewerSession = 'reviewer-session'
const call = 'todo-call'
const write = todo.todoWriteId(sha256, life, call)
const review = todo.todoReviewId(sha256, life, write)
const reviewer = todo.dedicatedReviewerId(sha256, life)
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

const enlistedFact = ({ managerLifeId = life, dedicatedReviewerId = reviewer, reviewerSessionId = reviewerSession } = {}) =>
  fact('DedicatedTodoReviewerEnlisted', {
    ManagerLifeId: managerLifeId,
    DedicatedReviewerId: dedicatedReviewerId,
    ReviewerSessionId: reviewerSessionId,
  })

const assignedFact = ({
  managerLifeId = life,
  todoWriteId = write,
  todoReviewId = review,
  dedicatedReviewerId = reviewer,
  reviewerSessionId = reviewerSession,
  reviewWorkStart = 4,
  managerReviewFrontier = 10,
} = {}) => fact('TodoProcessReviewAssigned', {
  ManagerLifeId: managerLifeId,
  TodoWriteId: todoWriteId,
  TodoReviewId: todoReviewId,
  DedicatedReviewerId: dedicatedReviewerId,
  ReviewerSessionId: reviewerSessionId,
  ReviewWorkStartCursor: cursor(reviewWorkStart),
  ManagerReviewFrontier: cursor(managerReviewFrontier),
})

const concludedFact = ({
  managerLifeId = life,
  todoWriteId = write,
  todoReviewId = review,
  dedicatedReviewerId = reviewer,
  reviewerSessionId = reviewerSession,
  verdict = 'REVISE',
  workRecordRef = 'review-lwr',
  workRecordDigest = 'review-lwr-digest',
  settledTodoRef = 'settled-list',
  settledTodoDigest = 'settled-list-digest',
  reviewerRecordFrontier = 8,
  providerRunId = 'reviewer-provider-run',
  toolCallId = 'reviewer-call',
} = {}) => fact('TodoReviewConcluded', {
  ManagerLifeId: managerLifeId,
  TodoWriteId: todoWriteId,
  TodoReviewId: todoReviewId,
  DedicatedReviewerId: dedicatedReviewerId,
  ReviewerSessionId: reviewerSessionId,
  Verdict: verdict,
  WorkRecordRef: workRecordRef,
  WorkRecordDigest: workRecordDigest,
  SettledTodoRef: settledTodoRef,
  SettledTodoDigest: settledTodoDigest,
  ReviewerRecordFrontier: cursor(reviewerRecordFrontier),
  ProviderRunId: providerRunId,
  ToolCallId: toolCallId,
})

const prepared = preparedFact()
const accepted = acceptedFact()
const enlisted = enlistedFact()
const assigned = assignedFact()
const concluded = concludedFact()

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
})

test('WHAT[OBLIGATION-LEDGER-011] REVISE conclusion cannot roll back CurrentObligations', () => {
  const handle = acceptedState()
  ok(foldMagic(handle, enlisted))
  ok(foldMagic(handle, assigned))
  ok(foldMagic(handle, concluded))

  const lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.currentObligations.reference, 'proposal-list')
  assert.equal(lifeState.currentObligations.digest, 'proposal-digest')
  assert.equal(lifeState.checkpoints[0].concluded.verdict, 'REVISE')
})

test('WHAT[OBLIGATION-LEDGER-012] rejects a conclusion with no matching assignment', () => {
  const handle = acceptedState()
  assert.equal(error(foldMagic(handle, concluded)).code, 'AssignmentWithoutAccepted')
})

test('WHAT[OBLIGATION-LEDGER-020] rejects process assignment before dedicated enlistment', () => {
  const handle = acceptedState()
  assert.equal(error(foldMagic(handle, assigned)).code, 'DedicatedMissingForAssign')
})

test('WHAT[OBLIGATION-LEDGER-008] rejects Accepted when it names another Prepared envelope', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  const mismatched = acceptedFact({ preparedFactRef: 'different-prepared-fact-ref' })
  assert.equal(error(foldMagic(handle, mismatched)).code, 'IdentityCorruption')
})

test('WHAT[OBLIGATION-LEDGER-013] treats an exact durable conclusion replay as idempotent', () => {
  const handle = acceptedState()
  ok(foldMagic(handle, enlisted))
  ok(foldMagic(handle, assigned))
  ok(foldMagic(handle, concluded))
  ok(foldMagic(handle, concluded))
  assert.equal(projection.MagicTodoProjectionSurface_view(handle, life).currentObligations.reference, 'proposal-list')
})

test('WHAT[OBLIGATION-LEDGER-013] rejects a new prepare until the preceding review concludes', () => {
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
  assert.equal(error(foldMagic(handle, nextPrepared, 'next-prepared')).code, 'OutstandingReviewBeforePrepare')
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

test('WHAT[OBLIGATION-LEDGER-014] legacy conclusion locator remains replayable but is not a Current writer', () => {
  const encoded = JSON.parse(concluded)
  assert.equal(encoded.SettledTodoRef, 'settled-list')
  assert.equal(encoded.SettledTodoDigest, 'settled-list-digest')
  const handle = acceptedState()
  ok(foldMagic(handle, enlisted))
  ok(foldMagic(handle, assigned))
  ok(foldMagic(handle, concluded))
  assert.equal(projection.MagicTodoProjectionSurface_view(handle, life).currentObligations.reference, 'proposal-list')
})

test('WHAT[OBLIGATION-LEDGER-018] stores typed Magic Todo bytes in the canonical Fact envelope', () => {
  const typed = ok(codec.encode(concluded)).value
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

test('WHAT[OBLIGATION-LEDGER-018] reviewer reverse locator is maintained on enlist and replacement', () => {
  const replacementSession = 'reviewer-session-replacement'
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, enlisted))
  assert.equal(projection.MagicTodoProjectionSurface_reviewerLife(handle, reviewerSession), life)

  const replaced = fact('DedicatedTodoReviewerReplaced', {
    ManagerLifeId: life,
    DedicatedReviewerId: reviewer,
    OldSessionId: reviewerSession,
    NewSessionId: replacementSession,
    EvidenceRef: 'reviewer-replacement-evidence',
  })
  ok(foldMagic(handle, replaced))
  assert.equal(projection.MagicTodoProjectionSurface_reviewerLife(handle, reviewerSession), null)
  assert.equal(projection.MagicTodoProjectionSurface_reviewerLife(handle, replacementSession), life)
})

test('WHAT[OBLIGATION-LEDGER-020] concluded manager review frontier advances only when the dedicated reviewer concludes', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  const firstCall = 'todo-review-range-first'
  const firstWrite = todo.todoWriteId(sha256, life, firstCall)
  const firstReview = todo.todoReviewId(sha256, life, firstWrite)
  const secondCall = 'todo-review-range-second'
  const secondWrite = todo.todoWriteId(sha256, life, secondCall)
  const secondReview = todo.todoReviewId(sha256, life, secondWrite)

  ok(foldMagic(handle, preparedFact({ todoWriteId: firstWrite, toolCallId: firstCall, reviewFrontier: 10 }), 'prepared-review-range-first'))
  ok(foldMagic(handle, acceptedFact({ todoWriteId: firstWrite, toolCallId: firstCall, preparedFactRef: 'prepared-review-range-first' })))
  ok(foldMagic(handle, enlisted))
  ok(foldMagic(handle, assignedFact({ todoWriteId: firstWrite, todoReviewId: firstReview, managerReviewFrontier: 10 })))
  ok(foldMagic(handle, concludedFact({ todoWriteId: firstWrite, todoReviewId: firstReview })))
  assert.equal(projection.MagicTodoProjectionSurface_view(handle, life).latestConcludedManagerReviewFrontier, 10)

  ok(foldMagic(handle, preparedFact({
    todoWriteId: secondWrite,
    toolCallId: secondCall,
    baseTodoRef: 'proposal-list',
    baseTodoDigest: 'proposal-digest',
    proposedTodoRef: 'proposal-list-second',
    proposedTodoDigest: 'proposal-digest-second',
    reviewFrontier: 20,
  }), 'prepared-review-range-second'))
  ok(foldMagic(handle, acceptedFact({
    todoWriteId: secondWrite,
    toolCallId: secondCall,
    preparedFactRef: 'prepared-review-range-second',
  })))
  assert.equal(
    projection.MagicTodoProjectionSurface_view(handle, life).latestConcludedManagerReviewFrontier,
    10,
    'the current Accepted checkpoint is not reviewer coverage until its own review concludes',
  )

  ok(foldMagic(handle, assignedFact({ todoWriteId: secondWrite, todoReviewId: secondReview, managerReviewFrontier: 20 })))
  ok(foldMagic(handle, concludedFact({ todoWriteId: secondWrite, todoReviewId: secondReview })))
  assert.equal(projection.MagicTodoProjectionSurface_view(handle, life).latestConcludedManagerReviewFrontier, 20)
})

test('WHAT[OBLIGATION-LEDGER-020] concluded manager coverage advances to the exact assigned frontier rather than the provisional prepared frontier', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  const exactCall = 'todo-review-exact-frontier'
  const exactWrite = todo.todoWriteId(sha256, life, exactCall)
  const exactReview = todo.todoReviewId(sha256, life, exactWrite)

  ok(foldMagic(handle, preparedFact({
    todoWriteId: exactWrite,
    toolCallId: exactCall,
    reviewFrontier: 18,
  }), 'prepared-review-exact-frontier'))
  ok(foldMagic(handle, acceptedFact({
    todoWriteId: exactWrite,
    toolCallId: exactCall,
    preparedFactRef: 'prepared-review-exact-frontier',
  })))
  ok(foldMagic(handle, enlisted))
  ok(foldMagic(handle, assignedFact({
    todoWriteId: exactWrite,
    todoReviewId: exactReview,
    managerReviewFrontier: 19,
  })))
  ok(foldMagic(handle, concludedFact({ todoWriteId: exactWrite, todoReviewId: exactReview })))

  assert.equal(
    projection.MagicTodoProjectionSurface_view(handle, life).latestConcludedManagerReviewFrontier,
    19,
    'reviewer knowledge advances to the range actually assigned, not the before-hook estimate',
  )
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

  const closeReview = (handle, cp) => {
    const reviewId = todo.todoReviewId(sha256, life, cp.write)
    const assignment = assignedFact({
      todoWriteId: cp.write,
      todoReviewId: reviewId,
      reviewWorkStart: cp.frontier + 1,
      managerReviewFrontier: cp.frontier,
    })
    const conclusion = concludedFact({
      todoWriteId: cp.write,
      todoReviewId: reviewId,
      verdict: 'PERFECT',
      workRecordRef: `review-${cp.suffix}`,
      workRecordDigest: `review-${cp.suffix}-digest`,
      settledTodoRef: `settled-${cp.suffix}`,
      settledTodoDigest: `settled-${cp.suffix}-digest`,
      reviewerRecordFrontier: cp.frontier + 2,
      providerRunId: `review-run-${cp.suffix}`,
      toolCallId: `review-call-${cp.suffix}`,
    })
    ok(foldMagic(handle, assignment))
    ok(foldMagic(handle, conclusion))
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
  assert.equal(lifeState.pendingReviewCheckpoint, planning.write)

  ok(foldMagic(handle, enlisted))
  closeReview(handle, planning)
  lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.pendingReviewCheckpoint, null)
  assert.equal(lifeState.latestConcludedManagerReviewFrontier, planning.frontier)

  ok(foldMagic(handle, commitment.prepared, commitment.preparedRef))
  ok(foldMagic(handle, commitment.accepted))
  lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.firstPlanCommitment, commitment.write)
  assert.equal(lifeState.latestCommittedCheckpoint, commitment.write)
  assert.equal(lifeState.previousCommittedCheckpoint, null)
  assert.equal(
    lifeState.latestConcludedManagerReviewFrontier,
    planning.frontier,
    'accepting the next checkpoint does not pretend its review range is already known by the reviewer',
  )

  closeReview(handle, commitment)
  lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.latestConcludedManagerReviewFrontier, commitment.frontier)
  ok(foldMagic(handle, laterFalse.prepared, laterFalse.preparedRef))
  ok(foldMagic(handle, laterFalse.accepted))
  lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.firstPlanCommitment, commitment.write)
  assert.equal(lifeState.previousCommittedCheckpoint, commitment.write)
  assert.equal(lifeState.latestCommittedCheckpoint, laterFalse.write)
  assert.equal(lifeState.latestConcludedManagerReviewFrontier, commitment.frontier)
})

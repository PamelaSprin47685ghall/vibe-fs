import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
  caseOf,
  envelope,
  eventId,
  fold,
  magicTodo,
  magicTodoFactEnvelope,
  magicTodoJournal,
  managerLifeId,
  mapEntries,
  providerRun,
  sessionId,
  stream,
  toList,
  toolCallId,
} from '../support/domain.mjs'

const FactCodec = await import('../../../dist/Journal/FactCodec.js')

const sha256 = (value) => `digest:${value}`
const life = managerLifeId('manager-life')
const managerSession = sessionId('manager-session')
const reviewerSession = sessionId('reviewer-session')
const call = toolCallId('todo-call')
const cursor = (sequence) => new magicTodoJournal.XTraceCursor(BigInt(sequence))
const ok = (result) => {
  assert.equal(result.tag, 0, `expected Ok, got ${JSON.stringify(result.fields?.[0])}`)
  return result.fields[0]
}
const error = (result) => {
  assert.equal(result.tag, 1, 'expected Error')
  return result.fields[0]
}

const write = magicTodo.todoWriteId(sha256, life, call)
const review = magicTodo.todoReviewId(sha256, life, write)
const reviewer = magicTodo.dedicatedReviewerId(sha256, life)
const preparedFactRef = eventId('prepared-fact-ref')
const prepared = new magicTodoJournal.TodoWritePrepared(
  managerSession,
  life,
  write,
  call,
  2,
  blobRef('base-list'),
  blobDigest('base-digest'),
  blobRef('proposal-list'),
  blobDigest('proposal-digest'),
  'provider-input-digest',
  cursor(10),
  'magic-v1',
)
const accepted = new magicTodoJournal.TodoWriteAccepted(
  life,
  write,
  call,
  preparedFactRef,
  'provider-input-digest',
  'output-digest',
  magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
  'magic-v1',
)
const enlisted = new magicTodoJournal.DedicatedTodoReviewerEnlisted(life, reviewer, reviewerSession)
const assigned = new magicTodoJournal.TodoProcessReviewAssigned(
  life,
  write,
  review,
  reviewer,
  reviewerSession,
  cursor(4),
  cursor(10),
)
const concluded = new magicTodoJournal.TodoReviewConcluded(
  life,
  write,
  review,
  reviewer,
  reviewerSession,
  magicTodo.revise,
  blobRef('review-lwr'),
  blobDigest('review-lwr-digest'),
  blobRef('settled-list'),
  blobDigest('settled-list-digest'),
  cursor(8),
  providerRun('reviewer-provider-run'),
  toolCallId('reviewer-call'),
)
const fact = (caseName, payload) => magicTodoJournal.MagicTodoFact(caseName, [payload])
let nextEnvelope = 0
const foldMagic = (state, magicFact, envelopeEventId = undefined) => {
  const ref = envelopeEventId ?? (caseOf(magicFact) === 'TodoWritePrepared' ? preparedFactRef : eventId(`magic-todo-${nextEnvelope++}`))
  return magicTodoJournal.fold(ref, state, magicFact)
}

test('TODO-006 concludes only after matching durable assignment and stores settled locator', () => {
  let state = magicTodoJournal.empty
  state = ok(foldMagic(state, fact('TodoWritePrepared', prepared)))
  state = ok(foldMagic(state, fact('TodoWriteAccepted', accepted)))
  state = ok(foldMagic(state, fact('DedicatedTodoReviewerEnlisted', enlisted)))
  state = ok(foldMagic(state, fact('TodoProcessReviewAssigned', assigned)))
  state = ok(foldMagic(state, fact('TodoReviewConcluded', concluded)))

  const lifeState = state.ByLife.get('manager-life')
  assert.equal(lifeState.SettledCurrentRef[0].fields[0], 'settled-list')
  assert.equal(lifeState.SettledCurrentRef[1].fields[0], 'settled-list-digest')
  assert.equal(lifeState.Checkpoints.get(magicTodo.todoWriteIdValue(write)).Concluded.Verdict, magicTodo.revise)
})

test('TODO-006 rejects a conclusion with no matching assignment', () => {
  let state = magicTodoJournal.empty
  state = ok(foldMagic(state, fact('TodoWritePrepared', prepared)))
  state = ok(foldMagic(state, fact('TodoWriteAccepted', accepted)))

  const rejected = error(foldMagic(state, fact('TodoReviewConcluded', concluded)))
  assert.equal(rejected.cases()[rejected.tag], 'AssignmentWithoutAccepted')
})

test('TODO-008 rejects process assignment before dedicated enlistment', () => {
  let state = magicTodoJournal.empty
  state = ok(foldMagic(state, fact('TodoWritePrepared', prepared)))
  state = ok(foldMagic(state, fact('TodoWriteAccepted', accepted)))

  const rejected = error(foldMagic(state, fact('TodoProcessReviewAssigned', assigned)))
  assert.equal(rejected.cases()[rejected.tag], 'DedicatedMissingForAssign')
})

test('TODO-004 rejects Accepted when it names another Prepared envelope', () => {
  const mismatched = new magicTodoJournal.TodoWriteAccepted(
    life,
    write,
    call,
    eventId('different-prepared-fact-ref'),
    'provider-input-digest',
    'output-digest',
    magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
    'magic-v1',
  )
  const state = ok(foldMagic(magicTodoJournal.empty, fact('TodoWritePrepared', prepared)))
  const rejected = error(foldMagic(state, fact('TodoWriteAccepted', mismatched)))
  assert.equal(rejected.cases()[rejected.tag], 'IdentityCorruption')
})

test('TODO-006 treats an exact durable conclusion replay as idempotent', () => {
  let state = magicTodoJournal.empty
  state = ok(foldMagic(state, fact('TodoWritePrepared', prepared)))
  state = ok(foldMagic(state, fact('TodoWriteAccepted', accepted)))
  state = ok(foldMagic(state, fact('DedicatedTodoReviewerEnlisted', enlisted)))
  state = ok(foldMagic(state, fact('TodoProcessReviewAssigned', assigned)))
  state = ok(foldMagic(state, fact('TodoReviewConcluded', concluded)))

  const replayed = ok(foldMagic(state, fact('TodoReviewConcluded', concluded)))
  assert.equal(replayed.ByLife.get('manager-life').SettledCurrentRef[0].fields[0], 'settled-list')
})

test('TODO-006 rejects a new prepare until the preceding review concludes', () => {
  const nextCall = toolCallId('todo-call-2')
  const nextWrite = magicTodo.todoWriteId(sha256, life, nextCall)
  const nextPrepared = new magicTodoJournal.TodoWritePrepared(
    managerSession,
    life,
    nextWrite,
    nextCall,
    1,
    blobRef('settled-list'),
    blobDigest('settled-list-digest'),
    blobRef('next-proposal-list'),
    blobDigest('next-proposal-digest'),
    'next-provider-input-digest',
    cursor(12),
    'magic-v1',
  )

  let state = ok(foldMagic(magicTodoJournal.empty, fact('TodoWritePrepared', prepared)))
  state = ok(foldMagic(state, fact('TodoWriteAccepted', accepted)))

  const rejected = error(foldMagic(state, fact('TodoWritePrepared', nextPrepared)))
  assert.equal(rejected.cases()[rejected.tag], 'OutstandingReviewBeforePrepare')
})

test('TODO-011 rejects a legacy seed after the first Magic provider request', () => {
  const legacySeed = new magicTodoJournal.LegacyTodoSeedAdopted(
    managerSession,
    life,
    blobRef('legacy-list'),
    blobDigest('legacy-digest'),
    toList([magicTodo.todoItemIdCreate('legacy-item')]),
  )
  const state = ok(foldMagic(magicTodoJournal.empty, fact('TodoWritePrepared', prepared)))
  const rejected = error(foldMagic(state, fact('LegacyTodoSeedAdopted', legacySeed)))
  assert.equal(rejected.cases()[rejected.tag], 'LegacySeedAfterCheckpoint')
})

test('TODO-012 codec preserves settled locator for deterministic replay', () => {
  const encoded = magicTodoJournal.encode(fact('TodoReviewConcluded', concluded))
  const decoded = ok(magicTodoJournal.tryDecode(encoded))
  const payload = decoded.fields[0]

  assert.equal(payload.SettledTodoRef.fields[0], 'settled-list')
  assert.equal(payload.SettledTodoDigest.fields[0], 'settled-list-digest')
})

test('TODO-012 stores typed Magic Todo bytes in the canonical Fact envelope', () => {
  const typed = magicTodoJournal.encode(fact('TodoReviewConcluded', concluded))
  const encoded = FactCodec.serializeFact(magicTodoFactEnvelope(typed))
  const decoded = ok(FactCodec.deserializeFact(encoded))

  assert.equal(decoded.cases()[decoded.tag], 'MagicTodo')
  assert.equal(decoded.fields[0], typed)
  const replayed = ok(magicTodoJournal.tryDecode(decoded.fields[0]))
  assert.equal(replayed.cases()[replayed.tag], 'TodoReviewConcluded')
})

test('TODO-012 rejects forward Magic Todo payloads without throwing through boot fold', () => {
  const encoded = magicTodoJournal.encode(fact('TodoWritePrepared', prepared))
  const forward = encoded.replace('TodoWritePrepared', 'FutureMagicTodoCase')

  assert.doesNotThrow(() => magicTodoJournal.tryDecode(forward))
  assert.equal(magicTodoJournal.tryDecode(forward).tag, 1)
})

test('TODO-012 folds a typed Magic Todo envelope into the one canonical projection', () => {
  const typed = magicTodoJournal.encode(fact('TodoWritePrepared', prepared))
  const folded = fold.one(
    fold.empty,
    envelope({
      stream: stream.session(managerSession),
      run: 'manager-provider-run',
      fact: magicTodoFactEnvelope(typed),
    }),
  )
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const lives = mapEntries(folded.value.AgentProjections.MagicTodo.ByLife)
  assert.equal(lives.length, 1)
  const checkpoints = mapEntries(lives[0][1].Checkpoints)
  assert.equal(checkpoints.length, 1)
  assert.equal(checkpoints[0][1].ProposedTodoDigest.fields[0], 'proposal-digest')
})

test('TODO-004 rejects a replay whose frozen prepared identity differs', () => {
  const collision = new magicTodoJournal.TodoWritePrepared(
    managerSession,
    life,
    write,
    call,
    2,
    blobRef('base-list'),
    blobDigest('base-digest'),
    blobRef('proposal-list'),
    blobDigest('proposal-digest'),
    'different-provider-input-digest',
    cursor(10),
    'magic-v1',
  )

  let state = ok(foldMagic(magicTodoJournal.empty, fact('TodoWritePrepared', prepared)))
  const rejected = error(foldMagic(state, fact('TodoWritePrepared', collision)))
  assert.equal(rejected.cases()[rejected.tag], 'IdentityCorruption')
})

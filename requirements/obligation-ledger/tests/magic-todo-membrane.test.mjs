import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as todoJournal from '../../../dist/Persistence/Journal/ObligationJournalSurface.js'
import * as host from '../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js'
import * as membrane from '../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js'
import * as locality from '../../../dist/Mission/Obligation/Todo/MagicTodoLocalitySurface.js'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')

const withJournal = async (body, runtime = 'rt_magic_todo_membrane') => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-obligation-membrane-'))
  const boot = await journal.JournalSurface_boot(directory, runtime, 4242, '2026-08-11T00:00:00Z')
  assert.equal(boot.ok, true, boot.ok ? '' : boot.error)
  try {
    return await body(boot.journal)
  } finally {
    journal.JournalSurface_dispose(boot.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}

const openLife = async (handle, session, life) => {
  const result = await membrane.MagicTodoMembraneSurface_openLife(handle, session, life)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
}

const prepare = (handle, session, call, obligations, planComplete = true, state = 0) => {
  const args = { planComplete, workingOn: obligations[0]?.name ?? '', obligations }
  const canonical = host.canonicalInput(args)
  const digest = host.canonicalInputDigest(sha256Hex, args)
  return membrane.MagicTodoMembraneSurface_prepare(handle, session, call, canonical, digest, planComplete, obligations, state)
    .then((result) => ({ result, digest, args, canonical }))
}

const accept = async (handle, prepared, inputDigest, outputDigest) =>
  membrane.MagicTodoMembraneSurface_accept(handle, prepared, 'LiveAfterSuccess', inputDigest, outputDigest)

const assertOk = (result, message = '') => {
  assert.equal(result.ok, true, message || (result.ok ? '' : JSON.stringify(result.error)))
  return result.value
}

const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })

const append = async (handle, session, caseName, payload) => {
  const result = await todoJournal.appendMagicTodo(handle, session, null, fact(caseName, payload))
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result
}

test('WHAT[OBLIGATION-LEDGER-025] accept rejects unknown physical success evidence', async () => {
  await withJournal(async (handle) => {
    const result = await membrane.MagicTodoMembraneSurface_accept(handle, null, 'UNKNOWN', 'input', 'output')
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'InvalidPhysicalEvidence')
  })
})

test('WHAT[OBLIGATION-LEDGER-025] openLife and compatibility injection do not wait for snapshot IO', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-before-latency'
    const life = 'life-before-latency'
    // openLife is a journal append — completes without any snapshot port
    await openLife(handle, session, life)
    // V1 compatibility injection is synchronous — no snapshot dependency
    const obligations = [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }]
    const rows = host.projectCompatibilityRows('diagnose', obligations)
    const output = { args: { planComplete: false, workingOn: 'diagnose', obligations } }
    host.replaceCompatibilityArgs(output, rows)
    assert.equal('obligations' in output.args, true)
    assert.equal(Object.prototype.propertyIsEnumerable.call(output.args, 'todos'), false)
    assert.equal(output.args.todos[0].content, 'diagnose: Fix the todowrite snapshot race.')
    assert.equal(output.args.todos[0].status, 'in_progress')
  })
})

test('WHAT[OBLIGATION-LEDGER-002] non-matching workingOn does not fail the membrane — all obligations are projected', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-workingon-repair'
    const life = 'life-workingon-repair'
    await openLife(handle, session, life)
    const obligations = [
      { name: 'synthesize-evidence-road', work: 'Synthesize the evidence.' },
      { name: 'ship', work: 'Ship the result.' },
    ]
    // V1 compatibility rows project all obligations regardless of workingOn match
    const rows = host.projectCompatibilityRows('synthesize-evidence-into-road', obligations)
    assert.deepEqual(rows, [
      { content: 'synthesize-evidence-road: Synthesize the evidence.', status: 'pending', priority: 'medium' },
      { content: 'ship: Ship the result.', status: 'pending', priority: 'medium' },
    ])
    // prepare succeeds with valid obligations even when workingOn doesn't match
    const args = { planComplete: false, workingOn: 'synthesize-evidence-into-road', obligations }
    const canonical = host.canonicalInput(args)
    const digest = host.canonicalInputDigest(sha256Hex, args)
    const result = await membrane.MagicTodoMembraneSurface_prepare(handle, session, 'call-workingon-repair', canonical, digest, false, obligations, 0)
    assert.equal(result.ok, true, 'prepare must not fail for non-matching workingOn')
  })
})

test('WHAT[OBLIGATION-LEDGER-009] duplicate obligation name is the provider-red class', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-syntax-red'
    const life = 'life-syntax-red'
    await openLife(handle, session, life)
    const result = await prepare(handle, session, 'call-syntax-red', [
      { name: 'same', work: 'first' },
      { name: 'same', work: 'second' },
    ], false)
    assert.equal(result.result.ok, false)
    assert.equal(result.result.error.code, 'DuplicateObligationName')
  })
})

test('WHAT[OBLIGATION-LEDGER-009] prepare succeeds without review runtime; accept flags review as required downstream', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-runtime-fatal'
    const life = 'life-runtime-fatal'
    await openLife(handle, session, life)
    // prepare+accept succeed without any review runtime — review is a downstream step, not a precondition
    const t1 = await prepare(handle, session, 'call-runtime-fatal', [
      { name: 'work', work: 'Do real mission work.' },
    ], true)
    const prepared = assertOk(t1.result)
    const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('runtime-fatal-output'))
    assert.equal(accepted.ok, true, 'prepare+accept must succeed without a review runtime')
    assert.equal(accepted.value.needsEnsureReview, true, 'accept must flag review as required downstream')
  })
})

test('WHAT[OBLIGATION-LEDGER-025] prepare rejects a pending ToolPart whose provider input is still empty', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-pending-input'
    await openLife(handle, session, 'life-magic-todo-pending-input')
    const result = await membrane.MagicTodoMembraneSurface_prepare(
      handle,
      session,
      'call-magic-todo-pending-input',
      '{}',
      'provider-input-digest',
      false,
      [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }],
      0,
    )
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'SnapshotInputMismatch')
  })
})

test('WHAT[OBLIGATION-LEDGER-025] before materializes the exact provider input including planComplete and workingOn', () => {
  const expected = host.canonicalInput({ planComplete: false, workingOn: 'diagnose', obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }] })
  const result = locality.materializeInput('call-magic-todo-await-input', '{}', 0, expected)
  assert.equal(result.ok, true)
  assert.equal(result.value.inputCanonical, expected)
})

test('WHAT[OBLIGATION-LEDGER-025] materialization fails closed when the provider input differs', () => {
  const actual = host.canonicalInput({ planComplete: false, workingOn: 'other', obligations: [{ name: 'other', work: 'Different provider input.' }] })
  const expected = host.canonicalInput({ planComplete: false, workingOn: 'diagnose', obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }] })
  const result = locality.materializeInput('call-magic-todo-await-conflict', actual, 1, expected)
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'InputMismatch')
})

test('WHAT[OBLIGATION-LEDGER-025] materialized snapshot input must still match tool.execute.before args', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-conflicting-input'
    await openLife(handle, session, 'life-magic-todo-conflicting-input')
    const input = host.canonicalInput({ planComplete: false, workingOn: 'other', obligations: [{ name: 'other', work: 'Different provider input.' }] })
    const submitted = [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }]
    const result = await membrane.MagicTodoMembraneSurface_prepare(handle, session, 'call-magic-todo-conflicting-input', input, 'provider-input-digest', false, submitted, 0)
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'SnapshotInputMismatch')
  })
})

const acceptPlanningFalseCheckpoint = async (handle, session, life, callText) => {
  const planning = await prepare(handle, session, callText, [
    { name: 'inspect-startup', work: 'Inspect startup paths so the implementation plan can be completed.' },
  ], false)
  const prepared = assertOk(planning.result)
  const accepted = await accept(handle, prepared.bridge, planning.digest, sha256Hex('planning-false-physical-output'))
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  return { planning, accepted }
}

test('WHAT[OBLIGATION-LEDGER-016] first accepted planComplete=false stays at the Planning Table without commitment', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-planning-false'
    const life = 'life-magic-todo-planning-false'
    await openLife(handle, session, life)
    await acceptPlanningFalseCheckpoint(handle, session, life, 'call-magic-todo-planning-false')
    assert.equal(membrane.MagicTodoMembraneSurface_snapshot(handle, life).firstPlanCommitment, null)
  })
})

test('WHAT[OBLIGATION-LEDGER-026] accepted planComplete=false carries no T1 entrustment revelation (revelation is reserved for the first accepted true)', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-planning-false'
    const life = 'life-magic-todo-planning-false'
    await openLife(handle, session, life)
    const { accepted } = await acceptPlanningFalseCheckpoint(handle, session, life, 'call-magic-todo-planning-false')
    assert.doesNotMatch(accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i)
  })
})

test('WHAT[OBLIGATION-LEDGER-017] zero-work planComplete=true with empty obligations is a valid T1 commitment', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-zero-work'
    const life = 'life-magic-todo-zero-work'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-magic-todo-zero-work', [], true)
    const prepared = assertOk(t1.result)
    const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('zero-work-t1-physical-output'))
    assert.equal(accepted.ok, true)
    assert.ok(membrane.MagicTodoMembraneSurface_snapshot(handle, life).firstPlanCommitment)
    assert.match(accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i)
  })
})

const acceptT1Checkpoint = async (handle, session, callText) => {
  const t1 = await prepare(handle, session, callText, [{ name: 'diagnose', work: 'Establish why the first todowrite succeeds.' }])
  const prepared = assertOk(t1.result)
  const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('t1-physical-output'))
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  return { t1, accepted }
}

const concludePerfectReview = async (handle, session, life, callText, t1) => {
  const write = todo.todoWriteId(sha256Hex, life, callText)
  const review = todo.todoReviewId(sha256Hex, life, write)
  const reviewer = todo.dedicatedReviewerId(sha256Hex, life)
  const reviewerSession = `ses-todo-reviewer-${callText}`
  const reviewRecord = await todoJournal.writePayload(handle, 'R1 found no material issue.')
  assert.equal(reviewRecord.ok, true, reviewRecord.ok ? '' : reviewRecord.error)
  await append(handle, session, 'DedicatedTodoReviewerEnlisted', {
    ManagerLifeId: life,
    DedicatedReviewerId: reviewer,
    ReviewerSessionId: reviewerSession,
  })
  await append(handle, session, 'TodoProcessReviewAssigned', {
    ManagerLifeId: life,
    TodoWriteId: write,
    TodoReviewId: review,
    DedicatedReviewerId: reviewer,
    ReviewerSessionId: reviewerSession,
    ReviewWorkStartCursor: { Sequence: 8 },
    ManagerReviewFrontier: { Sequence: 7 },
  })
  await append(handle, session, 'TodoReviewConcluded', {
    ManagerLifeId: life,
    TodoWriteId: write,
    TodoReviewId: review,
    DedicatedReviewerId: reviewer,
    ReviewerSessionId: reviewerSession,
    Verdict: 'PERFECT',
    WorkRecordRef: reviewRecord.blobRef,
    WorkRecordDigest: reviewRecord.blobDigest,
    SettledTodoRef: t1.result.value.prepared.proposedTodoRef,
    SettledTodoDigest: t1.result.value.prepared.proposedTodoDigest,
    ReviewerRecordFrontier: { Sequence: 10 },
    ProviderRunId: 'reviewer-provider-run',
    ToolCallId: 'reviewer-judge-call',
  })
}

test('WHAT[OBLIGATION-LEDGER-012] T1 accept derives the process-review duties (SSOT = TodoWriteAccepted)', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-lag1'
    const life = 'life-magic-todo-t1-t2-lag1'
    await openLife(handle, session, life)
    const { accepted } = await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    assert.equal(accepted.value.needsEnsureReview, true)
    assert.equal(accepted.value.needsDedicatedEnlist, true)
    const snapshot = membrane.MagicTodoMembraneSurface_snapshot(handle, life)
    assert.equal(snapshot.checkpoints.length, 1)
    assert.equal(snapshot.checkpoints[0].accepted, true)
    assert.equal(snapshot.checkpoints[0].assignment, null)
    assert.equal(snapshot.checkpoints[0].concluded, null)
  })
})

test('WHAT[OBLIGATION-LEDGER-010] T1 accept makes the proposed account Current immediately, before any review', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-lag1'
    const life = 'life-magic-todo-t1-t2-lag1'
    await openLife(handle, session, life)
    const { t1 } = await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    const snapshot = membrane.MagicTodoMembraneSurface_snapshot(handle, life)
    assert.equal(snapshot.currentObligations.reference, t1.result.value.prepared.proposedTodoRef)
    assert.equal(snapshot.currentObligations.digest, t1.result.value.prepared.proposedTodoDigest)
  })
})

test('WHAT[OBLIGATION-LEDGER-026] first accepted planComplete=true reveals entrustment in the enriched result', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-lag1'
    const life = 'life-magic-todo-t1-t2-lag1'
    await openLife(handle, session, life)
    const { accepted } = await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    assert.match(accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i)
  })
})

test('WHAT[OBLIGATION-LEDGER-013] T2 prepare while R1 is outstanding is a legal lag-1 wait, not a fail-closed Admission', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-lag1'
    const life = 'life-magic-todo-t1-t2-lag1'
    await openLife(handle, session, life)
    await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    const t2 = await prepare(handle, session, 'call-magic-todo-t2', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    assert.equal(t2.result.ok, false)
    assert.equal(t2.result.error.code, 'AwaitingConsumableReview')
  })
})

test('WHAT[OBLIGATION-LEDGER-014] T2 prepare is gated on a consumable TodoReviewConcluded, not on a mere verdict', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-resolve'
    const life = 'life-magic-todo-t1-t2-resolve'
    await openLife(handle, session, life)
    const { t1 } = await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    const t2Early = await prepare(handle, session, 'call-magic-todo-t2', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    assert.equal(t2Early.result.ok, false)
    assert.equal(t2Early.result.error.code, 'AwaitingConsumableReview')
    await concludePerfectReview(handle, session, life, 'call-magic-todo-t1', t1)
    const t2 = await prepare(handle, session, 'call-magic-todo-t2', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    const t2Prepared = assertOk(t2.result)
    const t2Accepted = await accept(handle, t2Prepared.bridge, t2.digest, sha256Hex('t2-physical-output'))
    assert.equal(t2Accepted.ok, true, t2Accepted.ok ? '' : JSON.stringify(t2Accepted.error))
  })
})

test('WHAT[OBLIGATION-LEDGER-026] enriched result after a concluded PERFECT review is silent about the previous review', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-resolve'
    const life = 'life-magic-todo-t1-t2-resolve'
    await openLife(handle, session, life)
    const { t1 } = await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    await concludePerfectReview(handle, session, life, 'call-magic-todo-t1', t1)
    const t2 = await prepare(handle, session, 'call-magic-todo-t2', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    const t2Prepared = assertOk(t2.result)
    const t2Accepted = await accept(handle, t2Prepared.bridge, t2.digest, sha256Hex('t2-physical-output'))
    assert.equal(t2Accepted.ok, true)
    assert.match(t2Accepted.value.enrichedResult, /Keep working/)
    assert.doesNotMatch(t2Accepted.value.enrichedResult, /Previous checkpoint review|R1 found no material issue/)
  })
})

test('WHAT[OBLIGATION-LEDGER-010] T2 accepted account supersedes CurrentObligations', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-resolve'
    const life = 'life-magic-todo-t1-t2-resolve'
    await openLife(handle, session, life)
    const { t1 } = await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    await concludePerfectReview(handle, session, life, 'call-magic-todo-t1', t1)
    const t2 = await prepare(handle, session, 'call-magic-todo-t2', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    const t2Prepared = assertOk(t2.result)
    const t2Accepted = await accept(handle, t2Prepared.bridge, t2.digest, sha256Hex('t2-physical-output'))
    assert.equal(t2Accepted.ok, true)
    assert.equal(membrane.MagicTodoMembraneSurface_snapshot(handle, life).currentObligations.reference, t2Prepared.prepared.proposedTodoRef)
  })
})

test('WHAT[OBLIGATION-LEDGER-011] REVISE is feedback only: next checkpoint sees the report and Current never rolls back', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-revise-feedback'
    const life = 'life-magic-todo-revise-feedback'
    const callText = 'call-revise-t1'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, callText, [{ name: 'implementation', work: 'Implement the requested behavior.' }])
    const t1Prepared = assertOk(t1.result)
    const t1Accepted = await accept(handle, t1Prepared.bridge, t1.digest, sha256Hex('revise-t1-output'))
    assert.equal(t1Accepted.ok, true)
    const write = todo.todoWriteId(sha256Hex, life, callText)
    const review = todo.todoReviewId(sha256Hex, life, write)
    const reviewer = todo.dedicatedReviewerId(sha256Hex, life)
    const reviewerSession = 'ses-revise-reviewer'
    const reviewText = 'The account omitted the required runtime verification.'
    const reviewRecord = await todoJournal.writePayload(handle, reviewText)
    assert.equal(reviewRecord.ok, true, reviewRecord.ok ? '' : reviewRecord.error)
    await append(handle, session, 'DedicatedTodoReviewerEnlisted', {
      ManagerLifeId: life,
      DedicatedReviewerId: reviewer,
      ReviewerSessionId: reviewerSession,
    })
    await append(handle, session, 'TodoProcessReviewAssigned', {
      ManagerLifeId: life,
      TodoWriteId: write,
      TodoReviewId: review,
      DedicatedReviewerId: reviewer,
      ReviewerSessionId: reviewerSession,
      ReviewWorkStartCursor: { Sequence: 8 },
      ManagerReviewFrontier: { Sequence: 7 },
    })
    await append(handle, session, 'TodoReviewConcluded', {
      ManagerLifeId: life,
      TodoWriteId: write,
      TodoReviewId: review,
      DedicatedReviewerId: reviewer,
      ReviewerSessionId: reviewerSession,
      Verdict: 'REVISE',
      WorkRecordRef: reviewRecord.blobRef,
      WorkRecordDigest: reviewRecord.blobDigest,
      SettledTodoRef: t1Prepared.prepared.baseTodoRef,
      SettledTodoDigest: t1Prepared.prepared.baseTodoDigest,
      ReviewerRecordFrontier: { Sequence: 10 },
      ProviderRunId: 'revise-review-provider-run',
      ToolCallId: 'revise-review-judge',
    })
    assert.equal(membrane.MagicTodoMembraneSurface_snapshot(handle, life).currentObligations.reference, t1Prepared.prepared.proposedTodoRef)
    const t2 = await prepare(handle, session, 'call-revise-t2', [
      { name: 'implementation', work: 'Implement the requested behavior.' },
      { name: 'verification', work: 'Run the required runtime verification and preserve evidence.' },
    ])
    const t2Prepared = assertOk(t2.result)
    const t2Accepted = await accept(handle, t2Prepared.bridge, t2.digest, sha256Hex('revise-t2-output'))
    assert.equal(t2Accepted.ok, true)
    assert.match(t2Accepted.value.enrichedResult, /An earlier account of the work left something unresolved/)
    assert.match(t2Accepted.value.enrichedResult, /omitted the required runtime verification/)
    assert.match(t2Accepted.value.enrichedResult, /Keep working/)
    assert.doesNotMatch(t2Accepted.value.enrichedResult, /settled|preview/i)
    assert.equal(membrane.MagicTodoMembraneSurface_snapshot(handle, life).currentObligations.reference, t2Prepared.prepared.proposedTodoRef)
  })
})

test('WHAT[OBLIGATION-LEDGER-026] prepare without open life is a structured rejection, never a provider red path', async () => {
  await withJournal(async (handle) => {
    // prepare without openLife → NoOpenManagerLife (infrastructure-level, not provider red)
    const obligations = [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }]
    const args = { planComplete: false, workingOn: 'diagnose', obligations }
    const canonical = host.canonicalInput(args)
    const digest = host.canonicalInputDigest(sha256Hex, args)
    const result = await membrane.MagicTodoMembraneSurface_prepare(handle, 'ses-after-failclose', 'call-after-failclose', canonical, digest, false, obligations, 0)
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'NoOpenManagerLife')
  })
})

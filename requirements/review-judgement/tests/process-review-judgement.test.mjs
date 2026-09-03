// REVIEW-JUDGEMENT-008 terminal judgement and interrupt boundary.
import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'
import * as judge from '../../../dist/Mission/Review/OpenCode/JudgeSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as reviewJournal from '../../../dist/Persistence/Journal/ReviewJournalSurface.js'

const verdictWitness = (value) => review.verdictWitness({ ProviderRun: value.run, ToolCallId: value.call, GitTreeHash: value.tree, ReviewerSessionId: value.reviewer })
const attempt = review.attemptIdentity('bar_finality', verdictWitness({ run: 'run_1', call: 'call_1', tree: 'tree_finality', reviewer: 'ses_finality_rev' }))
const guardWrap = (handle) => ({ handle, Witness: review.guardWitness(handle) })
const unwrap = (value) => value.handle ?? value
const empty = guardWrap(review.emptyGuard())
const apply = (attemptValue, verdictValue, current) => {
  const result = review.applyVerdict(attemptValue, verdictValue, unwrap(current))
  return result.ok ? { ok: true, value: guardWrap(result.value) } : result
}
const read = (value) => {
  const result = review.guardView(unwrap(value))
  return { ...result, witness: result.witness.state, seals: result.sealCount }
}

const appendBarrierAndVerdict = async (opened, reviewerSessionId, providerRun = 'run-exact', toolCallId = 'call-exact') => {
  assert.equal((await reviewJournal.appendReview(opened.journal, reviewerSessionId, null, 'ReviewBarrierStarted', {
    ReviewerSessionId: reviewerSessionId,
    ManagerSessionId: 'ses-manager',
    BarrierId: 'bar-finality',
    GitTreeHash: 'tree-1',
  })).ok, true)
  assert.equal((await reviewJournal.appendReview(opened.journal, reviewerSessionId, providerRun, 'ReviewVerdictRecorded', {
    ReviewerSessionId: reviewerSessionId,
    ManagerSessionId: 'ses-manager',
    BarrierId: 'bar-finality',
    GitTreeHash: 'tree-1',
    ProviderRun: providerRun,
    ToolCallId: toolCallId,
    Verdict: 'PERFECT',
  })).ok, true)
}

const appendToolResult = async (opened, reviewerSessionId, sequence, providerRun, toolCallId) => {
  const result = await reviewJournal.appendAgent(opened.journal, reviewerSessionId, providerRun, 'Companion', 'XTracePartAppended', {
    SessionId: reviewerSessionId,
    CursorSequence: sequence,
    Role: 'assistant',
    Turn: 1,
    PartIndex: sequence,
    Kind: 'tool_result',
    ToolName: 'judge',
    TextRef: `blob-${sequence}`,
    TextDigest: `digest-${sequence}`,
    Provenance: `g:0/msg:${providerRun}/host-part:${sequence}`,
    ProviderRun: providerRun,
    ToolCallId: toolCallId,
    HostToolPartId: `part-${sequence}`,
  })
  assert.equal(result.ok, true, JSON.stringify(result))
}

const invokeInterrupt = async (opened, reviewerSessionId, physicalUserMessageId) => {
  const scheduled = []
  const interrupted = []
  const sessionPort = {
    InterruptAttempt: async (sessionId) => {
      interrupted.push(sessionId)
      return { ok: true }
    },
  }
  const result = await judge.interruptAfterSubmittedJudgement(
    opened.journal,
    physicalUserMessageId,
    (work) => scheduled.push(work),
    sessionPort,
    reviewerSessionId,
  )
  return { result, scheduled, interrupted }
}

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_a_single_verdict_never_becomes_a_confirmed_witness_by_itself', () => {
  const applied = apply(attempt, 'PERFECT', empty)
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  assert.equal(read(applied.value).observedAttempts, 1)
  assert.equal(read(applied.value).witness, 'NoReview')
  assert.equal(review.satisfiesGuard('tree_finality', unwrap(applied.value)), false)
  assert.equal(review.isConfirmed(applied.value.Witness), false)

  const revised = apply(attempt, 'REVISE', empty)
  assert.equal(revised.ok, true)
  assert.equal(revised.value.Witness.state, 'RevisionWitness')
  assert.equal(review.isConfirmed(revised.value.Witness), false)
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_wrong_run_and_call_decoys_cannot_authorize_terminal_interrupt', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-review-decoy-tool-result-'))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, 'writer-decoy-tool-result', 'rt-decoy-tool-result', 4242, '2026-01-01T00:00:00Z')
  try {
    const reviewerSessionId = 'ses-reviewer-decoy'
    const physicalUserMessageId = 'msg-user-1'
    await appendBarrierAndVerdict(opened, reviewerSessionId)
    await appendToolResult(opened, reviewerSessionId, 5, 'run-wrong', 'call-exact')
    await appendToolResult(opened, reviewerSessionId, 6, 'run-exact', 'call-wrong')
    judge.markVerdictSubmitted(reviewerSessionId, physicalUserMessageId)
    const observed = await invokeInterrupt(opened, reviewerSessionId, physicalUserMessageId)
    assert.equal(observed.result.ok, true, observed.result.error)
    assert.deepEqual(observed.scheduled, [], 'wrong-run and wrong-call parts must not schedule an interrupt')
    assert.deepEqual(observed.interrupted, [])
    const session = reviewJournal.sessionView(opened.journal, reviewerSessionId)
    assert.deepEqual(session.closedAttempts, [], 'decoy tool results must not close the exact judgement attempt')
  } finally {
    judge.clearVerdictSubmissions()
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_exact_tool_result_closes_before_background_interrupt', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-review-exact-tool-result-'))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, 'writer-exact-tool-result', 'rt-exact-tool-result', 4242, '2026-01-01T00:00:00Z')
  try {
    const reviewerSessionId = 'ses-reviewer-exact'
    const physicalUserMessageId = 'msg-user-exact'
    await appendBarrierAndVerdict(opened, reviewerSessionId)
    await appendToolResult(opened, reviewerSessionId, 5, 'run-wrong', 'call-exact')
    await appendToolResult(opened, reviewerSessionId, 6, 'run-exact', 'call-wrong')
    await appendToolResult(opened, reviewerSessionId, 7, 'run-exact', 'call-exact')
    judge.markVerdictSubmitted(reviewerSessionId, physicalUserMessageId)

    const observed = await invokeInterrupt(opened, reviewerSessionId, physicalUserMessageId)
    assert.equal(observed.result.ok, true, observed.result.error)
    assert.equal(observed.scheduled.length, 1, 'exact durable closure must schedule one physical interrupt')
    assert.deepEqual(observed.interrupted, [], 'production entry must return before the physical interrupt starts')
    assert.deepEqual(reviewJournal.sessionView(opened.journal, reviewerSessionId).closedAttempts, [{
      providerRun: 'run-exact',
      toolCallId: 'call-exact',
      frontier: 8n,
    }])

    await observed.scheduled[0]()
    assert.deepEqual(observed.interrupted, [reviewerSessionId])
  } finally {
    judge.clearVerdictSubmissions()
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_real_append_failure_never_schedules_interrupt', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-review-append-fail-'))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, 'writer-append-fail', 'rt-append-fail', 4242, '2026-01-01T00:00:00Z')
  try {
    const reviewerSessionId = 'ses-reviewer-append-fail'
    const physicalUserMessageId = 'msg-user-2'
    await appendBarrierAndVerdict(opened, reviewerSessionId)
    await appendToolResult(opened, reviewerSessionId, 5, 'run-exact', 'call-exact')

    const eventsDirectory = join(directory, 'wanxiang', 'events')
    rmSync(eventsDirectory, { recursive: true, force: true })
    writeFileSync(eventsDirectory, 'physical append obstruction')
    judge.markVerdictSubmitted(reviewerSessionId, physicalUserMessageId)
    const observed = await invokeInterrupt(opened, reviewerSessionId, physicalUserMessageId)
    assert.equal(observed.result.ok, false, 'production interrupt entry must expose append failure')
    assert.match(observed.result.error, /REVIEW_013_TERMINAL_CLOSURE_FAILED/i)
    assert.deepEqual(observed.scheduled, [], 'closure append failure must not schedule an interrupt')
    assert.deepEqual(observed.interrupted, [])
  } finally {
    judge.clearVerdictSubmissions()
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

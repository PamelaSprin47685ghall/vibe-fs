// REVIEW-JUDGEMENT-008 terminal judgement and interrupt boundary.
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
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
const source = (path) => readFileSync(new URL(`../../../${path}`, import.meta.url), 'utf8')

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

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_first_terminal_receipt_is_physically_enforced_at_provider_transform', () => {
  const judgeTool = source('src/Wanxiangshu/Mission/Review/OpenCode/JudgeTool.fs')
  const transforms = source('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')
  const finalityHost = source('src/Wanxiangshu/Mission/Finality/OpenCode/HostPort.fs')
  const reviewerWorkflow = source('src/Wanxiangshu/Mission/Review/Judgement/Workflow.fs')
  const reviewPorts = source('src/Wanxiangshu/Mission/Review/Ports.fs')
  const terminalAwait = source('src/Wanxiangshu/Mission/Review/OpenCode/TerminalAwait.fs')
  const changeReview = source('src/Wanxiangshu/Change/Host/ReviewRunner.fs')
  const changeHost = source('src/Wanxiangshu/Change/Host/Host.fs')

  const duplicate = judgeTool.match(/\| ExecutionDecision\.AlreadyJudged ->([\s\S]*?)\| ExecutionDecision\.Proceed judgement/)
  assert.ok(duplicate, 'duplicate judgement branch must remain explicit')
  assert.match(duplicate[1], /return alreadyJudged context/)
  assert.doesNotMatch(duplicate[1], /InterruptAttempt|abortSession/, 'second judge must not own physical termination')

  assert.match(judgeTool, /let private decideSubmittedInterrupt[\s\S]*?currentPhysicalUserMessage[\s\S]*?SharedState\.VerdictSubmissions\.Contains/)
  assert.match(
    judgeTool,
    /let private interruptClosedSubmittedJudgement[\s\S]*?ensureSubmittedAttemptClosed[\s\S]*?awaitSubmittedRecordCapture[\s\S]*?scheduleInterrupt \(\)/,
    'closure and already-open Blogger producer settlement must precede scheduling the physical interrupt',
  )
  assert.match(judgeTool, /let interruptAfterSubmittedJudgement[\s\S]*?decideSubmittedInterrupt[\s\S]*?interruptClosedSubmittedJudgement/)
  assert.match(transforms, /InterruptAfterSubmittedJudgement:\s*string option -> Task<unit>/)
  assert.doesNotMatch(
    transforms,
    /\.InterruptAttempt\(/,
    'the transform composition root must not own raw reviewer attempt termination',
  )
  assert.match(
    judgeTool,
    /let private interruptClosedSubmittedJudgement[\s\S]*?runBackground[\s\S]*?sessionPort\.InterruptAttempt reviewerSessionId[\s\S]*?terminalReadiness/,
    'JudgeTool owns the raw physical interrupt only beside its durable closure successor proof',
  )
  assert.match(
    transforms,
    /JudgeTool\.interruptAfterSubmittedJudgement[\s\S]*?journal[\s\S]*?wired\.CurrentPhysicalUserMessage[\s\S]*?scope\.RunBackground[\s\S]*?sessionPort/,
  )
  assert.match(transforms, /caps\.SanitizeMessages outObj[\s\S]*?do! caps\.InterruptAfterSubmittedJudgement projectionSessionIdOpt/)
  assert.doesNotMatch(
    judgeTool,
    /let private interruptClosedSubmittedJudgement[\s\S]*?do!\s+sessionPort\.InterruptAttempt/,
    'messages.transform must never synchronously await the Host abort it is waiting to unblock',
  )
  assert.match(
    reviewerWorkflow,
    /let private decideSubmittedRecordCapture[\s\S]*?reviewerHasChronicle snapshot reviewerSessionId[\s\S]*?SubmittedRecordCaptureDecision\.AlreadyCaptured[\s\S]*?reviewerHasLinkedBlogger snapshot reviewerSessionId[\s\S]*?SubmittedRecordCaptureDecision\.NoBloggerRequired[\s\S]*?SubmittedRecordCaptureDecision\.AwaitFirstChronicle/,
    'record capture must decide from canonical Chronicle coverage before admitting a producer wait',
  )
  assert.match(
    reviewerWorkflow,
    /let private awaitFirstChronicle[\s\S]*?BloggerRuntimeHost\.awaitOpenProducerSettlement[\s\S]*?let awaitSubmittedRecordCapture[\s\S]*?SubmittedRecordCaptureDecision\.AwaitFirstChronicle[\s\S]*?awaitFirstChronicle/,
    'only the named first-Chronicle decision may wait an already-open Blogger producer',
  )
  assert.match(
    reviewerWorkflow,
    /XTraceProjection\.toolResultParts[\s\S]*?XTraceProjection\.frontierAfter/,
    'review closure must derive its frontier from the trace-owned provider/tool identity query',
  )
  assert.match(
    reviewerWorkflow,
    /XTraceCapture\.captureTerminalTextWithReceipt[\s\S]*?\| Ok _ ->[\s\S]*?\| Error error -> reportCaptureFailure/,
    'the review-owned tool-only fallback must consume the typed terminal capture receipt',
  )
  assert.match(
    reviewerWorkflow,
    /TerminalReporter\.completeWithEvidence[\s\S]*?XTraceTerminalCompletion\.Published[\s\S]*?XTraceTerminalCompletion\.CaptureFailed[\s\S]*?XTraceTerminalCompletion\.RejectedEmptyOutput[\s\S]*?XTraceTerminalCompletion\.RejectedMissingRole/,
    'ordinary reviewer completion must exhaust the terminal reporter evidence outcomes',
  )
  assert.doesNotMatch(
    reviewerWorkflow,
    /XTraceProjection\.(?:parts|currentGenerationParts|head|headSequence|semanticCursorFor|tryHostMessageId)\b|\.Cursor\.Sequence\b|\{\s*Sequence\s*=/,
    'review judgement must not inspect raw trace storage or cursor representation',
  )
  assert.match(
    reviewPorts,
    /type ReviewerTerminalOccasion\s*=\s*\{ ReviewerSessionId: SessionId\s*BarrierId: ReviewBarrierId \}/,
    'reviewer terminal authority must carry the reusable session and exact barrier as one typed occasion',
  )
  assert.match(
    terminalAwait,
    /let tryDurablyClosedJudgementRun[\s\S]*?barrierId: ReviewBarrierId[\s\S]*?guard\.CurrentBarrierId = Some barrierId[\s\S]*?attempt\.ReviewBarrierId = barrierId[\s\S]*?ReviewProjection\.closedAttemptOf attempt guard[\s\S]*?attempt\.ProviderRun/,
    'clean-abort evidence must be scoped to the exact current review barrier',
  )
  assert.match(
    terminalAwait,
    /let hasDurablyClosedJudgement journal reviewerSessionId barrierId =[\s\S]*?tryDurablyClosedJudgementRun journal reviewerSessionId barrierId[\s\S]*?Option\.isSome/,
    'the public closure predicate must delegate to the exact barrier-scoped run witness',
  )
  assert.match(
    terminalAwait,
    /let private terminalResult[\s\S]*?TerminalOutcome\.Completed run -> return Ok run\.ProviderRun[\s\S]*?TerminalOutcome\.Aborted _ -> return closedJudgementRunResult journal reviewerSessionId barrierId/,
    'the shared terminal interpreter must preserve exact ProviderRun identity and authorize Abort only from durable barrier-scoped closure',
  )
  assert.match(
    terminalAwait,
    /occasion: ReviewerTerminalOccasion[\s\S]*?let reviewerSessionId = occasion\.ReviewerSessionId[\s\S]*?let barrierId = occasion\.BarrierId[\s\S]*?SubscribeFutureTerminal/,
    'the physical wait must subscribe using the typed review occasion',
  )
  assert.match(finalityHost, /let awaitTerminal \(occasion: ReviewerTerminalOccasion\)[\s\S]*?ReviewerTerminalAwait\.awaitFuture scope\.Journal scope\.Sessions occasion reviewerTimeoutMs/)
  assert.match(changeReview, /awaitReviewer: ReviewerTerminalOccasion -> Task<Result<ProviderRunIdentity, string>>[\s\S]*?let terminalOccasion =[\s\S]*?ReviewerSessionId = reviewerSessionId[\s\S]*?BarrierId = barrierId[\s\S]*?AwaitReviewer = fun \(\) -> awaitReviewer terminalOccasion/)
  assert.match(changeHost, /fun \(occasion: ReviewerTerminalOccasion\) ->[\s\S]*?ReviewerTerminalAwait\.awaitFuture[\s\S]*?deps\.Journal[\s\S]*?deps\.Sessions[\s\S]*?occasion/)
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_ensure_submitted_attempt_closed_returns_ok_false_when_tool_result_missing_and_does_not_interrupt', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-review-missing-tool-result-'))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, 'writer-missing-tool-result', 'rt-missing-tool-result', 4242, '2026-01-01T00:00:00Z')
  try {
    const reviewerSessionId = 'ses-reviewer-missing'
    const managerSessionId = 'ses-manager'
    const physicalUserMessageId = 'msg-user-1'

    await reviewJournal.appendReview(opened.journal, reviewerSessionId, null, 'ReviewBarrierStarted', {
      ReviewerSessionId: reviewerSessionId,
      ManagerSessionId: managerSessionId,
      BarrierId: 'bar-finality',
      GitTreeHash: 'tree-1',
    })
    await reviewJournal.appendReview(opened.journal, reviewerSessionId, 'run-1', 'ReviewVerdictRecorded', {
      ReviewerSessionId: reviewerSessionId,
      ManagerSessionId: managerSessionId,
      BarrierId: 'bar-finality',
      GitTreeHash: 'tree-1',
      ProviderRun: 'run-1',
      ToolCallId: 'call-1',
      Verdict: 'PERFECT',
    })

    const closureResult = await judge.ensureSubmittedAttemptClosed(opened.journal, reviewerSessionId)
    assert.deepEqual(closureResult, { ok: true, closed: false })

    judge.markVerdictSubmitted(reviewerSessionId, physicalUserMessageId)
    let interrupted = false
    const sessionPort = {
      InterruptAttempt: async () => {
        interrupted = true
        return { ok: true }
      },
    }

    const interruptResult = await judge.interruptAfterSubmittedJudgement(
      opened.journal,
      physicalUserMessageId,
      sessionPort,
      reviewerSessionId,
    )
    assert.equal(interruptResult.ok, true, `unexpected error: ${interruptResult.error}`)
    assert.equal(interrupted, false, 'interrupt must NOT be triggered when tool_result part is missing from XTrace')
    assert.equal(interruptResult.interrupted, false)

    const session = reviewJournal.sessionView(opened.journal, reviewerSessionId)
    assert.equal(session.closedAttempts.length, 0, 'no closed attempt must be recorded')
  } finally {
    judge.clearVerdictSubmissions()
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_ensure_submitted_attempt_closed_returns_error_on_append_failure_and_fails_closed', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-review-append-fail-'))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, 'writer-append-fail', 'rt-append-fail', 4242, '2026-01-01T00:00:00Z')
  try {
    const reviewerSessionId = 'ses-reviewer-append-fail'
    const managerSessionId = 'ses-manager'
    const physicalUserMessageId = 'msg-user-2'

    await reviewJournal.appendReview(opened.journal, reviewerSessionId, null, 'ReviewBarrierStarted', {
      ReviewerSessionId: reviewerSessionId,
      ManagerSessionId: managerSessionId,
      BarrierId: 'bar-finality',
      GitTreeHash: 'tree-1',
    })
    await reviewJournal.appendReview(opened.journal, reviewerSessionId, 'run-1', 'ReviewVerdictRecorded', {
      ReviewerSessionId: reviewerSessionId,
      ManagerSessionId: managerSessionId,
      BarrierId: 'bar-finality',
      GitTreeHash: 'tree-1',
      ProviderRun: 'run-1',
      ToolCallId: 'call-1',
      Verdict: 'PERFECT',
    })
    await reviewJournal.appendAgent(opened.journal, reviewerSessionId, 'run-1', 'Companion', 'XTracePartAppended', {
      SessionId: reviewerSessionId,
      CursorSequence: 5,
      Role: 'assistant',
      Turn: 1,
      PartIndex: 0,
      Kind: 'tool_result',
      ToolName: null,
      TextRef: 'blob-judge-result',
      TextDigest: 'digest-judge-result',
      Provenance: 'g:0/msg:review-run/host-part:judge-result',
      ProviderRun: 'run-1',
      ToolCallId: 'call-1',
      HostToolPartId: 'prt-judge-result',
    })

    journal.JournalSurface_dispose(opened.journal)

    const closureResult = await judge.ensureSubmittedAttemptClosed(opened.journal, reviewerSessionId)
    assert.equal(closureResult.ok, false, 'ensureSubmittedAttemptClosed must return Error on append failure')
    assert.ok(typeof closureResult.error === 'string' && closureResult.error.length > 0)

    judge.markVerdictSubmitted(reviewerSessionId, physicalUserMessageId)
    let interrupted = false
    const sessionPort = {
      InterruptAttempt: async () => {
        interrupted = true
        return { ok: true }
      },
    }

    const interruptResult = await judge.interruptAfterSubmittedJudgement(
      opened.journal,
      physicalUserMessageId,
      sessionPort,
      reviewerSessionId,
    )
    assert.equal(interruptResult.ok, false, 'interruptAfterSubmittedJudgement must fail closed on append failure')
    assert.match(interruptResult.error, /REVIEW_013_TERMINAL_CLOSURE_FAILED/i)
    assert.equal(interrupted, false, 'interrupt must NOT be triggered when closure append fails')
  } finally {
    judge.clearVerdictSubmissions()
    rmSync(directory, { recursive: true, force: true })
  }
})

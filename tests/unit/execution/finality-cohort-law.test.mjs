// tests/unit/execution/finality-cohort-law.test.mjs — GLORY-040/042/044/045/055/060/072/073/074/075.
//
// The combinator law tests of the timing-control-flow proposal §18, asserted on
// the observable behaviour of the Finality cohort through the real Host surface
// (`suicide` tool + `ReviewController.submit` + one real journal), because
// `FinalityController.concurrentAllOrShortCircuit` / `raceWithCancel` are
// module-private in production (no visibility was widened for tests):
//
//   concurrentAllOrShortCircuit
//     - the first Revision short-circuits the cohort; `FinalityRejected` waits
//       for its covered canonical work record
//     - all Confirmed gathers all into one `FinalityBlessed` bundle
//     - the short-circuit cancels the sibling driver before its next effect
//       and never disposes a durable session
//   ensureX (enlistMember)
//     - an existing durable fact ⇒ zero physical send (session reused, no
//       second fork, no duplicate enlistment)
//     - a missing fact ⇒ exactly one effect
//     - crash-after-effect-before-next-bind ⇒ replay from facts idempotent
//   awaitProjection spirit
//     - the challenge continuation subscribes before its send, so a terminal
//       wake right after the nudge is never lost
//     - the reverify recursion is bounded (one challenge nudge per member) and
//       cancellable (a short-circuited sibling gets no continuation at all)

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import test from 'node:test'

import {
  acceptAuthorityRoot,
  acceptChildAgentOwnerRoot,
  activateLife,
  awaitPrompted,
  notifyCompleted,
  observeTerminalSubscriptions,
  withExecutablePlugin,
} from '../plugin/plugin-fixture.mjs'
import {
  agentFact,
  agentJournal,
  bloggerRequestId,
  caseOf,
  frameEpochId,
  handleController,
  idValue,
  listItems,
  mapEntries,
  mapTryFind,
  payloadOf,
  prefixEpochId,
  promptDispatcher,
  reviewChallenge,
  roles,
  sessionId,
  transportReceipt,
  utcOffset,
  xTraceCapture,
} from '../support/domain.mjs'

import { ReviewController_submit, VerdictSubmission } from '../../../dist/Session/ReviewController.js'
import { AgentFact, ManagerLifecycleFact, ReviewFactCases, ReviewGuardVerdict } from '../../../dist/Kernel/Fact.js'
import {
  AgentJournalModule_appendAgent,
  AgentJournalModule_appendManagerLifecycle,
  AgentJournalModule_awaitChangeFrom,
  AgentJournalModule_revision,
  AgentJournalModule_snapshot,
  AgentJournal__WriteBlob_Z721C83C5,
} from '../../../dist/Journal/AgentJournal.js'
import { StreamId } from '../../../dist/Journal/Envelope.js'
import {
  FinalityRequestIdModule_create,
  GitTreeHashModule_create,
  PhysicalUserMessageIdModule_create,
  ProviderRunIdentityModule_create,
  SealDigestModule_create,
  ToolCallIdModule_create,
} from '../../../dist/Kernel/Identity.js'
import { contentDigest } from '../../../dist/Domain/ReviewChallenge.js'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'
import { create as createGitTree } from '../../../dist/Infrastructure/OpenCode/Host/GitTree.js'
import { ensureContinuation } from '../../../dist/Application/Review/ReviewerWorkflow.js'

// Role.Reviewer is Fable case tag 6 (Kernel/Roles.fs order).
const ROLE_REVIEWER = 6

// A visible sha256 stand-in for the challenge digest algebra (REVIEW-003): the
// property under test is which text is digested, not the hash function.
const H = (input) => `H(${input})`

const CHALLENGE_DIGEST_TEXT = idValue.sealDigest(contentDigest(H))

const suicideContext = (call, run) => ({
  sessionID: 'mgr',
  agent: 'fast-manager',
  callID: call,
  messageID: run,
})

const sessionsOf = (journal) => AgentJournalModule_snapshot(journal).AgentProjections.Sessions

const managerLife = (journal) => mapTryFind(sessionId('mgr'), sessionsOf(journal)).ManagerLife

const currentRequest = (journal) => managerLife(journal).CurrentLife.ActiveFinality

const memberOf = (request, reviewerValue) =>
  mapEntries(request.Members).find(([key]) => idValue.session(key) === reviewerValue)?.[1]

/** The enlist path awaits before `CreateChildSession`, so the id is not
 *  available synchronously after `execute` returns. */
const waitForSession = async (createdIds, index) => {
  for (let attempt = 0; attempt < 200; attempt += 1) {
    if (createdIds[index] !== undefined) return createdIds[index]
    await new Promise((resolve) => setTimeout(resolve, 25))
  }
  throw new Error(`child session ${index} never appeared; have ${JSON.stringify(createdIds)}`)
}

const promptsFor = (runtime, sessionValue) =>
  runtime.prompts.filter((entry) => (entry?.path?.id ?? entry?.sessionID) === sessionValue)

/** Wait until the session has received at least `count` production prompts.
 *  `awaitPrompted` is one-shot and becomes a no-op after the first wake, so
 *  reusing a Reviewer session for a second request must count prompts. */
const waitForPromptCount = async (runtime, sessionValue, count) => {
  for (let attempt = 0; attempt < 200; attempt += 1) {
    if (promptsFor(runtime, sessionValue).length >= count) return
    await new Promise((resolve) => setTimeout(resolve, 25))
  }
  throw new Error(
    `session ${sessionValue} never reached ${count} prompts; have ${promptsFor(runtime, sessionValue).length}`,
  )
}

/** Accept the AgentOwnerRoot that the production fork just sent, so a later
 *  re-enlistment of the same session can claim a new root without colliding
 *  with a still-pending claim. Already-accepted keys are a no-op: production
 *  may have observed the physical message before the test body runs. */
const acceptLatestPrompt = (runtime, sessionValue) => {
  const prompt = [...runtime.prompts]
    .reverse()
    .find((entry) => (entry?.path?.id ?? entry?.sessionID) === sessionValue)
  const key =
    prompt?.body?.metadata?.wanxiangshu_prompt_key ??
    prompt?.body?.parts?.find((part) => part?.type === 'text')?.metadata?.wanxiangshu_prompt_key
  assert.ok(key, `latest prompt for ${sessionValue} must carry a PromptKey`)
  try {
    acceptChildAgentOwnerRoot(runtime, sessionValue, key)
  } catch (error) {
    if (!String(error?.message ?? error).includes('is not a pending AgentOwnerRoot')) throw error
  }
}

/** GLORY-037.14: the ending needs a readable tree; an unborn repo has none. */
const commitWorkspace = (directory) => {
  execFileSync('git', ['-C', directory, 'add', '-A'])
  execFileSync('git', [
    '-C', directory,
    '-c', 'user.name=wxs',
    '-c', 'user.email=wxs@example.com',
    'commit', '--allow-empty', '-m', 'init',
  ])
}

/** GLORY-049: give a reviewer the canonical LWR the wound/blessing paths read.
 *  The first trace part is consumed as the opening span (openingEnd = first
 *  cursor + 1), so the deliberation must be a SECOND part to survive into the
 *  rendered gap. */
const captureWorkRecord = (journal, reviewerValue, deliberation) => {
  xTraceCapture.captureOpening(journal, sessionId(reviewerValue), `review assignment ${reviewerValue}`)
  xTraceCapture.captureProjection(
    journal,
    sessionId(reviewerValue),
    xTraceCapture.semantic({
      messages: [
        {
          role: 'assistant',
          parts: [xTraceCapture.text('start of the review'), xTraceCapture.text(deliberation)],
        },
      ],
    }),
  )
  const record = xTraceCapture.lifecycleWorkRecord(journal, sessionId(reviewerValue), false)
  assert.ok(record && record.trim() !== '', `reviewer ${reviewerValue} must have a canonical work record`)
  return record
}

/** Await a specific cohort shape through journal changes, never wall-clock time. */
const awaitOpenCohort = async (journal, memberCount) => {
  for (;;) {
    const request = currentRequest(journal)
    if (request && caseOf(request.Resolution) === 'Open' && mapEntries(request.Members).length === memberCount) {
      return request
    }
    const revision = AgentJournalModule_revision(journal)
    await AgentJournalModule_awaitChangeFrom(revision, journal)
  }
}

const awaitResolution = async (journal, requestId, resolution) => {
  for (;;) {
    const request = currentRequest(journal)
    if (
      request
      && idValue.finalityRequest(request.RequestId) === idValue.finalityRequest(requestId)
      && caseOf(request.Resolution) === resolution
    ) {
      return request
    }
    const revision = AgentJournalModule_revision(journal)
    await AgentJournalModule_awaitChangeFrom(revision, journal)
  }
}

const reviewerIds = (request) => mapEntries(request.Members).map(([reviewer]) => idValue.session(reviewer))

/** Production TerminalFrontier.Sequence = lastPart + 1 (exclusive head).
 *  Real Blogger IngestedThroughSequence max = lastPart (= this value - 1). */
const terminalFrontier = (journal, reviewerValue) => {
  const session = mapTryFind(sessionId(reviewerValue), sessionsOf(journal))
  const parts = listItems(session?.XTrace?.Parts ?? [])
  assert.ok(parts.length >= 2, `${reviewerValue} must have the two-part review trace`)
  return Math.max(...parts.map((part) => Number(part.Cursor.Sequence))) + 1
}

/** Durable terminal evidence fixes the frontier before the REVISE fact arrives. */
const appendTerminalEvidence = (journal, reviewerValue, run, text) => {
  const written = AgentJournal__WriteBlob_Z721C83C5(journal, text)
  assert.equal(written.tag, 0, `terminal evidence blob write rejected: ${written.fields?.[0]}`)
  const receipt = written.fields[0]
  const appended = AgentJournalModule_appendAgent(
    new StreamId(1, [sessionId(reviewerValue)]),
    ProviderRunIdentityModule_create(run),
    agentFact('TerminalOutputCaptured', {
      SessionId: sessionId(reviewerValue),
      TextRef: receipt.BlobRef,
      TextDigest: receipt.BlobDigest,
      ProviderRun: ProviderRunIdentityModule_create(run),
    }),
    journal,
  )
  assert.equal(appended.tag, 0, `TerminalOutputCaptured append rejected: ${appended.fields?.[0]}`)
}

/** Controlled durable Blogger receipt; no transform, timer, or polling participates. */
const appendBlogCoverage = (journal, reviewerValue, { previous, next, label }) => {
  const written = AgentJournal__WriteBlob_Z721C83C5(journal, `${label} durable work log`)
  assert.equal(written.tag, 0, `BlogEntryCommitted blob write rejected: ${written.fields?.[0]}`)
  const receipt = written.fields[0]
  const run = ProviderRunIdentityModule_create(`blog-run-${label}`)
  const appended = AgentJournalModule_appendAgent(
    new StreamId(1, [sessionId(reviewerValue)]),
    run,
    agentFact('BlogEntryCommitted', {
      SessionId: sessionId(reviewerValue),
      BloggerSessionId: sessionId(`blogger-${reviewerValue}`),
      RequestId: bloggerRequestId(`blog-request-${label}`),
      FrameEpochId: frameEpochId(0),
      PreviousIngestedThroughSequence: BigInt(previous),
      NextIngestedThroughSequence: BigInt(next),
      PreviousCoverableTurnCutoffExclusive: 0,
      NextCoverableTurnCutoffExclusive: 0,
      NextCoveredPrefixDigest: `covered-${label}`,
      TextRef: receipt.BlobRef,
      TextDigest: receipt.BlobDigest,
      ProviderRun: run,
      ToolCallIds: ofArray([]),
      TipRuleId: `tip-${label}`,
      FieldNameAtCommit: `field-${label}`,
      EvidenceRef: undefined,
      ObservedPrefixEpochId: prefixEpochId(0),
    }),
    journal,
  )
  assert.equal(appended.tag, 0, `BlogEntryCommitted append rejected: ${appended.fields?.[0]}`)
}

const makeRecordReady = (journal, reviewerValue, run, label) => {
  const frontier = terminalFrontier(journal, reviewerValue)
  appendTerminalEvidence(journal, reviewerValue, run, `${label} terminal evidence`)
  appendBlogCoverage(journal, reviewerValue, { previous: 0, next: frontier, label })
  return frontier
}

const rejectionsFor = (journal, requestId) =>
  agentJournal
    .persistedEnvelopes(journal)
    .filter((envelope) => caseOf(envelope.Fact) === 'ManagerLifecycle')
    .map((envelope) => payloadOf(envelope.Fact))
    .filter((fact) => caseOf(fact) === 'FinalityRejected')
    .map(payloadOf)
    .filter((payload) => idValue.finalityRequest(payload.RequestId) === idValue.finalityRequest(requestId))

/** Active Companion Blogger + Handle so coverageCanAdvance can flip to false on abandon. */
const linkActiveCompanionBlogger = (journal, reviewerValue) => {
  const bloggerValue = `blogger-${reviewerValue}`
  const bloggerAgentId = `blogger-agent-${reviewerValue}`
  const association = AgentJournalModule_appendAgent(
    new StreamId(1, [sessionId(reviewerValue)]),
    undefined,
    agentFact('CompanionBloggerLinked', {
      SessionId: sessionId(reviewerValue),
      BloggerSessionId: sessionId(bloggerValue),
      BloggerAgent: 'fast-blogger',
    }),
    journal,
  )
  assert.equal(association.tag, 0, `CompanionBloggerLinked rejected: ${association.fields?.[0]}`)
  const linked = handleController.link(
    journal,
    sessionId(reviewerValue),
    bloggerAgentId,
    sessionId(bloggerValue),
    'fast-blogger',
    roles.of('Blogger'),
  )
  assert.equal(linked.ok, true, linked.ok ? '' : linked.error)
  return { bloggerValue, bloggerAgentId }
}

const abandonCompanionBlogger = (journal, reviewerValue, bloggerAgentId) => {
  const abandoned = handleController.recordAbandon(
    journal,
    sessionId(reviewerValue),
    bloggerAgentId,
    'DeadlineExceeded',
    utcOffset('2026-03-01T12:00:00Z'),
  )
  assert.equal(abandoned.ok, true, abandoned.ok ? '' : abandoned.error)
}

/** Drop process-local journal waiters (crash); durable REVISE evidence stays. */
const crashLocalJournalWaiters = (journal) => {
  assert.ok(Array.isArray(journal.waiters), 'AgentJournal waiter collection is unavailable')
  const orphaned = journal.waiters.splice(0, journal.waiters.length)
  for (const entry of orphaned) {
    const tcs = entry?.[1]
    if (tcs && typeof tcs.SetCancelled === 'function') tcs.SetCancelled()
  }
}

const awaitSubscriptions = async (subscriptions, count) => {
  for (let index = 0; index < count; index += 1) await subscriptions.next()
}

/** One verdict through the real single writer (ReviewController.submit). */
const submitVerdict = (journal, request, reviewerValue, run, call, verdict) => {
  const member = memberOf(request, reviewerValue)
  assert.ok(member, `${reviewerValue} must be an enlisted member`)
  const decision = ReviewController_submit(
    journal,
    H,
    new VerdictSubmission(
      member.BarrierId,
      request.GitTreeHash,
      sessionId('mgr'),
      sessionId(reviewerValue),
      undefined,
      undefined,
      ProviderRunIdentityModule_create(run),
      ToolCallIdModule_create(call),
      verdict,
    ),
  )
  assert.equal(decision.tag, 0, `verdict submit rejected: ${decision.fields?.[0]}`)
  return decision.fields[0]
}

/** REVIEW-010: the input seal the second PERFECT must consume (fold fact). */
const sealChallengeResponse = (journal, reviewerValue, run) => {
  const result = AgentJournalModule_appendAgent(
    new StreamId(1, [sessionId(reviewerValue)]),
    ProviderRunIdentityModule_create(run),
    new AgentFact(2, [
      new ReviewFactCases(3, [
        {
          CanonicalVersion: 1,
          IncludedToolResultDigests: ofArray([SealDigestModule_create(CHALLENGE_DIGEST_TEXT)]),
          PhysicalUserMessageId: PhysicalUserMessageIdModule_create(`phys-${reviewerValue}`),
          ProviderRun: ProviderRunIdentityModule_create(run),
          SealDigest: SealDigestModule_create(`seal-${run}`),
          SessionId: sessionId(reviewerValue),
        },
      ]),
    ]),
    journal,
  )
  assert.equal(result.tag, 0, `ProviderInputSealed append rejected: ${result.fields?.[0]}`)
}

const driveReviewerContinuation = async (journal, runtime, reviewerValue, run) => {
  const sessionPort = {
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    SendPrompt: async (sid, text, options) => {
      runtime.prompts.push({ path: { id: idValue.session(sid) }, body: { text, options } })
      return { tag: 0, fields: [promptDispatcher.admittedWithReceipt(transportReceipt(`receipt-${reviewerValue}-${run}`))] }
    },
  }
  runtime.reviewerNudges ??= new Set()
  return ensureContinuation(
    sessionPort,
    journal,
    runtime.reviewerNudges,
    sessionId(reviewerValue),
    ProviderRunIdentityModule_create(run),
    reviewerValue,
  )
}

/** One member's causally confirmed dual PERFECT (REVIEW-003/010). */
const dualPerfect = async (journal, runtime, request, reviewerValue, label) => {
  const challengeSeen = () =>
    promptsFor(runtime, reviewerValue).some((entry) => {
      const text = JSON.stringify(entry)
      return text.includes(reviewChallenge.text) || text.includes('re-evaluate')
    })

  submitVerdict(journal, request, reviewerValue, `run-${label}-first`, `call-${label}-first`, ReviewGuardVerdict.Perfect)
  notifyCompleted(runtime, reviewerValue, `wide ${label} first`, `formal ${label} first`, ROLE_REVIEWER)
  await driveReviewerContinuation(journal, runtime, reviewerValue, `run-${label}-first`)

  for (let attempt = 0; attempt < 200; attempt += 1) {
    if (challengeSeen()) break
    await new Promise((resolve) => setTimeout(resolve, 25))
    if (attempt === 199) {
      throw new Error(`challenge continuation never reached ${reviewerValue}`)
    }
  }

  sealChallengeResponse(journal, reviewerValue, `run-${label}-second`)
  submitVerdict(journal, request, reviewerValue, `run-${label}-second`, `call-${label}-second`, ReviewGuardVerdict.Perfect)
  notifyCompleted(runtime, reviewerValue, `wide ${label} second`, `formal ${label} second`, ROLE_REVIEWER)
}

// ── GLORY-044: concurrentAllOrShortCircuit ──────────────────────────────────

test('GLORY_044_the_first_Revision_short_circuits_to_rejection_and_never_disposes', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    commitWorkspace(directory)
    acceptAuthorityRoot(runtime, 'mgr', 'fast-manager')
    activateLife(runtime, 'mgr')

    const suicide = hooks.tool.suicide.execute(
      { last_words: 'the work is done' },
      suicideContext('call-s-1', 'run-mgr-1'),
    )
    const reviewer = await waitForSession(createdIds, 0)
    await waitForPromptCount(runtime, reviewer, 1)
    acceptLatestPrompt(runtime, reviewer)
    captureWorkRecord(runtime.journal, reviewer, 'deliberation one')
    makeRecordReady(runtime.journal, reviewer, 'run-r-1', 'first-revision')

    const request = currentRequest(runtime.journal)
    submitVerdict(runtime.journal, request, reviewer, 'run-r-1', 'call-r-1', ReviewGuardVerdict.Revise)
    notifyCompleted(runtime, reviewer, 'wide one', 'formal one', ROLE_REVIEWER)

    const result = await suicide
    assert.ok(result.startsWith('# Your ending has not accepted you.'), result)
    // GLORY-055: the short-circuit closes the request immediately…
    assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Rejected')
    // …and never disposes the durable reviewer session.
    assert.deepEqual(runtime.abortedIds, [], 'a REVISE must not dispose the durable reviewer session')
    assert.equal(
      mapEntries(managerLife(runtime.journal).CurrentLife.EnlistedReviewers).length,
      1,
      'the rejected member stays enlisted for the next request',
    )
  })
})

test('GLORY_044_all_Confirmed_gathers_all_into_one_blessing_bundle', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    commitWorkspace(directory)
    acceptAuthorityRoot(runtime, 'mgr', 'fast-manager')
    activateLife(runtime, 'mgr')

    // Round 1: one REVISE rejection leaves one ungraduated historical reviewer.
    const roundOne = hooks.tool.suicide.execute(
      { last_words: 'first ending' },
      suicideContext('call-s-1', 'run-mgr-1'),
    )
    const historical = await waitForSession(createdIds, 0)
    await waitForPromptCount(runtime, historical, 1)
    acceptLatestPrompt(runtime, historical)
    captureWorkRecord(runtime.journal, historical, 'deliberation alpha')
    makeRecordReady(runtime.journal, historical, 'run-a-1', 'round-one-revision')
    submitVerdict(runtime.journal, currentRequest(runtime.journal), historical, 'run-a-1', 'call-a-1', ReviewGuardVerdict.Revise)
    notifyCompleted(runtime, historical, 'wide a', 'formal a', ROLE_REVIEWER)
    await roundOne
    assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Rejected')

    // Round 2: the cohort is the historical reviewer (session reused) plus
    // exactly one new reviewer.
    const roundTwo = hooks.tool.suicide.execute(
      { last_words: 'second ending' },
      suicideContext('call-s-2', 'run-mgr-2'),
    )
    const newcomer = await waitForSession(createdIds, 1)
    assert.equal(createdIds.length, 2, 'reuse must not create a second session for the historical reviewer')
    await waitForPromptCount(runtime, historical, 2)
    acceptLatestPrompt(runtime, historical)
    await waitForPromptCount(runtime, newcomer, 1)
    acceptLatestPrompt(runtime, newcomer)
    captureWorkRecord(runtime.journal, newcomer, 'deliberation beta')

    const request = currentRequest(runtime.journal)
    assert.equal(mapEntries(request.Members).length, 2, 'one reused member + exactly one new member')

    // Both members produce a causally confirmed dual PERFECT; the second
    // terminal lands immediately after the challenge nudge, so a wake lost by a
    // subscribe-after-send race would hang the drive (awaitProjection spirit).
    await dualPerfect(runtime.journal, runtime, request, historical, 'a2')
    await dualPerfect(runtime.journal, runtime, request, newcomer, 'b2')

    const result = await roundTwo
    assert.ok(result.startsWith('# Your ending has accepted you'), result)
    // GLORY-060: the bundle is the stable-ordinal concatenation of both LWRs.
    assert.ok(result.includes('deliberation alpha'), 'bundle must carry the historical reviewer canonical record')
    assert.ok(result.includes('deliberation beta'), 'bundle must carry the new reviewer canonical record')
    assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Blessed')
    // The bounded recursion nudged each member exactly once on the blessing round
    // (assignment + challenge). Historical also carries the rejected round's assignment.
    const promptsPerReviewer = (value) => promptsFor(runtime, value).length
    assert.equal(promptsPerReviewer(historical), 3, 'historical reviewer: reject-assignment + blessing-assignment + challenge')
    assert.equal(promptsPerReviewer(newcomer), 2, 'new reviewer: assignment + one challenge, never more')
    // GLORY-055/060: only the BLESSED path releases the physical sessions, and
    // only after the bundle landed.
    assert.deepEqual([...runtime.abortedIds].sort(), [historical, newcomer].sort())
  })
})

test('GLORY_044_a_Revision_short_circuit_cancels_the_sibling_before_its_next_effect', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    commitWorkspace(directory)
    acceptAuthorityRoot(runtime, 'mgr', 'fast-manager')
    activateLife(runtime, 'mgr')

    // Round 1: rejection creates the historical reviewer.
    const roundOne = hooks.tool.suicide.execute(
      { last_words: 'first ending' },
      suicideContext('call-s-1', 'run-mgr-1'),
    )
    const historical = await waitForSession(createdIds, 0)
    await waitForPromptCount(runtime, historical, 1)
    acceptLatestPrompt(runtime, historical)
    captureWorkRecord(runtime.journal, historical, 'deliberation alpha')
    makeRecordReady(runtime.journal, historical, 'run-a-1', 'sibling-round-one-revision')
    submitVerdict(runtime.journal, currentRequest(runtime.journal), historical, 'run-a-1', 'call-a-1', ReviewGuardVerdict.Revise)
    notifyCompleted(runtime, historical, 'wide a', 'formal a', ROLE_REVIEWER)
    await roundOne

    // Round 2: two members. The historical reviewer lands a first PERFECT (its
    // next effect would be the challenge continuation); the newcomer REVISE wins
    // the race and short-circuits first.
    const roundTwo = hooks.tool.suicide.execute(
      { last_words: 'second ending' },
      suicideContext('call-s-2', 'run-mgr-2'),
    )
    const newcomer = await waitForSession(createdIds, 1)
    await waitForPromptCount(runtime, historical, 2)
    acceptLatestPrompt(runtime, historical)
    await waitForPromptCount(runtime, newcomer, 1)
    acceptLatestPrompt(runtime, newcomer)
    captureWorkRecord(runtime.journal, newcomer, 'deliberation beta')
    makeRecordReady(runtime.journal, newcomer, 'run-c-2', 'sibling-round-two-revision')

    const request = currentRequest(runtime.journal)
    // Land both verdicts first so the journal already knows the REVISE winner.
    // Notify the REVISE member first so the short-circuit cancel is set before
    // the sibling's terminal can drive a challenge continuation.
    submitVerdict(runtime.journal, request, historical, 'run-c-1', 'call-c-1', ReviewGuardVerdict.Perfect)
    submitVerdict(runtime.journal, request, newcomer, 'run-c-2', 'call-c-2', ReviewGuardVerdict.Revise)
    notifyCompleted(runtime, newcomer, 'wide c2', 'formal c2', ROLE_REVIEWER)
    await awaitResolution(runtime.journal, request.RequestId, 'Rejected')
    notifyCompleted(runtime, historical, 'wide c1', 'formal c1', ROLE_REVIEWER)
    await driveReviewerContinuation(runtime.journal, runtime, historical, 'run-c-1')

    const result = await roundTwo
    assert.ok(result.startsWith('# Your ending has not accepted you.'), result)
    assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Rejected')
    // The sibling stopped before its next effect: no challenge continuation.
    const promptTexts = runtime.prompts.map((prompt) => JSON.stringify(prompt)).join('\n')
    assert.equal(promptTexts.includes(reviewChallenge.text), false, 'the cancelled sibling must not receive the challenge continuation')
    // Cancellation never disposes a durable session.
    assert.deepEqual(runtime.abortedIds, [], 'the short-circuit must not dispose either durable session')
  })
})

test('GLORY_044_072_073_REVISE_closes_the_cohort_but_waits_for_the_matching_covered_work_record', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    commitWorkspace(directory)
    acceptAuthorityRoot(runtime, 'mgr', 'fast-manager')
    activateLife(runtime, 'mgr')

    // Establish one durable, ungraduated historical reviewer. Its complete first
    // rejection keeps the second request focused on the delayed-record race.
    const firstSubscriptions = observeTerminalSubscriptions(runtime)
    const firstRound = hooks.tool.suicide.execute(
      { last_words: 'first ending' },
      suicideContext('call-race-1', 'run-race-manager-1'),
    )
    const firstRequest = await awaitOpenCohort(runtime.journal, 1)
    const [historical] = reviewerIds(firstRequest)
    assert.ok(historical, 'the first cohort must enlist one historical reviewer')
    await awaitPrompted(historical)
    acceptLatestPrompt(runtime, historical)
    captureWorkRecord(runtime.journal, historical, 'historical deliberation')
    makeRecordReady(runtime.journal, historical, 'run-race-historical-revise', 'historical-ready')
    await awaitSubscriptions(firstSubscriptions, 3)
    firstSubscriptions.restore()
    submitVerdict(
      runtime.journal,
      firstRequest,
      historical,
      'run-race-historical-revise',
      'call-race-historical-revise',
      ReviewGuardVerdict.Revise,
    )
    notifyCompleted(runtime, historical, 'historical wide', 'historical formal', ROLE_REVIEWER)
    await firstRound

    // The next roster contains that pending-PERFECT sibling plus one fresh
    // rejecting reviewer. Listener observations are event-driven readiness
    // barriers: two runs each install Host, dispatch, and Finality subscriptions.
    const secondSubscriptions = observeTerminalSubscriptions(runtime)
    const secondRound = hooks.tool.suicide.execute(
      { last_words: 'second ending' },
      suicideContext('call-race-2', 'run-race-manager-2'),
    )
    const request = await awaitOpenCohort(runtime.journal, 2)
    const newcomer = reviewerIds(request).find((value) => value !== historical)
    assert.ok(newcomer, 'the second cohort must add exactly one new reviewer')
    await Promise.all([awaitPrompted(historical), awaitPrompted(newcomer)])
    acceptLatestPrompt(runtime, historical)
    acceptLatestPrompt(runtime, newcomer)
    await awaitSubscriptions(secondSubscriptions, 6)
    secondSubscriptions.restore()

    captureWorkRecord(runtime.journal, newcomer, 'delayed reviewer deliberation')
    const frontier = terminalFrontier(runtime.journal, newcomer)
    assert.ok(frontier > 1, 'the delayed record must have an older frontier to reject')
    appendTerminalEvidence(runtime.journal, newcomer, 'run-race-revise', 'durable rejecting terminal evidence')
    submitVerdict(
      runtime.journal,
      request,
      historical,
      'run-race-sibling-perfect',
      'call-race-sibling-perfect',
      ReviewGuardVerdict.Perfect,
    )

    // This observes the actual AgentJournal B-class subscription. It races only
    // the Finality result: old production resolves via FinalityRejected; the
    // required implementation registers AwaitChangeFrom before any durable wound.
    const recordReadyWait = agentJournal.observeWaiters(runtime.journal)
    try {
      submitVerdict(
        runtime.journal,
        request,
        newcomer,
        'run-race-revise',
        'call-race-revise',
        ReviewGuardVerdict.Revise,
      )
      notifyCompleted(runtime, newcomer, 'rejecting wide', 'rejecting formal', ROLE_REVIEWER)

      const initialProgress = await Promise.race([
        secondRound.then((outcome) => ({ kind: 'settled', outcome })),
        recordReadyWait.next().then(() => ({ kind: 'waiting' })),
      ])
      assert.equal(
        initialProgress.kind,
        'waiting',
        `REVISE must await record-ready instead of resolving ${initialProgress.outcome ?? 'FinalityRejected'}`,
      )
      assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Open')
      assert.equal(rejectionsFor(runtime.journal, request.RequestId).length, 0, 'no WorkRecordRef before matching coverage')

      const siblingPromptCount = promptsFor(runtime, historical).length
      await driveReviewerContinuation(runtime.journal, runtime, historical, 'run-race-sibling-perfect')
      assert.equal(
        promptsFor(runtime, historical).length,
        siblingPromptCount,
        'a REVISE closes the sibling continuation before record-ready',
      )

      // A fully covered different reviewer must not release this reviewer.
      appendBlogCoverage(runtime.journal, 'unrelated-reviewer', {
        previous: 0,
        next: frontier,
        label: 'mismatched-reviewer',
      })
      const afterMismatch = await Promise.race([
        secondRound.then((outcome) => ({ kind: 'settled', outcome })),
        recordReadyWait.next().then(() => ({ kind: 'waiting' })),
      ])
      assert.equal(afterMismatch.kind, 'waiting', 'mismatched Blogger coverage must not unlock rejection')
      assert.equal(rejectionsFor(runtime.journal, request.RequestId).length, 0)

      // Real-reachable coverage = lastPart = frontier-1 with a work-log frame
      // unlocks rejection. Expecting 'waiting' here encoded the off-by-one hang.
      appendBlogCoverage(runtime.journal, newcomer, {
        previous: 0,
        next: frontier - 1,
        label: 'older-target-frontier',
      })
      const afterOlderCoverage = await Promise.race([
        secondRound.then((outcome) => ({ kind: 'settled', outcome })),
        recordReadyWait.next().then(() => ({ kind: 'waiting' })),
      ])
      assert.equal(
        afterOlderCoverage.kind,
        'settled',
        'real-reachable lastPart coverage with a work log must unlock FinalityRejected',
      )
      const result = afterOlderCoverage.outcome
      assert.ok(result.startsWith('# Your ending has not accepted you.'), result)

      const rejections = rejectionsFor(runtime.journal, request.RequestId)
      assert.equal(rejections.length, 1, 'lastPart coverage lands exactly one FinalityRejected')
      const record = agentJournal.readBlob(runtime.journal, rejections[0].WorkRecordRef)
      assert.equal(record.ok, true, record.ok ? '' : record.error)
      assert.match(record.value, /Work log\n\S/, 'FinalityRejected must reference a non-empty covered work log')
      assert.match(record.value, /older-target-frontier durable work log/)
    } finally {
      recordReadyWait.restore()
    }
  })
})

// ── GLORY-040/057: ensureX — the idempotent enlistment ──────────────────────

test('GLORY_040_crash_between_request_and_enlistment_replays_the_ensure_idempotently', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    commitWorkspace(directory)
    acceptAuthorityRoot(runtime, 'mgr', 'fast-manager')
    activateLife(runtime, 'mgr')

    // The crash: `FinalityRequested` landed, the process died before the first
    // enlistment bound. Re-entry must continue from facts, not from memory.
    const requestId = FinalityRequestIdModule_create('req-crash-1')
    const blob = AgentJournal__WriteBlob_Z721C83C5(runtime.journal, 'last words of the crashed run')
    assert.equal(blob.tag, 0, `last_words blob write rejected: ${blob.fields?.[0]}`)
    const receipt = blob.fields[0]
    const appended = AgentJournalModule_appendManagerLifecycle(
      new StreamId(1, [sessionId('mgr')]),
      new ManagerLifecycleFact(2, [
        {
          SessionId: sessionId('mgr'),
          LifeId: managerLife(runtime.journal).CurrentLife.LifeId,
          RequestId: requestId,
          GitTreeHash: GitTreeHashModule_create(createGitTree(directory).GetTreeHash()),
          LastWordsRef: receipt.BlobRef,
          LastWordsDigest: receipt.BlobDigest,
          ProviderRun: ProviderRunIdentityModule_create('run-crash-1'),
          ToolCallId: ToolCallIdModule_create('call-crash-1'),
        },
      ]),
      runtime.journal,
    )
    assert.equal(appended.tag, 0, `FinalityRequested append rejected: ${appended.fields?.[0]}`)

    // The next suicide re-enters the SAME request and completes the missing
    // bind: exactly one session, exactly one enlistment.
    const retry = hooks.tool.suicide.execute(
      { last_words: 'retry after crash' },
      suicideContext('call-crash-2', 'run-crash-2'),
    )
    const reviewer = await waitForSession(createdIds, 0)
    await waitForPromptCount(runtime, reviewer, 1)
    acceptLatestPrompt(runtime, reviewer)
    captureWorkRecord(runtime.journal, reviewer, 'deliberation crash')
    makeRecordReady(runtime.journal, reviewer, 'run-crash-r', 'crash-revision')

    const request = currentRequest(runtime.journal)
    assert.equal(idValue.finalityRequest(request.RequestId), 'req-crash-1', 'the replay continues the SAME request')
    assert.equal(createdIds.length, 1, 'the missing effect ran exactly once')
    assert.equal(
      mapEntries(managerLife(runtime.journal).CurrentLife.EnlistedReviewers).length,
      1,
      'the replay must not duplicate the enlistment',
    )

    submitVerdict(runtime.journal, request, reviewer, 'run-crash-r', 'call-crash-r', ReviewGuardVerdict.Revise)
    notifyCompleted(runtime, reviewer, 'wide crash', 'formal crash', ROLE_REVIEWER)
    const result = await retry
    assert.ok(result.startsWith('# Your ending has not accepted you.'), result)
    assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Rejected')
    assert.deepEqual(runtime.abortedIds, [])
  })
})

// ── GLORY-073 regression: real-reachable coverage must unlock rejection ───────
//
// Production TerminalFrontier.Sequence = lastPart + 1. Real Blogger coverage
// tops out at lastPart. makeRecordReady fakes next: frontier and masks the
// recordReadiness `coverage >= frontier.Sequence` hang. This case uses only the
// reachable lastPart and bounds the wait so a hang fails fast.

test('GLORY_073_real_reachable_lastPart_coverage_unlocks_FinalityRejected_with_work_log', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    commitWorkspace(directory)
    acceptAuthorityRoot(runtime, 'mgr', 'fast-manager')
    activateLife(runtime, 'mgr')

    const subscriptions = observeTerminalSubscriptions(runtime)
    const ending = hooks.tool.suicide.execute(
      { last_words: 'real-reachable coverage ending' },
      suicideContext('call-real-cov-1', 'run-real-cov-mgr'),
    )
    const request = await awaitOpenCohort(runtime.journal, 1)
    const [reviewer] = reviewerIds(request)
    assert.ok(reviewer, 'cohort must enlist one reviewer')
    await awaitPrompted(reviewer)
    acceptLatestPrompt(runtime, reviewer)
    await awaitSubscriptions(subscriptions, 3)
    subscriptions.restore()

    captureWorkRecord(runtime.journal, reviewer, 'real-reachable deliberation')
    const frontier = terminalFrontier(runtime.journal, reviewer)
    assert.ok(frontier > 1, 'reviewer must have a two-part frontier')
    const lastPart = frontier - 1
    appendTerminalEvidence(runtime.journal, reviewer, 'run-real-cov-revise', 'real-reachable terminal evidence')
    appendBlogCoverage(runtime.journal, reviewer, {
      previous: 0,
      next: lastPart,
      label: 'real-reachable-lastPart',
    })

    submitVerdict(
      runtime.journal,
      request,
      reviewer,
      'run-real-cov-revise',
      'call-real-cov-revise',
      ReviewGuardVerdict.Revise,
    )
    notifyCompleted(runtime, reviewer, 'real-reachable wide', 'real-reachable formal', ROLE_REVIEWER)

    const BOUND_MS = 2500
    const outcome = await Promise.race([
      ending.then((result) => ({ kind: 'settled', result })),
      new Promise((resolve) => setTimeout(() => resolve({ kind: 'timeout' }), BOUND_MS)),
    ])
    assert.notEqual(
      outcome.kind,
      'timeout',
      `coverage=lastPart (${lastPart}) with a work-log frame must conclude FinalityRejected; hung for ${BOUND_MS}ms under coverage>=frontier`,
    )
    assert.ok(outcome.result.startsWith('# Your ending has not accepted you.'), outcome.result)
    assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Rejected')
    const rejections = rejectionsFor(runtime.journal, request.RequestId)
    assert.equal(rejections.length, 1, 'exactly one FinalityRejected')
    const record = agentJournal.readBlob(runtime.journal, rejections[0].WorkRecordRef)
    assert.equal(record.ok, true, record.ok ? '' : record.error)
    assert.match(record.value, /Work log\n\S/, 'FinalityRejected must reference a non-empty work log')
  })
})

// ── GLORY-074: Abandoned Blogger during record-ready → Undecided, no partial rejection ─
//
// coverageCanAdvance becomes false when the companion Blogger handle is Abandoned.
// Without a materializable `Work log`, recordReadiness is RecordUnavailable and
// concludeRejection fail-closes to FinalityUndecided — never a WorkRecordRef-less
// FinalityRejected.

test('GLORY_074_blogger_abandonment_during_record_ready_concludes_undecided_no_partial_rejection', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    commitWorkspace(directory)
    acceptAuthorityRoot(runtime, 'mgr', 'fast-manager')
    activateLife(runtime, 'mgr')

    const subscriptions = observeTerminalSubscriptions(runtime)
    const ending = hooks.tool.suicide.execute(
      { last_words: 'abandonment during record-ready' },
      suicideContext('call-abandon-1', 'run-abandon-mgr'),
    )
    const request = await awaitOpenCohort(runtime.journal, 1)
    const [reviewer] = reviewerIds(request)
    assert.ok(reviewer, 'cohort must enlist one reviewer')
    await awaitPrompted(reviewer)
    acceptLatestPrompt(runtime, reviewer)
    await awaitSubscriptions(subscriptions, 3)
    subscriptions.restore()

    captureWorkRecord(runtime.journal, reviewer, 'abandonment deliberation')
    const frontier = terminalFrontier(runtime.journal, reviewer)
    assert.ok(frontier > 1, 'reviewer must have a two-part frontier')
    appendTerminalEvidence(runtime.journal, reviewer, 'run-abandon-revise', 'abandonment terminal evidence')
    const { bloggerAgentId } = linkActiveCompanionBlogger(runtime.journal, reviewer)

    const recordReadyWait = agentJournal.observeWaiters(runtime.journal)
    try {
      submitVerdict(
        runtime.journal,
        request,
        reviewer,
        'run-abandon-revise',
        'call-abandon-revise',
        ReviewGuardVerdict.Revise,
      )
      notifyCompleted(runtime, reviewer, 'abandonment wide', 'abandonment formal', ROLE_REVIEWER)

      const BOUND_MS = 2500
      const initialProgress = await Promise.race([
        ending.then((result) => ({ kind: 'settled', result })),
        recordReadyWait.next().then(() => ({ kind: 'waiting' })),
        new Promise((resolve) => setTimeout(() => resolve({ kind: 'timeout' }), BOUND_MS)),
      ])
      assert.equal(
        initialProgress.kind,
        'waiting',
        `REVISE must await record-ready before abandonment; got ${initialProgress.kind}`,
      )
      assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Open')
      assert.equal(rejectionsFor(runtime.journal, request.RequestId).length, 0, 'no FinalityRejected before abandonment')

      abandonCompanionBlogger(runtime.journal, reviewer, bloggerAgentId)

      const outcome = await Promise.race([
        ending.then((result) => ({ kind: 'settled', result })),
        new Promise((resolve) => setTimeout(() => resolve({ kind: 'timeout' }), BOUND_MS)),
      ])
      assert.notEqual(
        outcome.kind,
        'timeout',
        `Abandoned Blogger must conclude Undecided within ${BOUND_MS}ms; must not hang on AwaitJournal`,
      )
      assert.ok(
        outcome.result.startsWith('# Your ending could not be decided.'),
        outcome.result,
      )
      assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Undecided')
      assert.equal(
        rejectionsFor(runtime.journal, request.RequestId).length,
        0,
        'Abandoned Blogger must not emit FinalityRejected / WorkRecordRef',
      )
      const rejectionLike = agentJournal
        .persistedEnvelopes(runtime.journal)
        .filter((envelope) => caseOf(envelope.Fact) === 'ManagerLifecycle')
        .map((envelope) => payloadOf(envelope.Fact))
        .filter((fact) => caseOf(fact) === 'FinalityRejected')
      assert.equal(rejectionLike.length, 0, 'no partial rejection blob without Work log')
    } finally {
      recordReadyWait.restore()
    }
  })
})

// ── GLORY-075: waiter crash → resumeDurableRevise from durable evidence, no timer poll ─
//
// Local waiter disposal is not durable abandonment. Re-entry with the same ToolCallId
// resumes concludeRejection / awaitRecordReady via awaitChangeFrom only.

test('GLORY_075_waiter_crash_resumes_from_durable_evidence_no_timer_poll', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    commitWorkspace(directory)
    acceptAuthorityRoot(runtime, 'mgr', 'fast-manager')
    activateLife(runtime, 'mgr')

    const subscriptions = observeTerminalSubscriptions(runtime)
    const firstEnding = hooks.tool.suicide
      .execute(
        { last_words: 'waiter crash before coverage' },
        suicideContext('call-075', 'run-075-mgr'),
      )
      .catch(() => ({ crashed: true }))
    const request = await awaitOpenCohort(runtime.journal, 1)
    const [reviewer] = reviewerIds(request)
    assert.ok(reviewer, 'cohort must enlist one reviewer')
    await awaitPrompted(reviewer)
    acceptLatestPrompt(runtime, reviewer)
    await awaitSubscriptions(subscriptions, 3)
    subscriptions.restore()

    captureWorkRecord(runtime.journal, reviewer, 'crash-resume deliberation')
    const frontier = terminalFrontier(runtime.journal, reviewer)
    assert.ok(frontier > 1, 'reviewer must have a two-part frontier')
    const lastPart = frontier - 1
    appendTerminalEvidence(runtime.journal, reviewer, 'run-075-revise', 'crash-resume terminal evidence')

    const firstWait = agentJournal.observeWaiters(runtime.journal)
    try {
      submitVerdict(
        runtime.journal,
        request,
        reviewer,
        'run-075-revise',
        'call-075-revise',
        ReviewGuardVerdict.Revise,
      )
      notifyCompleted(runtime, reviewer, 'crash-resume wide', 'crash-resume formal', ROLE_REVIEWER)

      const BOUND_MS = 2500
      const initialProgress = await Promise.race([
        firstEnding.then((result) => ({ kind: 'settled', result })),
        firstWait.next().then(() => ({ kind: 'waiting' })),
        new Promise((resolve) => setTimeout(() => resolve({ kind: 'timeout' }), BOUND_MS)),
      ])
      assert.equal(
        initialProgress.kind,
        'waiting',
        `REVISE must register awaitChangeFrom before coverage; got ${initialProgress.kind}`,
      )
      assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Open')
      assert.equal(rejectionsFor(runtime.journal, request.RequestId).length, 0)
    } finally {
      firstWait.restore()
    }

    // Process-local waiter death: durable REVISE + frontier remain; no lifecycle terminal.
    crashLocalJournalWaiters(runtime.journal)
    await firstEnding
    assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Open', 'waiter crash must not close the request')
    assert.equal(rejectionsFor(runtime.journal, request.RequestId).length, 0)

    const resumeWait = agentJournal.observeWaiters(runtime.journal)
    try {
      const resumed = hooks.tool.suicide.execute(
        { last_words: 'waiter crash before coverage' },
        suicideContext('call-075', 'run-075-resume'),
      )

      const BOUND_MS = 2500
      const afterResume = await Promise.race([
        resumed.then((result) => ({ kind: 'settled', result })),
        resumeWait.next().then(() => ({ kind: 'waiting' })),
        new Promise((resolve) => setTimeout(() => resolve({ kind: 'timeout' }), BOUND_MS)),
      ])
      assert.equal(
        afterResume.kind,
        'waiting',
        'resumeDurableRevise must re-register awaitChangeFrom (no timerTask/sleep re-probe)',
      )
      assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Open')
      assert.equal(rejectionsFor(runtime.journal, request.RequestId).length, 0, 'resume must not reject before coverage')
      assert.equal(
        mapEntries(currentRequest(runtime.journal).Members).length,
        1,
        'resume must not reopen / re-enlist the cohort',
      )

      appendBlogCoverage(runtime.journal, reviewer, {
        previous: 0,
        next: lastPart,
        label: 'crash-resume-lastPart',
      })

      const outcome = await Promise.race([
        resumed.then((result) => ({ kind: 'settled', result })),
        new Promise((resolve) => setTimeout(() => resolve({ kind: 'timeout' }), BOUND_MS)),
      ])
      assert.notEqual(outcome.kind, 'timeout', `resumed record-ready must settle within ${BOUND_MS}ms`)
      assert.ok(outcome.result.startsWith('# Your ending has not accepted you.'), outcome.result)
      assert.equal(caseOf(currentRequest(runtime.journal).Resolution), 'Rejected')
      const rejections = rejectionsFor(runtime.journal, request.RequestId)
      assert.equal(rejections.length, 1, 'exactly one FinalityRejected after durable resume + coverage')
      const record = agentJournal.readBlob(runtime.journal, rejections[0].WorkRecordRef)
      assert.equal(record.ok, true, record.ok ? '' : record.error)
      assert.match(record.value, /Work log\n\S/, 'FinalityRejected must reference a non-empty work log')
    } finally {
      resumeWait.restore()
    }
  })
})

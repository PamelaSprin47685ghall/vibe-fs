// tests/unit/execution/finality-cohort-law.test.mjs — GLORY-040/042/044/045/055/060.
//
// The combinator law tests of the timing-control-flow proposal §18, asserted on
// the observable behaviour of the Finality cohort through the real Host surface
// (`suicide` tool + `ReviewController.submit` + one real journal), because
// `FinalityController.concurrentAllOrShortCircuit` / `raceWithCancel` are
// module-private in production (no visibility was widened for tests):
//
//   concurrentAllOrShortCircuit
//     - the first Revision short-circuits to `FinalityRejected`
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
  notifyCompleted,
  withExecutablePlugin,
} from '../plugin/plugin-fixture.mjs'
import {
  caseOf,
  idValue,
  mapEntries,
  mapTryFind,
  promptDispatcher,
  reviewChallenge,
  sessionId,
  transportReceipt,
  xTraceCapture,
} from '../support/domain.mjs'

import { ReviewController_submit, VerdictSubmission } from '../../../dist/Session/ReviewController.js'
import { AgentFact, ManagerLifecycleFact, ReviewFactCases, ReviewGuardVerdict } from '../../../dist/Kernel/Fact.js'
import {
  AgentJournalModule_appendAgent,
  AgentJournalModule_appendManagerLifecycle,
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

    const request = currentRequest(runtime.journal)
    // Land both verdicts first so the journal already knows the REVISE winner.
    // Notify the REVISE member first so the short-circuit cancel is set before
    // the sibling's terminal can drive a challenge continuation.
    submitVerdict(runtime.journal, request, historical, 'run-c-1', 'call-c-1', ReviewGuardVerdict.Perfect)
    submitVerdict(runtime.journal, request, newcomer, 'run-c-2', 'call-c-2', ReviewGuardVerdict.Revise)
    notifyCompleted(runtime, newcomer, 'wide c2', 'formal c2', ROLE_REVIEWER)
    // Give the short-circuit a turn to cancel siblings before the Perfect
    // member's terminal is delivered.
    await new Promise((resolve) => setTimeout(resolve, 10))
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

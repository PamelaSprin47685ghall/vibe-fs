// Moved from tests/unit/temporal/finality-cohort-law.test.mjs (cutover Wave 2a); owner: finality
// tests/unit/temporal/finality-cohort-law.test.mjs — G4R temporal theorems for Finality cohort.
//
// Proves race-as-algebra on production FinalityReviewCohort + ManagerLifecycle fold.
// No test-only business logic: every assertion folds / rosterOf through production.
//
// Race model (G4R §10/§12 + GLORY-043/044/045):
//   Roster = ungraduated historical Reviewers + exactly one new slot.
//   Graduation is derived from ConfirmedReviewWitness on an enlisted barrier.
//   Enlistment replay is idempotent (exactly-once Members).
//   dropEphemeral recovers durable Finality resolution without a second completion.
//
// Traces are enumerated explicitly via DeterministicEventQueue. No wall clock is
// authority; VirtualClock is unused here because Finality roster algebra is pure.
//
// Production symbols invoked:
//   - FinalityReviewCohort.rosterOf / graduatedReviewer
//   - ManagerLifecycleFact fold (LifeOpened / WorkActivated / FinalityRequested /
//     FinalityReviewerEnlisted / FinalityRejected / FinalityBlessed)
//   - Fold.foldEnvelope / AgentProjection (ConfirmedReviewWitness → graduation)
//   - AgentJournal.appendManagerLifecycle + dropEphemeral (EventStore resume)

import assert from 'node:assert/strict'
import test from 'node:test'
import { rosterOf, graduatedReviewer } from '../../../dist/Composition/Bridges/FinalityReview/FinalityReviewCohort.js'
import { AgentJournalModule_appendManagerLifecycle } from '../../../dist/Persistence/Journal/AgentJournal.js'
import {
  agentJournal,
  blobDigest,
  blobRef,
  caseOf,
  envelope,
  fact,
  finalityRequestId,
  fold,
  gitTreeHash,
  idValue,
  listItems,
  managerLifeId,
  managerLifecycleFact,
  mapEntries,
  payloadOf,
  physicalUser,
  promptKey,
  providerRun,
  reviewBarrierId,
  sealDigest,
  sessionId,
  stream,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'
import {
  DeterministicEventQueue,
  createDurableWorld,
  dropEphemeral,
} from '../../verification-system/tests/support/temporal-harness.mjs'

// ── fixtures ────────────────────────────────────────────────────────────────

const MGR = sessionId('mgr')
const LIFE = managerLifeId('life-1')
const TREE = gitTreeHash('tree-1')
const BLOB = blobRef('blob-1')
const DIGEST = blobDigest('d-1')
const KEY = promptKey('key-1')
const REQ1 = finalityRequestId('req-1')
const REQ2 = finalityRequestId('req-2')
const HIST_A = sessionId('ses-hist-a')
const HIST_B = sessionId('ses-hist-b')
const NEW = sessionId('ses-new')
const BAR1 = reviewBarrierId('bar-1')
const BAR2 = reviewBarrierId('bar-2')
const BAR_A = reviewBarrierId('bar-a')
const BAR_B = reviewBarrierId('bar-b')

const mgrEnv = (lifecycleFact) => envelope({ stream: stream.session(MGR), fact: lifecycleFact })

const lifeOpened = () =>
  managerLifecycleFact('LifeOpened', {
    SessionId: MGR,
    LifeId: LIFE,
    OpeningUserMessageId: physicalUser('msg-open'),
    OpeningTextRef: BLOB,
    OpeningTextDigest: DIGEST,
    OpeningCursorSequence: 1n,
  })

const workActivated = () =>
  managerLifecycleFact('WorkActivated', {
    SessionId: MGR,
    LifeId: LIFE,
    ActivationPromptKey: KEY,
    ProtectedPrefixEndSequence: 42n,
  })

const finalityRequested = (requestId, run, call) =>
  managerLifecycleFact('FinalityRequested', {
    SessionId: MGR,
    LifeId: LIFE,
    RequestId: requestId,
    GitTreeHash: TREE,
    LastWordsRef: BLOB,
    LastWordsDigest: DIGEST,
    ProviderRun: providerRun(run),
    ToolCallId: toolCallId(call),
  })

const enlist = (requestId, reviewer, ordinal, barrier, isNew) =>
  managerLifecycleFact('FinalityReviewerEnlisted', {
    SessionId: MGR,
    LifeId: LIFE,
    RequestId: requestId,
    ReviewerSessionId: reviewer,
    ReviewerOrdinal: ordinal,
    BarrierId: barrier,
    GitTreeHash: TREE,
    IsNewReviewer: isNew,
  })

const finalityRejected = (requestId, reviewer, barrier) =>
  managerLifecycleFact('FinalityRejected', {
    SessionId: MGR,
    LifeId: LIFE,
    RequestId: requestId,
    RejectingReviewerSessionId: reviewer,
    BarrierId: barrier,
    GitTreeHash: TREE,
    WorkRecordRef: BLOB,
    WorkRecordDigest: DIGEST,
  })

const finalityBlessed = (requestId) =>
  managerLifecycleFact('FinalityBlessed', {
    SessionId: MGR,
    LifeId: LIFE,
    RequestId: requestId,
    GitTreeHash: TREE,
    WorkRecordBundleRef: BLOB,
    WorkRecordBundleDigest: DIGEST,
  })

const confirmWitness = (reviewer, barrier) => [
  envelope({
    stream: stream.session(reviewer),
    fact: fact('ReviewBarrierStarted', {
      ReviewerSessionId: reviewer,
      ManagerSessionId: MGR,
      BarrierId: barrier,
      GitTreeHash: TREE,
    }),
  }),
  envelope({
    stream: stream.session(reviewer),
    fact: fact('ConfirmedReviewWitness', {
      BarrierId: barrier,
      ChallengeResultDigest: sealDigest(`chal-${idValue.session(reviewer)}`),
      SecondProviderInputDigest: sealDigest(`in-${idValue.session(reviewer)}`),
      FirstProviderRun: providerRun(`rev1-${idValue.session(reviewer)}`),
      FirstToolCallId: toolCallId(`tc1-${idValue.session(reviewer)}`),
      GitTreeHash: TREE,
      ReviewerSessionId: reviewer,
      SecondProviderRun: providerRun(`rev2-${idValue.session(reviewer)}`),
      SecondToolCallId: toolCallId(`tc2-${idValue.session(reviewer)}`),
      ManagerSessionId: MGR,
    }),
  }),
]

const currentLife = (projection) => fold.session(projection, 'mgr')?.ManagerLife?.CurrentLife

const slotView = (slots) =>
  listItems(slots).map((slot) => ({
    agentId: slot.AgentId,
    session: slot.ReviewerSessionId == null ? null : idValue.session(slot.ReviewerSessionId),
    ordinal: slot.ReviewerOrdinal,
    isNew: slot.IsNew,
  }))

const membersView = (request) =>
  mapEntries(request.Members)
    .map(([sid, member]) => ({
      session: idValue.session(sid),
      ordinal: member.ReviewerOrdinal,
      barrier: idValue.reviewBarrier(member.BarrierId),
      isNew: member.IsNewReviewer,
    }))
    .sort((a, b) => a.session.localeCompare(b.session))

const standingOf = (life, reviewer) =>
  mapEntries(life.EnlistedReviewers).find(([sid]) => idValue.session(sid) === idValue.session(reviewer))?.[1]

const appendLifecycle = async (journal, lifecycleFact) => {
  const result = await AgentJournalModule_appendManagerLifecycle(
    stream.session(MGR),
    payloadOf(lifecycleFact),
    journal,
  )
  assert.equal(result.tag, 0, `appendManagerLifecycle rejected: ${JSON.stringify(result.fields)}`)
  return result
}

const uniqueDirectory = (label) =>
  `temporal-finality-${label}-${process.hrtime.bigint().toString(16)}-${Math.random().toString(16).slice(2)}`

// ── Theorem 1: roster algebra — ungraduated history + exactly one new ───────

test('THEOREM_finality_roster_is_ungraduated_history_plus_exactly_one_new', () => {
  // Trace T0: open Life → request1 enlist hist-a → reject → request2 open.
  // rosterOf(request2) MUST be [hist-a reused, exactly one new slot].
  const opened = fold.apply(fold.empty, [
    mgrEnv(lifeOpened()),
    mgrEnv(workActivated()),
    mgrEnv(finalityRequested(REQ1, 'run-1', 'call-1')),
    mgrEnv(enlist(REQ1, HIST_A, 0, BAR1, true)),
    mgrEnv(finalityRejected(REQ1, HIST_A, BAR1)),
    mgrEnv(finalityRequested(REQ2, 'run-2', 'call-2')),
  ])
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))

  const life = currentLife(opened.value)
  const request = life.ActiveFinality
  assert.equal(caseOf(request.Resolution), 'Open')
  assert.equal(mapEntries(request.Members).length, 0, 'new request starts with empty Members')

  const roster = slotView(rosterOf(opened.value.AgentProjections, life, request))
  assert.deepEqual(roster, [
    { agentId: 'finality-new-req-1', session: 'ses-hist-a', ordinal: 0, isNew: false },
    { agentId: 'finality-new-req-2', session: null, ordinal: 1, isNew: true },
  ])

  // Exactly-once new-slot law: rosterOf is a pure function of durable facts —
  // calling it twice yields the same algebra (no ephemeral fork counter).
  assert.deepEqual(slotView(rosterOf(opened.value.AgentProjections, life, request)), roster)
})

test('THEOREM_finality_graduated_reviewer_excluded_from_roster', () => {
  // Trace T1: same as T0, then ConfirmedReviewWitness on hist-a's enlisted barrier.
  // Graduation is DERIVED (GLORY-045); roster drops hist-a and keeps exactly one new.
  const base = fold.apply(fold.empty, [
    mgrEnv(lifeOpened()),
    mgrEnv(workActivated()),
    mgrEnv(finalityRequested(REQ1, 'run-1', 'call-1')),
    mgrEnv(enlist(REQ1, HIST_A, 0, BAR1, true)),
    mgrEnv(finalityRejected(REQ1, HIST_A, BAR1)),
    mgrEnv(finalityRequested(REQ2, 'run-2', 'call-2')),
  ])
  assert.equal(base.ok, true, base.ok ? '' : JSON.stringify(base.error))

  const confirmed = fold.apply(base.value, confirmWitness(HIST_A, BAR1))
  assert.equal(confirmed.ok, true, confirmed.ok ? '' : JSON.stringify(confirmed.error))

  const life = currentLife(confirmed.value)
  const standing = standingOf(life, HIST_A)
  assert.ok(standing, 'hist-a standing must survive across requests')
  assert.equal(
    graduatedReviewer(confirmed.value.AgentProjections, HIST_A, standing),
    true,
    'Confirmed witness on enlisted barrier graduates hist-a',
  )

  const roster = slotView(rosterOf(confirmed.value.AgentProjections, life, life.ActiveFinality))
  assert.deepEqual(roster, [
    { agentId: 'finality-new-req-2', session: null, ordinal: 1, isNew: true },
  ])
})

test('THEOREM_finality_crash_reentry_reuses_already_created_new_slot_exactly_once', () => {
  // Trace T2 (GLORY-045 crash re-entry): after the new Reviewer is enlisted,
  // rosterOf must reuse that session under IsNew=false and MUST NOT invent a
  // second new slot (exactly-once new Reviewer per request).
  const enlisted = fold.apply(fold.empty, [
    mgrEnv(lifeOpened()),
    mgrEnv(workActivated()),
    mgrEnv(finalityRequested(REQ1, 'run-1', 'call-1')),
    mgrEnv(enlist(REQ1, NEW, 0, BAR1, true)),
  ])
  assert.equal(enlisted.ok, true, enlisted.ok ? '' : JSON.stringify(enlisted.error))

  const life = currentLife(enlisted.value)
  const roster = slotView(rosterOf(enlisted.value.AgentProjections, life, life.ActiveFinality))
  assert.deepEqual(roster, [
    { agentId: 'finality-new-req-1', session: 'ses-new', ordinal: 0, isNew: false },
  ])

  // Duplicate enlistment of the same reviewer+barrier is absorbed (exactly-once Members).
  const replay = fold.apply(enlisted.value, [mgrEnv(enlist(REQ1, NEW, 0, BAR1, true))])
  assert.equal(replay.ok, true, replay.ok ? '' : JSON.stringify(replay.error))
  assert.deepEqual(membersView(currentLife(replay.value).ActiveFinality), membersView(life.ActiveFinality))
  assert.equal(mapEntries(currentLife(replay.value).ActiveFinality.Members).length, 1)
})

// ── Theorem 2: multi-historical enlistment order is confluent for roster ────

test('THEOREM_finality_historical_enlist_order_confluent_for_roster', () => {
  // Two independent historical enlistments on request1. Any interleaving of
  // (enlist A, enlist B) must fold to the same Members and, after reject +
  // request2, the same rosterOf algebra. Enumerate traces explicitly.
  const prefix = [
    mgrEnv(lifeOpened()),
    mgrEnv(workActivated()),
    mgrEnv(finalityRequested(REQ1, 'run-1', 'call-1')),
  ]
  const enlistA = mgrEnv(enlist(REQ1, HIST_A, 0, BAR_A, true))
  const enlistB = mgrEnv(enlist(REQ1, HIST_B, 1, BAR_B, false))
  const suffix = [
    mgrEnv(finalityRejected(REQ1, HIST_A, BAR_A)),
    mgrEnv(finalityRequested(REQ2, 'run-2', 'call-2')),
  ]

  const traces = DeterministicEventQueue.interleavings([enlistA], [enlistB])
  assert.equal(traces.length, 2, 'two explicit interleavings: A;B and B;A')

  const rosterViews = []
  const memberViews = []
  for (const mid of traces) {
    const folded = fold.apply(fold.empty, [...prefix, ...mid, ...suffix])
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    const life = currentLife(folded.value)
    // Round-1 Members are closed under Rejected request1; standing survives.
    assert.equal(mapEntries(life.EnlistedReviewers).length, 2)
    memberViews.push(
      mapEntries(life.EnlistedReviewers)
        .map(([sid, standing]) => ({
          session: idValue.session(sid),
          ordinal: standing.ReviewerOrdinal,
          barriers: listItems(standing.Barriers).map(idValue.reviewBarrier).sort(),
        }))
        .sort((a, b) => a.session.localeCompare(b.session)),
    )
    rosterViews.push(slotView(rosterOf(folded.value.AgentProjections, life, life.ActiveFinality)))
  }

  assert.deepEqual(memberViews[0], memberViews[1], 'enlist A;B vs B;A must converge standing')
  assert.deepEqual(rosterViews[0], rosterViews[1], 'rosterOf must be confluent across enlist races')
  // Both historical reviewers ungraduated + exactly one new.
  assert.equal(rosterViews[0].filter((s) => s.isNew).length, 1)
  assert.equal(rosterViews[0].filter((s) => !s.isNew).length, 2)
  assert.deepEqual(
    rosterViews[0].filter((s) => !s.isNew).map((s) => s.session).sort(),
    ['ses-hist-a', 'ses-hist-b'],
  )
})

// ── Theorem 3: terminal completion is exactly-once under fold ───────────────

test('THEOREM_finality_blessed_exactly_once_second_completion_rejected', () => {
  // Trace T3: Open → enlist → Blessed. A second Blessed on the same request
  // must not flip / double-write LastBlessing (fold rejects; projection stable).
  const blessed = fold.apply(fold.empty, [
    mgrEnv(lifeOpened()),
    mgrEnv(workActivated()),
    mgrEnv(finalityRequested(REQ1, 'run-1', 'call-1')),
    mgrEnv(enlist(REQ1, NEW, 0, BAR1, true)),
    mgrEnv(finalityBlessed(REQ1)),
  ])
  assert.equal(blessed.ok, true, blessed.ok ? '' : JSON.stringify(blessed.error))
  const once = currentLife(blessed.value)
  assert.equal(caseOf(once.ActiveFinality.Resolution), 'Blessed')
  assert.equal(idValue.finalityRequest(once.LastBlessing.RequestId), 'req-1')

  const again = fold.apply(blessed.value, [mgrEnv(finalityBlessed(REQ1))])
  assert.equal(again.ok, false, 'second FinalityBlessed must be rejected by production fold')
  // Projection from the successful fold remains the sole completion evidence.
  assert.equal(caseOf(once.ActiveFinality.Resolution), 'Blessed')
  assert.equal(idValue.finalityRequest(once.LastBlessing.RequestId), 'req-1')
})

// ── Theorem 4: dropEphemeral preserves durable finality; no second completion

test('THEOREM_drop_ephemeral_preserves_finality_facts_no_duplicate_completion', async () => {
  // G4R §12: world1 → durable FinalityBlessed F; DROP EPHEMERAL; world2 := recover(F)
  // → same Resolution/Members; re-append Blessed is refused (no second completion).
  const dir = uniqueDirectory('bless')
  const world1 = await createDurableWorld({ directory: dir, runtime: 'rt_finality_1', pid: 5101 })

  for (const lifecycleFact of [
    lifeOpened(),
    workActivated(),
    finalityRequested(REQ1, 'run-1', 'call-1'),
    enlist(REQ1, HIST_A, 0, BAR1, true),
    finalityRejected(REQ1, HIST_A, BAR1),
    finalityRequested(REQ2, 'run-2', 'call-2'),
    enlist(REQ2, NEW, 1, BAR2, true),
    finalityBlessed(REQ2),
  ]) {
    await appendLifecycle(world1.journal, lifecycleFact)
  }

  const before = currentLife(agentJournal.snapshot(world1.journal))
  assert.equal(caseOf(before.ActiveFinality.Resolution), 'Blessed')
  const beforeMembers = membersView(before.ActiveFinality)
  assert.deepEqual(beforeMembers, [
    { session: 'ses-new', ordinal: 1, barrier: 'bar-2', isNew: true },
  ])
  assert.equal(mapEntries(before.EnlistedReviewers).length, 2)

  const world2 = await dropEphemeral(world1, { runtime: 'rt_finality_recovered', pid: 5102 })
  const after = currentLife(agentJournal.snapshot(world2.journal))
  assert.equal(caseOf(after.ActiveFinality.Resolution), 'Blessed')
  assert.deepEqual(membersView(after.ActiveFinality), beforeMembers)
  assert.equal(idValue.finalityRequest(after.ActiveFinality.RequestId), 'req-2')
  assert.equal(idValue.finalityRequest(after.LastBlessing.RequestId), 'req-2')
  assert.equal(mapEntries(after.EnlistedReviewers).length, 2)

  // Resume must not accept a second Blessed completion for the same request.
  const duplicate = await AgentJournalModule_appendManagerLifecycle(
    stream.session(MGR),
    payloadOf(finalityBlessed(REQ2)),
    world2.journal,
  )
  assert.notEqual(duplicate.tag, 0, 'duplicate FinalityBlessed after resume must be refused')
  const still = currentLife(agentJournal.snapshot(world2.journal))
  assert.equal(caseOf(still.ActiveFinality.Resolution), 'Blessed')
  assert.deepEqual(membersView(still.ActiveFinality), beforeMembers)

  // Roster algebra still reachable from recovered durable projection:
  // ungraduated hist-a (not in Members) + crash-reentry new slot (IsNew=false).
  const recoveredRoster = slotView(
    rosterOf(agentJournal.snapshot(world2.journal).AgentProjections, still, still.ActiveFinality),
  )
  assert.deepEqual(recoveredRoster, [
    { agentId: 'finality-new-req-1', session: 'ses-hist-a', ordinal: 0, isNew: false },
    { agentId: 'finality-new-req-2', session: 'ses-new', ordinal: 1, isNew: false },
  ])

  world2.dispose()
})

test('THEOREM_drop_ephemeral_preserves_open_finality_roster_source', async () => {
  // Open request (not yet terminal): crash/resume must keep ActiveFinality Open
  // and the same EnlistedReviewers so rosterOf after resume still yields
  // ungraduated history + exactly one new — no re-enlist / no duplicate Members.
  const dir = uniqueDirectory('open')
  const world1 = await createDurableWorld({ directory: dir, runtime: 'rt_finality_open_1', pid: 5201 })

  for (const lifecycleFact of [
    lifeOpened(),
    workActivated(),
    finalityRequested(REQ1, 'run-1', 'call-1'),
    enlist(REQ1, HIST_A, 0, BAR1, true),
    finalityRejected(REQ1, HIST_A, BAR1),
    finalityRequested(REQ2, 'run-2', 'call-2'),
  ]) {
    await appendLifecycle(world1.journal, lifecycleFact)
  }

  const beforeSnap = agentJournal.snapshot(world1.journal)
  const beforeLife = currentLife(beforeSnap)
  assert.equal(caseOf(beforeLife.ActiveFinality.Resolution), 'Open')
  const beforeRoster = slotView(
    rosterOf(beforeSnap.AgentProjections, beforeLife, beforeLife.ActiveFinality),
  )
  assert.deepEqual(beforeRoster, [
    { agentId: 'finality-new-req-1', session: 'ses-hist-a', ordinal: 0, isNew: false },
    { agentId: 'finality-new-req-2', session: null, ordinal: 1, isNew: true },
  ])

  const world2 = await dropEphemeral(world1, { runtime: 'rt_finality_open_recovered', pid: 5202 })
  const afterSnap = agentJournal.snapshot(world2.journal)
  const afterLife = currentLife(afterSnap)
  assert.equal(caseOf(afterLife.ActiveFinality.Resolution), 'Open')
  assert.equal(idValue.finalityRequest(afterLife.ActiveFinality.RequestId), 'req-2')
  assert.equal(mapEntries(afterLife.ActiveFinality.Members).length, 0)
  assert.deepEqual(
    slotView(rosterOf(afterSnap.AgentProjections, afterLife, afterLife.ActiveFinality)),
    beforeRoster,
  )

  // Enlist the new reviewer once after resume; replay must not double Members.
  await appendLifecycle(world2.journal, enlist(REQ2, NEW, 1, BAR2, true))
  const enlistedLife = currentLife(agentJournal.snapshot(world2.journal))
  assert.deepEqual(membersView(enlistedLife.ActiveFinality), [
    { session: 'ses-new', ordinal: 1, barrier: 'bar-2', isNew: true },
  ])
  await appendLifecycle(world2.journal, enlist(REQ2, NEW, 1, BAR2, true))
  assert.deepEqual(
    membersView(currentLife(agentJournal.snapshot(world2.journal)).ActiveFinality),
    [{ session: 'ses-new', ordinal: 1, barrier: 'bar-2', isNew: true }],
  )

  world2.dispose()
})

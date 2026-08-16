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
import {
  agentJournal,
  blobDigest,
  blobRef,
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
  physicalUser,
  promptKey,
  providerRun,
  reviewBarrierId,
  sealDigest,
  sessionId,
  stream,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'
import { finalityCohort } from './support/finality-contract.mjs'
import * as finalitySurface from './support/finality-surface.mjs'
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

// ── PR 6: JS lifecycle events (FinalitySurface input vocabulary) ────────────

const mgrEvt = (evt) => ({ ...evt, sessionId: idValue.session(MGR) })

const lifeOpenedEvt = () => ({
  kind: 'life-opened',
  lifeId: idValue.managerLife(LIFE),
  openingUserMessageId: 'msg-open',
  openingTextRef: idValue.blobRef(BLOB),
  openingTextDigest: idValue.blobDigest(DIGEST),
  openingCursorSequence: 1,
})

const workActivatedEvt = () => ({
  kind: 'work-activated',
  lifeId: idValue.managerLife(LIFE),
  activationPromptKey: idValue.promptKey(KEY),
  protectedPrefixEndSequence: 42,
})

const finalityRequestedEvt = (requestId, run, call) => ({
  kind: 'finality-requested',
  lifeId: idValue.managerLife(LIFE),
  requestId: idValue.finalityRequest(requestId),
  gitTreeHash: idValue.gitTree(TREE),
  lastWordsRef: idValue.blobRef(BLOB),
  lastWordsDigest: idValue.blobDigest(DIGEST),
  providerRun: run,
  toolCallId: call,
})

const enlistEvt = (requestId, reviewer, ordinal, barrier, isNew) => ({
  kind: 'finality-reviewer-enlisted',
  lifeId: idValue.managerLife(LIFE),
  requestId: idValue.finalityRequest(requestId),
  reviewerSessionId: idValue.session(reviewer),
  reviewerOrdinal: ordinal,
  barrierId: idValue.reviewBarrier(barrier),
  gitTreeHash: idValue.gitTree(TREE),
  isNewReviewer: isNew,
})

const finalityRejectedEvt = (requestId, reviewer, barrier) => ({
  kind: 'finality-rejected',
  lifeId: idValue.managerLife(LIFE),
  requestId: idValue.finalityRequest(requestId),
  rejectingReviewerSessionId: idValue.session(reviewer),
  barrierId: idValue.reviewBarrier(barrier),
  gitTreeHash: idValue.gitTree(TREE),
  workRecordRef: idValue.blobRef(BLOB),
  workRecordDigest: idValue.blobDigest(DIGEST),
})

const finalityBlessedEvt = (requestId) => ({
  kind: 'finality-blessed',
  lifeId: idValue.managerLife(LIFE),
  requestId: idValue.finalityRequest(requestId),
  gitTreeHash: idValue.gitTree(TREE),
  workRecordBundleRef: idValue.blobRef(BLOB),
  workRecordBundleDigest: idValue.blobDigest(DIGEST),
})

const confirmWitnessEvt = (reviewer, barrier) => [
  mgrEvt({
    kind: 'review-barrier-started',
    reviewerSessionId: idValue.session(reviewer),
    barrierId: idValue.reviewBarrier(barrier),
    gitTreeHash: idValue.gitTree(TREE),
  }),
  mgrEvt({
    kind: 'confirmed-review-witness',
    barrierId: idValue.reviewBarrier(barrier),
    challengeResultDigest: `chal-${idValue.session(reviewer)}`,
    secondProviderInputDigest: `in-${idValue.session(reviewer)}`,
    firstProviderRun: `rev1-${idValue.session(reviewer)}`,
    firstToolCallId: `tc1-${idValue.session(reviewer)}`,
    gitTreeHash: idValue.gitTree(TREE),
    reviewerSessionId: idValue.session(reviewer),
    secondProviderRun: `rev2-${idValue.session(reviewer)}`,
    secondToolCallId: `tc2-${idValue.session(reviewer)}`,
  }),
]

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
  // `toJSON()[1]` is the single payload field of the top-level Fact wrapper
  // (Fable unions serialize as [name, payload...]) — the ManagerLifecycleFact
  // the AgentJournal append expects, without exposing union shape here.
  const result = await agentJournal.appendManagerLifecycle(
    stream.session(MGR),
    lifecycleFact.toJSON()[1],
    journal,
  )
  assert.equal(result.ok, true, `appendManagerLifecycle rejected: ${JSON.stringify(result.error)}`)
  return result
}

const uniqueDirectory = (label) =>
  `temporal-finality-${label}-${process.hrtime.bigint().toString(16)}-${Math.random().toString(16).slice(2)}`

// ── Theorem 1: roster algebra — ungraduated history + exactly one new ───────
// PR 6: expressed as JS lifecycle events through FinalitySurface; the roster
// answer is JS-shaped (agentId/reviewerSessionId/ordinal/isNew).

const surface = finalitySurface

const sv = (world) =>
  surface.cohortRoster(world).map((slot) => ({
    agentId: slot.agentId,
    session: slot.reviewerSessionId,
    ordinal: slot.ordinal,
    isNew: slot.isNew,
  }))

test('WHAT[FINALITY-009] roster is ungraduated history plus exactly one new', () => {
  // Trace T0: open Life → request1 enlist hist-a → reject → request2 open.
  // rosterOf(request2) MUST be [hist-a reused, exactly one new slot].
  const opened = surface.project([
    mgrEvt(lifeOpenedEvt()),
    mgrEvt(workActivatedEvt()),
    mgrEvt(finalityRequestedEvt(REQ1, 'run-1', 'call-1')),
    mgrEvt(enlistEvt(REQ1, HIST_A, 0, BAR1, true)),
    mgrEvt(finalityRejectedEvt(REQ1, HIST_A, BAR1)),
    mgrEvt(finalityRequestedEvt(REQ2, 'run-2', 'call-2')),
  ])
  assert.equal(opened.ok, true, JSON.stringify(opened.error))

  const view = surface.lifeView(opened.world)
  assert.equal(view.activeFinality.resolution.kind, 'open')
  assert.equal(view.activeFinality.members.length, 0, 'new request starts with empty Members')

  const roster = sv(opened.world)
  assert.deepEqual(roster, [
    { agentId: 'finality-new-req-1', session: 'ses-hist-a', ordinal: 0, isNew: false },
    { agentId: 'finality-new-req-2', session: null, ordinal: 1, isNew: true },
  ])

  // Exactly-once new-slot law: rosterOf is a pure function of durable facts —
  // calling it twice yields the same algebra (no ephemeral fork counter).
  assert.deepEqual(sv(opened.world), roster)
})

test('WHAT[FINALITY-010] graduated reviewer excluded from roster', () => {
  // Trace T1: same as T0, then ConfirmedReviewWitness on hist-a's enlisted barrier.
  // Graduation is DERIVED (GLORY-045); roster drops hist-a and keeps exactly one new.
  const base = surface.project([
    mgrEvt(lifeOpenedEvt()),
    mgrEvt(workActivatedEvt()),
    mgrEvt(finalityRequestedEvt(REQ1, 'run-1', 'call-1')),
    mgrEvt(enlistEvt(REQ1, HIST_A, 0, BAR1, true)),
    mgrEvt(finalityRejectedEvt(REQ1, HIST_A, BAR1)),
    mgrEvt(finalityRequestedEvt(REQ2, 'run-2', 'call-2')),
  ])
  assert.equal(base.ok, true, JSON.stringify(base.error))

  const confirmed = surface.applyEvents(base.world, confirmWitnessEvt(HIST_A, BAR1))
  assert.equal(confirmed.ok, true, JSON.stringify(confirmed.error))

  const life = surface.lifeView(confirmed.world)
  const standing = life.enlistedReviewers.find((r) => r.sessionId === 'ses-hist-a')?.standing
  assert.ok(standing, 'hist-a standing must survive across requests')
  assert.equal(
    surface.graduatedReviewer(confirmed.world, 'ses-hist-a'),
    true,
    'Confirmed witness on enlisted barrier graduates hist-a',
  )

  const roster = sv(confirmed.world)
  assert.deepEqual(roster, [
    { agentId: 'finality-new-req-2', session: null, ordinal: 1, isNew: true },
  ])
})

test('WHAT[FINALITY-009] crash reentry reuses already created new slot exactly once', () => {
  // Trace T2 (GLORY-045 crash re-entry): after the new Reviewer is enlisted,
  // rosterOf must reuse that session under IsNew=false and MUST NOT invent a
  // second new slot (exactly-once new Reviewer per request).
  const enlisted = surface.project([
    mgrEvt(lifeOpenedEvt()),
    mgrEvt(workActivatedEvt()),
    mgrEvt(finalityRequestedEvt(REQ1, 'run-1', 'call-1')),
    mgrEvt(enlistEvt(REQ1, NEW, 0, BAR1, true)),
  ])
  assert.equal(enlisted.ok, true, JSON.stringify(enlisted.error))

  const roster = sv(enlisted.world)
  assert.deepEqual(roster, [
    { agentId: 'finality-new-req-1', session: 'ses-new', ordinal: 0, isNew: false },
  ])

  // Duplicate enlistment of the same reviewer+barrier is absorbed (exactly-once Members).
  const replay = surface.applyEvents(enlisted.world, [mgrEvt(enlistEvt(REQ1, NEW, 0, BAR1, true))])
  assert.equal(replay.ok, true, JSON.stringify(replay.error))
  assert.equal(surface.lifeView(replay.world).activeFinality.members.length, 1)
})

// ── Theorem 2: multi-historical enlistment order is confluent for roster ────

test('WHAT[FINALITY-009] historical enlist order confluent for roster', () => {
  // Two independent historical enlistments on request1. Any interleaving of
  // (enlist A, enlist B) must fold to the same Members and, after reject +
  // request2, the same rosterOf algebra. Enumerate traces explicitly.
  const prefix = [
    mgrEvt(lifeOpenedEvt()),
    mgrEvt(workActivatedEvt()),
    mgrEvt(finalityRequestedEvt(REQ1, 'run-1', 'call-1')),
  ]
  const enlistA = mgrEvt(enlistEvt(REQ1, HIST_A, 0, BAR_A, true))
  const enlistB = mgrEvt(enlistEvt(REQ1, HIST_B, 1, BAR_B, false))
  const suffix = [
    mgrEvt(finalityRejectedEvt(REQ1, HIST_A, BAR_A)),
    mgrEvt(finalityRequestedEvt(REQ2, 'run-2', 'call-2')),
  ]

  const traces = DeterministicEventQueue.interleavings([enlistA], [enlistB])
  assert.equal(traces.length, 2, 'two explicit interleavings: A;B and B;A')

  const rosterViews = []
  const memberViews = []
  for (const mid of traces) {
    const folded = surface.project([...prefix, ...mid, ...suffix])
    assert.equal(folded.ok, true, JSON.stringify(folded.error))
    const life = surface.lifeView(folded.world)
    // Round-1 Members are closed under Rejected request1; standing survives.
    assert.equal(life.enlistedReviewers.length, 2)
    memberViews.push(
      life.enlistedReviewers
        .map(({ sessionId, standing }) => ({
          session: sessionId,
          ordinal: standing.ordinal,
          barriers: standing.barriers.slice().sort(),
        }))
        .sort((a, b) => a.session.localeCompare(b.session)),
    )
    rosterViews.push(sv(folded.world))
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

test('WHAT[FINALITY-016] blessed exactly once: second completion rejected', () => {
  // Trace T3: Open → enlist → Blessed. A second Blessed on the same request
  // must not flip / double-write LastBlessing (fold rejects; projection stable).
  const blessed = surface.project([
    mgrEvt(lifeOpenedEvt()),
    mgrEvt(workActivatedEvt()),
    mgrEvt(finalityRequestedEvt(REQ1, 'run-1', 'call-1')),
    mgrEvt(enlistEvt(REQ1, NEW, 0, BAR1, true)),
    mgrEvt(finalityBlessedEvt(REQ1)),
  ])
  assert.equal(blessed.ok, true, JSON.stringify(blessed.error))
  const once = surface.lifeView(blessed.world)
  assert.equal(once.activeFinality.resolution.kind, 'blessed')
  assert.equal(once.lastBlessing.requestId, 'req-1')

  const again = surface.applyEvents(blessed.world, [mgrEvt(finalityBlessedEvt(REQ1))])
  assert.equal(again.ok, false, 'second FinalityBlessed must be rejected by production fold')
  // Projection from the successful fold remains the sole completion evidence.
  assert.equal(surface.lifeView(blessed.world).activeFinality.resolution.kind, 'blessed')
  assert.equal(surface.lifeView(blessed.world).lastBlessing.requestId, 'req-1')
})

// ── Theorem 4: dropEphemeral preserves durable finality; no second completion

test('WHAT[FINALITY-008] drop ephemeral preserves durable finality facts: no duplicate completion', async () => {
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
  assert.equal(before.ActiveFinality.Resolution.name, 'Blessed')
  const beforeMembers = membersView(before.ActiveFinality)
  assert.deepEqual(beforeMembers, [
    { session: 'ses-new', ordinal: 1, barrier: 'bar-2', isNew: true },
  ])
  assert.equal(mapEntries(before.EnlistedReviewers).length, 2)

  const world2 = await dropEphemeral(world1, { runtime: 'rt_finality_recovered', pid: 5102 })
  const after = currentLife(agentJournal.snapshot(world2.journal))
  assert.equal(after.ActiveFinality.Resolution.name, 'Blessed')
  assert.deepEqual(membersView(after.ActiveFinality), beforeMembers)
  assert.equal(idValue.finalityRequest(after.ActiveFinality.RequestId), 'req-2')
  assert.equal(idValue.finalityRequest(after.LastBlessing.RequestId), 'req-2')
  assert.equal(mapEntries(after.EnlistedReviewers).length, 2)

  // Resume must not accept a second Blessed completion for the same request.
  const duplicate = await agentJournal.appendManagerLifecycle(
    stream.session(MGR),
    finalityBlessed(REQ2).toJSON()[1],
    world2.journal,
  )
  assert.notEqual(duplicate.ok, true, 'duplicate FinalityBlessed after resume must be refused')
  const still = currentLife(agentJournal.snapshot(world2.journal))
  assert.equal(still.ActiveFinality.Resolution.name, 'Blessed')
  assert.deepEqual(membersView(still.ActiveFinality), beforeMembers)

  // Roster algebra still reachable from recovered durable projection:
  // ungraduated hist-a (not in Members) + crash-reentry new slot (IsNew=false).
  const recoveredRoster = slotView(
    finalityCohort.rosterOf(agentJournal.snapshot(world2.journal).AgentProjections, still, still.ActiveFinality),
  )
  assert.deepEqual(recoveredRoster, [
    { agentId: 'finality-new-req-1', session: 'ses-hist-a', ordinal: 0, isNew: false },
    { agentId: 'finality-new-req-2', session: 'ses-new', ordinal: 1, isNew: false },
  ])

  world2.dispose()
})

test('WHAT[FINALITY-009] drop ephemeral preserves open finality roster source', async () => {
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
  assert.equal(beforeLife.ActiveFinality.Resolution.name, 'Open')
  const beforeRoster = slotView(
    finalityCohort.rosterOf(beforeSnap.AgentProjections, beforeLife, beforeLife.ActiveFinality),
  )
  assert.deepEqual(beforeRoster, [
    { agentId: 'finality-new-req-1', session: 'ses-hist-a', ordinal: 0, isNew: false },
    { agentId: 'finality-new-req-2', session: null, ordinal: 1, isNew: true },
  ])

  const world2 = await dropEphemeral(world1, { runtime: 'rt_finality_open_recovered', pid: 5202 })
  const afterSnap = agentJournal.snapshot(world2.journal)
  const afterLife = currentLife(afterSnap)
  assert.equal(afterLife.ActiveFinality.Resolution.name, 'Open')
  assert.equal(idValue.finalityRequest(afterLife.ActiveFinality.RequestId), 'req-2')
  assert.equal(mapEntries(afterLife.ActiveFinality.Members).length, 0)
  assert.deepEqual(
    slotView(finalityCohort.rosterOf(afterSnap.AgentProjections, afterLife, afterLife.ActiveFinality)),
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

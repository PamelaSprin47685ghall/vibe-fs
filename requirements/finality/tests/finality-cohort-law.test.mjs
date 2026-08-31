// FINALITY-009/010/016 cohort laws. Every plain lifecycle event enters the
// registered FinalitySurface one-event fold; the answer is JS-native roster,
// standing, and resolution data. Crash/re-entry is durable history replay.

import assert from 'node:assert/strict'
import test from 'node:test'

const surface = await import('../../../dist/Mission/Manager/FinalitySurface.js')

const MGR = 'mgr'
const LIFE = 'life-1'
const TREE = 'tree-1'
const BLOB = 'blob-1'
const DIGEST = 'd-1'
const REQ1 = 'req-1'
const REQ2 = 'req-2'
const HIST_A = 'ses-hist-a'
const HIST_B = 'ses-hist-b'
const NEW = 'ses-new'
const BAR1 = 'bar-1'
const BAR2 = 'bar-2'
const BAR_A = 'bar-a'
const BAR_B = 'bar-b'

const lifeOpened = () => ({
  kind: 'life-opened',
  sessionId: MGR,
  lifeId: LIFE,
  openingUserMessageId: 'msg-open',
  openingTextRef: BLOB,
  openingTextDigest: DIGEST,
  openingCursorSequence: 1,
})

const workActivated = () => ({
  kind: 'work-activated',
  sessionId: MGR,
  lifeId: LIFE,
  activationPromptKey: 'key-1',
  protectedPrefixEndSequence: 42,
})

const finalityRequested = (requestId, run, call) => ({
  kind: 'finality-requested',
  sessionId: MGR,
  lifeId: LIFE,
  requestId,
  gitTreeHash: TREE,
  lastWordsRef: BLOB,
  lastWordsDigest: DIGEST,
  providerRun: run,
  toolCallId: call,
})

const enlist = (requestId, reviewer, ordinal, barrier, isNew) => ({
  kind: 'finality-reviewer-enlisted',
  sessionId: MGR,
  lifeId: LIFE,
  requestId,
  reviewerSessionId: reviewer,
  reviewerOrdinal: ordinal,
  barrierId: barrier,
  gitTreeHash: TREE,
  isNewReviewer: isNew,
})

const finalityRejected = (requestId, reviewer, barrier) => ({
  kind: 'finality-rejected',
  sessionId: MGR,
  lifeId: LIFE,
  requestId,
  rejectingReviewerSessionId: reviewer,
  barrierId: barrier,
  gitTreeHash: TREE,
  workRecordRef: BLOB,
  workRecordDigest: DIGEST,
})

const finalityBlessed = (requestId) => ({
  kind: 'finality-blessed',
  sessionId: MGR,
  lifeId: LIFE,
  requestId,
  gitTreeHash: TREE,
  workRecordBundleRef: BLOB,
  workRecordBundleDigest: DIGEST,
})

const confirmWitness = (reviewer, barrier) => [
  {
    kind: 'review-barrier-started',
    sessionId: MGR,
    reviewerSessionId: reviewer,
    barrierId: barrier,
    gitTreeHash: TREE,
  },
  {
    kind: 'confirmed-review-witness',
    sessionId: MGR,
    reviewerSessionId: reviewer,
    barrierId: barrier,
    gitTreeHash: TREE,
    challengeResultDigest: `chal-${reviewer}`,
    secondProviderInputDigest: `in-${reviewer}`,
    firstProviderRun: `rev1-${reviewer}`,
    firstToolCallId: `tc1-${reviewer}`,
    secondProviderRun: `rev2-${reviewer}`,
    secondToolCallId: `tc2-${reviewer}`,
  },
]

const project = (events) => {
  let world = surface.emptyWorld()
  for (const event of events) {
    const result = surface.applyEvent(world, event)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    world = result.world
  }
  return world
}

const apply = (world, events) => {
  for (const event of events) {
    const result = surface.applyEvent(world, event)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    world = result.world
  }
  return world
}

const rosterView = (world) =>
  surface.cohortRoster(world).map((slot) => ({
    agentId: slot.agentId,
    session: slot.session,
    ordinal: slot.ordinal,
    isNew: slot.isNew,
  }))

const interleavings = (left, right) => [
  [...left, ...right],
  [...right, ...left],
]

// ── Theorem 1: roster algebra — ungraduated history + exactly one new ────────

test('WHAT[FINALITY-009] roster is ungraduated history plus exactly one new', () => {
  const world = project([
    lifeOpened(),
    workActivated(),
    finalityRequested(REQ1, 'run-1', 'call-1'),
    enlist(REQ1, HIST_A, 0, BAR1, true),
    finalityRejected(REQ1, HIST_A, BAR1),
    finalityRequested(REQ2, 'run-2', 'call-2'),
  ])

  const life = surface.lifeView(world)
  assert.equal(life.activeFinality.resolution.kind, 'open')
  assert.equal(life.activeFinality.members.length, 0)
  assert.deepEqual(rosterView(world), [
    { agentId: 'finality-new-req-1', session: HIST_A, ordinal: 0, isNew: false },
    { agentId: 'finality-new-req-2', session: null, ordinal: 1, isNew: true },
  ])
  assert.deepEqual(rosterView(world), rosterView(world), 'roster calculation is repeatable')
})

test('WHAT[FINALITY-010] graduated reviewer excluded from roster', () => {
  const base = project([
    lifeOpened(),
    workActivated(),
    finalityRequested(REQ1, 'run-1', 'call-1'),
    enlist(REQ1, HIST_A, 0, BAR1, true),
    finalityRejected(REQ1, HIST_A, BAR1),
    finalityRequested(REQ2, 'run-2', 'call-2'),
  ])
  const confirmed = apply(base, confirmWitness(HIST_A, BAR1))

  const standing = surface.lifeView(confirmed).enlistedReviewers.find((entry) => entry.sessionId === HIST_A)
  assert.ok(standing)
  assert.equal(surface.graduatedReviewer(confirmed, HIST_A), true)
  assert.deepEqual(rosterView(confirmed), [
    { agentId: 'finality-new-req-2', session: null, ordinal: 1, isNew: true },
  ])
})

test('WHAT[FINALITY-009] crash reentry reuses already created new slot exactly once', () => {
  const world = project([
    lifeOpened(),
    workActivated(),
    finalityRequested(REQ1, 'run-1', 'call-1'),
    enlist(REQ1, NEW, 0, BAR1, true),
  ])
  assert.deepEqual(rosterView(world), [
    { agentId: 'finality-new-req-1', session: NEW, ordinal: 0, isNew: false },
  ])

  const replay = surface.applyEvent(world, enlist(REQ1, NEW, 0, BAR1, true))
  assert.equal(replay.ok, true, JSON.stringify(replay.error))
  assert.equal(surface.lifeView(replay.world).activeFinality.members.length, 1)
})

// ── Theorem 2: historical enlist order is confluent ─────────────────────────

test('WHAT[FINALITY-009] historical enlist order confluent for roster', () => {
  const prefix = [lifeOpened(), workActivated(), finalityRequested(REQ1, 'run-1', 'call-1')]
  const enlistA = enlist(REQ1, HIST_A, 0, BAR_A, true)
  const enlistB = enlist(REQ1, HIST_B, 1, BAR_B, false)
  const suffix = [finalityRejected(REQ1, HIST_A, BAR_A), finalityRequested(REQ2, 'run-2', 'call-2')]

  const views = interleavings([enlistA], [enlistB]).map((middle) => {
    const world = project([...prefix, ...middle, ...suffix])
    const life = surface.lifeView(world)
    return {
      standing: life.enlistedReviewers
        .map(({ sessionId, standing }) => ({
          session: sessionId,
          ordinal: standing.ordinal,
          barriers: standing.barriers.slice().sort(),
        }))
        .sort((a, b) => a.session.localeCompare(b.session)),
      roster: rosterView(world),
    }
  })

  assert.deepEqual(views[0].standing, views[1].standing)
  assert.deepEqual(views[0].roster, views[1].roster)
  assert.equal(views[0].roster.filter((slot) => slot.isNew).length, 1)
  assert.deepEqual(
    views[0].roster.filter((slot) => !slot.isNew).map((slot) => slot.session).sort(),
    [HIST_A, HIST_B],
  )
})

// ── Theorem 3: terminal completion is exactly-once under fold ───────────────

test('WHAT[FINALITY-016] blessed exactly once: second completion rejected', () => {
  const world = project([
    lifeOpened(),
    workActivated(),
    finalityRequested(REQ1, 'run-1', 'call-1'),
    enlist(REQ1, NEW, 0, BAR1, true),
    finalityBlessed(REQ1),
  ])
  assert.equal(surface.lifeView(world).activeFinality.resolution.kind, 'blessed')
  assert.equal(surface.lifeView(world).lastBlessing.requestId, REQ1)

  const again = surface.applyEvent(world, finalityBlessed(REQ1))
  assert.equal(again.ok, false, 'second FinalityBlessed must be rejected by production fold')
  assert.equal(surface.lifeView(world).activeFinality.resolution.kind, 'blessed')
})

// ── Theorem 4: replay preserves durable finality; no second completion ──────

test('WHAT[FINALITY-008] history replay preserves durable finality facts', () => {
  const history = [
    lifeOpened(),
    workActivated(),
    finalityRequested(REQ1, 'run-1', 'call-1'),
    enlist(REQ1, HIST_A, 0, BAR1, true),
    finalityRejected(REQ1, HIST_A, BAR1),
    finalityRequested(REQ2, 'run-2', 'call-2'),
    enlist(REQ2, NEW, 1, BAR2, true),
    finalityBlessed(REQ2),
  ]
  const before = project(history)
  const after = project(history)
  assert.deepEqual(surface.lifeView(after), surface.lifeView(before))
  assert.deepEqual(
    surface.cohortRosterFromSnapshot(after, LIFE, REQ2),
    [
      { agentId: 'finality-new-req-1', session: HIST_A, ordinal: 0, isNew: false },
      { agentId: 'finality-new-req-2', session: NEW, ordinal: 1, isNew: false },
    ],
  )

  const duplicate = surface.applyEvent(after, finalityBlessed(REQ2))
  assert.equal(duplicate.ok, false, 'replayed history cannot accept a second blessing')
})

test('WHAT[FINALITY-009] replay preserves an open finality roster source', () => {
  const history = [
    lifeOpened(),
    workActivated(),
    finalityRequested(REQ1, 'run-1', 'call-1'),
    enlist(REQ1, HIST_A, 0, BAR1, true),
    finalityRejected(REQ1, HIST_A, BAR1),
    finalityRequested(REQ2, 'run-2', 'call-2'),
  ]
  const before = project(history)
  const expected = rosterView(before)
  const after = project(history)
  assert.equal(surface.lifeView(after).activeFinality.resolution.kind, 'open')
  assert.deepEqual(rosterView(after), expected)
  assert.deepEqual(surface.cohortRosterFromSnapshot(after, LIFE, REQ2), expected)

  const enlisted = surface.applyEvent(after, enlist(REQ2, NEW, 1, BAR2, true))
  assert.equal(enlisted.ok, true, JSON.stringify(enlisted.error))
  assert.deepEqual(surface.lifeView(enlisted.world).activeFinality.members, [
    { sessionId: NEW, ordinal: 1, barrierId: BAR2, isNew: true },
  ])
  const replay = surface.applyEvent(enlisted.world, enlist(REQ2, NEW, 1, BAR2, true))
  assert.equal(replay.ok, true, JSON.stringify(replay.error))
  assert.deepEqual(surface.lifeView(replay.world).activeFinality.members, [
    { sessionId: NEW, ordinal: 1, barrierId: BAR2, isNew: true },
  ])
})

// requirements/finality/tests/manager-finality-disposition.test.mjs
//
// FINALITY-* disposition law: one suicide call interpreted against the durable
// Life (GLORY-040/041/054/055/060/062/065; TODO-010). Pure: every fixture is
// a JS lifecycle event list folded by the production fold inside
// FinalitySurface.project (PR 6 exemplar), then classifyEnding / admitLabor
// decide the ending experience as JS-shaped answers.
//
// The drain *mechanics* (await ConsumableReview, REVISE report delivery) live
// in FinalityTool.execute and are covered by the membrane / magic-todo domain
// suites (REUSE in PROOF.md); this file locks the pure disposition algebra:
//   - no accepted planComplete=true commitment       → continue-planning
//     (TODO-010 zero-checkpoint fail closed, GLORY-039)
//   - completed Life                                  → already-completed
//   - open request, same ToolCallId                   → resume-request
//   - open request, no members yet                    → recover-request-without-reviewers
//   - open request, members, different call           → wait-for-current-request
//   - latest blessing                                 → complete-blessed-life (rest)
//   - otherwise                                       → begin-finality
//   - open request owns labor                         → finality-owns-life
//
// JS-SEMANTIC-SURFACE-002/003/005: the only production entry is the
// registered FinalitySurface; the test never touches Fable unions, dist
// internals, or the fold facade.

import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowed } from '../../../dist/Foundation/RolesSurface.js'

const finality = await import('../../../dist/Mission/Manager/FinalitySurface.js')

const SESSION = 'ses_finality_disposition'
const LIFE = 'life-finality'
const REQ = 'req-1'
const REVIEWER = 'ses-reviewer'
const BARRIER = 'bar-1'

// ── JS lifecycle vocabulary (the only input the test owns) ──────────────────

const lifeOpened = () => ({
  kind: 'life-opened',
  sessionId: SESSION,
  lifeId: LIFE,
  openingUserMessageId: 'msg-open',
  openingTextRef: 'blob-1',
  openingTextDigest: 'digest-1',
  openingCursorSequence: 1,
})

const finalityRequested = (callId = 'call-1', reqId = REQ) => ({
  kind: 'finality-requested',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId: reqId,
  gitTreeHash: 'tree-1',
  lastWordsRef: 'blob-1',
  lastWordsDigest: 'digest-1',
  providerRun: 'run-1',
  toolCallId: callId,
})

const finalityReviewerEnlisted = () => ({
  kind: 'finality-reviewer-enlisted',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId: REQ,
  reviewerSessionId: REVIEWER,
  reviewerOrdinal: 1,
  barrierId: BARRIER,
  gitTreeHash: 'tree-1',
  isNewReviewer: true,
})

const finalityRejected = () => ({
  kind: 'finality-rejected',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId: REQ,
  rejectingReviewerSessionId: REVIEWER,
  barrierId: BARRIER,
  gitTreeHash: 'tree-1',
  workRecordRef: 'blob-1',
  workRecordDigest: 'digest-1',
})

const finalityBlessed = () => ({
  kind: 'finality-blessed',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId: REQ,
  gitTreeHash: 'tree-1',
  workRecordBundleRef: 'blob-1',
  workRecordBundleDigest: 'digest-1',
})

const lifeCompleted = () => ({
  kind: 'life-completed',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId: REQ,
  terminalRef: 'blob-1',
  terminalDigest: 'digest-1',
})

/** Fold a JS event list through the production fold; returns the opaque world. */
const worldOf = (events) => {
  const out = finality.project(events)
  assert.equal(out.ok, true, JSON.stringify(out.error))
  return out.world
}

const classify = (world, callId, hasPlanCommitment) =>
  finality.classifyEnding(world, callId ?? '', hasPlanCommitment)

test('WHAT[FINALITY-001] only the Manager holds ToolPermission.Finality', () => {
  assert.equal(isAllowed('manager', 'Finality'), true)
  for (const role of ['coder', 'inspector', 'devops', 'browser', 'inquiry', 'reviewer', 'orchestrator', 'distiller', 'blogger']) {
    assert.equal(isAllowed(role, 'Finality'), false, `role ${role} must not hold Finality`)
  }
})

test('WHAT[FINALITY-004] no accepted planComplete=true commitment stays at Planning Table', () => {
  const world = worldOf([lifeOpened()])
  assert.deepEqual(classify(world, '', false), { kind: 'continue-planning' })
})

test('WHAT[FINALITY-025] a completed Life replays as AlreadyCompleted, never restarts', () => {
  const world = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed(), lifeCompleted()])
  const archived = finality.archivedLivesView(world)
  assert.equal(archived.length, 1)
  assert.equal(archived[0].completed, true, 'LifeCompleted must archive a completed Life')
  assert.deepEqual(classify(world, 'call-1', true), { kind: 'already-completed' })
})

test('WHAT[FINALITY-003] an open request resumes the same ToolCallId replay', () => {
  const world = worldOf([lifeOpened(), finalityRequested()])
  assert.deepEqual(classify(world, 'call-1', true), { kind: 'resume-request', requestId: 'req-1' })
})

test('WHAT[FINALITY-003] an open request with no enlisted members is recoverable', () => {
  const world = worldOf([lifeOpened(), finalityRequested()])
  assert.deepEqual(classify(world, 'call-2', true), { kind: 'recover-request-without-reviewers', requestId: 'req-1' })
})

test('WHAT[FINALITY-003] a request already in motion waits for the current cohort', () => {
  const world = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted()])
  assert.deepEqual(classify(world, 'call-2', true), { kind: 'wait-for-current-request' })
})

test('WHAT[FINALITY-014] rejection keeps the same Life and a new suicide begins fresh Finality', () => {
  const world = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityRejected()])
  assert.equal(finality.lifeView(world).activeFinality.resolution.kind, 'rejected')
  // Same Life continues: no blessing, no new request — next suicide starts a new cohort.
  assert.deepEqual(classify(world, 'call-2', true), { kind: 'begin-finality' })
})

test('WHAT[FINALITY-026] a rejected request does not block labor: labor may continue', () => {
  const world = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityRejected()])
  assert.equal(finality.admitLabor(world), 'labor-may-continue')
})

test('WHAT[FINALITY-016] a blessing leaves the Life open until the second suicide', () => {
  const world = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()])
  const life = finality.lifeView(world)
  assert.equal(life.completed, false)
  assert.ok(life.lastBlessing != null)
})

test('WHAT[FINALITY-017] the second suicide after a blessing is the rest path', () => {
  const world = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()])
  assert.deepEqual(classify(world, 'call-2', true), { kind: 'complete-blessed-life' })
  // Blessing is resolved, not open: ordinary labor may continue (GLORY-061).
  assert.equal(finality.admitLabor(world), 'labor-may-continue')
})

test('WHAT[FINALITY-018] an open request owns the Life: Manager labor is deferred', () => {
  const world = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted()])
  assert.equal(finality.admitLabor(world), 'finality-owns-life')
})

test('WHAT[FINALITY-026] resolved historical requests do not block labor', () => {
  // Resolved historical requests do not block labor (GLORY-055).
  const world = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityRejected()])
  assert.equal(finality.admitLabor(world), 'labor-may-continue')
})

test('WHAT[FINALITY-022] a new Life inherits no blessing/roster/request and starts fresh Finality', () => {
  const first = finality.project([
    lifeOpened(),
    finalityRequested(),
    finalityReviewerEnlisted(),
    finalityBlessed(),
    lifeCompleted(),
  ])
  assert.equal(first.ok, true, JSON.stringify(first.error))
  assert.equal(finality.archivedLivesView(first.world).length, 1)

  const reopened = finality.applyEvents(first.world, [
    {
      kind: 'life-opened',
      sessionId: SESSION,
      lifeId: 'life-2',
      openingUserMessageId: 'msg-open-2',
      openingTextRef: 'blob-1',
      openingTextDigest: 'digest-1',
      openingCursorSequence: 50,
    },
  ])
  assert.equal(reopened.ok, true, JSON.stringify(reopened.error))
  const current = finality.lifeView(reopened.world)
  assert.equal(current.lifeId, 'life-2')
  assert.equal(current.activeFinality, null)
  assert.equal(current.lastBlessing, null)
  assert.deepEqual(classify(reopened.world, '', true), { kind: 'begin-finality' })
})

test('WHAT[FINALITY-007] no mechanical terminal-todo completeness gate', () => {
  // A Life without any Finality fact set is still BeginFinality — there is no
  // mechanical obligation-completeness gate in front of Finality.
  const world = worldOf([lifeOpened()])
  assert.deepEqual(classify(world, '', true), { kind: 'begin-finality' })
})

test('WHAT[FINALITY-021] disposition never derives from narrative text', () => {
  // The pure dispatcher only reads typed projections — no obligations, no prose
  // inspection; the commitment gate is typed projection evidence, not narrative.
  const world = worldOf([lifeOpened()])
  assert.deepEqual(classify(world, '', true), { kind: 'begin-finality' })
})

test('WHAT[FINALITY-002] finality eligibility is the combination of commitment, request, and experience typing', () => {
  // WHY umbrella (GLORY-037 + TODO-010 + GLORY-003/058): eligibility is not a
  // single flag — the same Life answers differently as the durable facts move
  // through planning → request → blessing, each stage typed by its own fact.
  const planned = worldOf([lifeOpened()])
  assert.deepEqual(classify(planned, '', false), { kind: 'continue-planning' }, 'no accepted plan commitment → planning table')
  assert.deepEqual(classify(planned, '', true), { kind: 'begin-finality' }, 'accepted commitment, no request → begin finality')

  const inFlight = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted()])
  assert.deepEqual(classify(inFlight, 'call-2', true), { kind: 'wait-for-current-request' }, 'open request owns the cohort')

  const blessed = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()])
  assert.deepEqual(classify(blessed, 'call-2', true), { kind: 'complete-blessed-life' }, 'accepted → rest path on the second suicide')
})

test('WHAT[FINALITY-005] the rest-path suicide is a drain, not a new cohort', () => {
  // FINALITY-017's rest path never builds a Finality Reviewer / barrier /
  // request: the second suicide after a blessing must classify as the blessed
  // rest drain, excluding every cohort-creating disposition.
  const blessed = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()])
  const ending = classify(blessed, 'call-2', true)
  assert.deepEqual(ending, { kind: 'complete-blessed-life' })
  for (const disposition of ['begin-finality', 'resume-request', 'recover-request-without-reviewers', 'wait-for-current-request']) {
    assert.notEqual(ending.kind, disposition, `${disposition} must not be reachable on the rest path`)
  }
})

test('WHAT[FINALITY-006] drain outcomes are two-typed: Revision (REVISE) vs Confirmed (PERFECT)', () => {
  // TODO-006: a checkpoint drain awaits the latest ConsumableReview whose
  // verdict is exactly one of two typed outcomes — REVISE returns the canonical
  // work record and keeps the Life going; PERFECT proceeds into Finality.
  assert.deepEqual(finality.reviewerOutcomeKinds(), ['Revision', 'Confirmed'])
  assert.deepEqual(finality.reviewerOutcomeRevision('defect A at src/a.ts'), {
    kind: 'revision',
    workRecord: 'defect A at src/a.ts',
  })
  assert.deepEqual(finality.reviewerOutcomeConfirmed(REVIEWER, BARRIER), {
    kind: 'confirmed',
    reviewerSessionId: REVIEWER,
    barrierId: BARRIER,
  })
})

test('WHAT[FINALITY-015] a blessing keeps the enlisted process-review standing: no dispose', () => {
  // GLORY-055/TODO-008: Blessing releases no process-review session — the
  // enlisted standing survives the blessed request so later checkpoints and
  // the second suicide still have a reviewer to serve.
  const world = worldOf([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()])
  const life = finality.lifeView(world)
  assert.equal(life.completed, false)
  assert.equal(life.enlistedReviewers.length, 1, 'enlisted standing must survive blessing')
  assert.ok(life.lastBlessing != null, 'blessing evidence must be retained')
})

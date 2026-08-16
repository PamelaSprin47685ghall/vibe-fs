// requirements/finality/tests/manager-finality-disposition.test.mjs
//
// FINALITY-* disposition law: one suicide call interpreted against the durable
// Life (GLORY-040/041/054/055/060/062/065; TODO-010). Pure: every fixture is
// folded from production ManagerLifecycle facts (GLORY-010), then
// ManagerFinality.classifyEnding / admitLabor decide the ending experience.
//
// The drain *mechanics* (await ConsumableReview, REVISE report delivery) live
// in FinalityTool.execute and are covered by the membrane / magic-todo domain
// suites (REUSE in PROOF.md); this file locks the pure disposition algebra:
//   - no accepted planComplete=true commitment       → ContinuePlanning
//     (TODO-010 zero-checkpoint fail closed, GLORY-039)
//   - completed Life                                  → AlreadyCompleted
//   - open request, same ToolCallId                   → ResumeRequest
//   - open request, no members yet                    → RecoverRequestWithoutReviewers
//   - open request, members, different call           → WaitForCurrentRequest
//   - latest blessing                                 → CompleteBlessedLife (rest)
//   - otherwise                                       → BeginFinality
//   - open request owns labor                         → FinalityOwnsLife
//
// GLORY-065 Life isolation: a new Life after LifeCompleted inherits no
// blessing / roster / request — the next suicide starts a fresh BeginFinality.

import assert from 'node:assert/strict'
import test from 'node:test'
import { admitLabor, classifyEnding, EndingDisposition, LaborAdmission } from '../../../dist/Mission/Manager/Finality.js'
import { finalityContract, reviewerOutcomeContract } from './support/finality-contract.mjs'
import { isAllowed } from '../../../dist/Foundation/RolesSurface.js'
import {
  blobDigest,
  blobRef,
  envelope,
  finalityRequestId,
  fold,
  gitTreeHash,
  idValue,
  listItems,
  managerLifecycleFact,
  managerLifeId,
  mapEntries,
  physicalUser,
  providerRun,
  reviewBarrierId,
  sessionId,
  stream,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'

const SESSION = sessionId('ses_finality_disposition')
const SESSION_KEY = idValue.session(SESSION)
const LIFE = managerLifeId('life-finality')
const OPENING = physicalUser('msg-open')
const TREE = gitTreeHash('tree-1')
const REQ = finalityRequestId('req-1')
const REVIEWER = sessionId('ses-reviewer')
const BARRIER = reviewBarrierId('bar-1')
const BLOB = blobRef('blob-1')
const DIGEST = blobDigest('digest-1')
const RUN = providerRun('run-1')
const CALL = toolCallId('call-1')

const lifecycleEnv = (fact) => envelope({ stream: stream.session(SESSION), fact })

const lifeOpened = () =>
  managerLifecycleFact('LifeOpened', {
    SessionId: SESSION,
    LifeId: LIFE,
    OpeningUserMessageId: OPENING,
    OpeningTextRef: BLOB,
    OpeningTextDigest: DIGEST,
    OpeningCursorSequence: 1n,
  })

const finalityRequested = (callId = CALL, reqId = REQ) =>
  managerLifecycleFact('FinalityRequested', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: reqId,
    GitTreeHash: TREE,
    LastWordsRef: BLOB,
    LastWordsDigest: DIGEST,
    ProviderRun: RUN,
    ToolCallId: callId,
  })

const finalityReviewerEnlisted = () =>
  managerLifecycleFact('FinalityReviewerEnlisted', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: REQ,
    ReviewerSessionId: REVIEWER,
    ReviewerOrdinal: 1,
    BarrierId: BARRIER,
    GitTreeHash: TREE,
    IsNewReviewer: true,
  })

const finalityRejected = () =>
  managerLifecycleFact('FinalityRejected', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: REQ,
    RejectingReviewerSessionId: REVIEWER,
    BarrierId: BARRIER,
    GitTreeHash: TREE,
    WorkRecordRef: BLOB,
    WorkRecordDigest: DIGEST,
  })

const finalityBlessed = () =>
  managerLifecycleFact('FinalityBlessed', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: REQ,
    GitTreeHash: TREE,
    WorkRecordBundleRef: BLOB,
    WorkRecordBundleDigest: DIGEST,
  })

const lifeCompleted = () =>
  managerLifecycleFact('LifeCompleted', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: REQ,
    TerminalRef: BLOB,
    TerminalDigest: DIGEST,
  })

const foldLife = (facts) => {
  const out = fold.apply(fold.empty, facts.map(lifecycleEnv))
  assert.equal(out.ok, true, out.ok ? '' : JSON.stringify(out.error))
  return fold.session(out.value, SESSION_KEY)?.ManagerLife
}

const caseOf = (value) => value.cases()[value.tag]

test('WHAT[FINALITY-001] only the Manager holds ToolPermission.Finality', () => {
  assert.equal(isAllowed('manager', 'Finality'), true)
  for (const role of ['coder', 'inspector', 'devops', 'browser', 'inquiry', 'reviewer', 'orchestrator', 'distiller', 'blogger']) {
    assert.equal(isAllowed(role, 'Finality'), false, `role ${role} must not hold Finality`)
  }
})

test('WHAT[FINALITY-004] no accepted planComplete=true commitment stays at Planning Table', () => {
  const life = foldLife([lifeOpened()]).CurrentLife
  const ending = classifyEnding(undefined, life, false)
  assert.equal(ending, EndingDisposition.ContinuePlanning)
})

test('WHAT[FINALITY-025] a completed Life replays as AlreadyCompleted, never restarts', () => {
  const archived = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed(), lifeCompleted()])
  assert.equal(archived.CurrentLife, undefined)
  const completedLives = listItems(archived.CompletedLives)
  assert.equal(completedLives.length, 1)
  const done = completedLives[0]
  assert.equal(done.Completed, true, 'LifeCompleted must archive a completed Life')
  assert.equal(classifyEnding(CALL, done, true), EndingDisposition.AlreadyCompleted)
})

test('WHAT[FINALITY-003] an open request resumes the same ToolCallId replay', () => {
  const life = foldLife([lifeOpened(), finalityRequested()]).CurrentLife
  const ending = classifyEnding(CALL, life, true)
  assert.equal(caseOf(ending), 'ResumeRequest')
  // The resumed request is the same durable request, not a new one.
  assert.equal(idValue.finalityRequest(life.ActiveFinality.RequestId), 'req-1')
})

test('WHAT[FINALITY-003] an open request with no enlisted members is recoverable', () => {
  const life = foldLife([lifeOpened(), finalityRequested()]).CurrentLife
  assert.equal(caseOf(classifyEnding(toolCallId('call-2'), life, true)), 'RecoverRequestWithoutReviewers')
})

test('WHAT[FINALITY-003] a request already in motion waits for the current cohort', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted()]).CurrentLife
  assert.equal(classifyEnding(toolCallId('call-2'), life, true), EndingDisposition.WaitForCurrentRequest)
})

test('WHAT[FINALITY-014] rejection keeps the same Life and a new suicide begins fresh Finality', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityRejected()]).CurrentLife
  assert.equal(caseOf(life.ActiveFinality.Resolution), 'Rejected')
  // Same Life continues: no blessing, no new request — next suicide starts a new cohort.
  assert.equal(classifyEnding(toolCallId('call-2'), life, true), EndingDisposition.BeginFinality)
})

test('WHAT[FINALITY-026] a rejected request does not block labor: labor may continue', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityRejected()]).CurrentLife
  assert.equal(admitLabor(life), LaborAdmission.LaborMayContinue)
})

test('WHAT[FINALITY-016] a blessing leaves the Life open until the second suicide', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()]).CurrentLife
  assert.equal(life.Completed, false)
  assert.ok(life.LastBlessing != null)
})

test('WHAT[FINALITY-017] the second suicide after a blessing is the rest path', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()]).CurrentLife
  assert.equal(finalityContract.endingName(classifyEnding(toolCallId('call-2'), life, true)), 'CompleteBlessedLife')
  // Blessing is resolved, not open: ordinary labor may continue (GLORY-061).
  assert.equal(admitLabor(life), LaborAdmission.LaborMayContinue)
})

test('WHAT[FINALITY-018] an open request owns the Life: Manager labor is deferred', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted()]).CurrentLife
  assert.equal(admitLabor(life), LaborAdmission.FinalityOwnsLife)
})

test('WHAT[FINALITY-026] resolved historical requests do not block labor', () => {
  // Resolved historical requests do not block labor (GLORY-055).
  const rejected = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityRejected()]).CurrentLife
  assert.equal(admitLabor(rejected), LaborAdmission.LaborMayContinue)
})

test('WHAT[FINALITY-022] a new Life inherits no blessing/roster/request and starts fresh Finality', () => {
  const first = fold.apply(fold.empty, [lifecycleEnv(lifeOpened()), lifecycleEnv(finalityRequested()), lifecycleEnv(finalityReviewerEnlisted()), lifecycleEnv(finalityBlessed()), lifecycleEnv(lifeCompleted())])
  assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
  assert.equal(fold.session(first.value, SESSION_KEY)?.ManagerLife.CurrentLife, undefined)
  const secondLife = managerLifecycleFact('LifeOpened', {
    SessionId: SESSION,
    LifeId: managerLifeId('life-2'),
    OpeningUserMessageId: physicalUser('msg-open-2'),
    OpeningTextRef: BLOB,
    OpeningTextDigest: DIGEST,
    OpeningCursorSequence: 50n,
  })
  const reopened = fold.apply(first.value, [lifecycleEnv(secondLife)])
  assert.equal(reopened.ok, true, reopened.ok ? '' : JSON.stringify(reopened.error))
  const current = fold.session(reopened.value, SESSION_KEY)?.ManagerLife.CurrentLife
  assert.equal(idValue.managerLife(current.LifeId), 'life-2')
  assert.equal(current.ActiveFinality, undefined)
  assert.equal(current.LastBlessing, undefined)
  assert.equal(classifyEnding(undefined, current, true), EndingDisposition.BeginFinality)
})

test('WHAT[FINALITY-007] no mechanical terminal-todo completeness gate', () => {
  // A Life without any Finality fact set is still BeginFinality — there is no
  // mechanical obligation-completeness gate in front of Finality.
  const life = foldLife([lifeOpened()]).CurrentLife
  assert.equal(classifyEnding(undefined, life, true), EndingDisposition.BeginFinality)
})

test('WHAT[FINALITY-021] disposition never derives from narrative text', () => {
  // The pure dispatcher only reads typed projections — no obligations, no prose
  // inspection; the commitment gate is typed projection evidence, not narrative.
  const life = foldLife([lifeOpened()]).CurrentLife
  assert.equal(classifyEnding(undefined, life, true), EndingDisposition.BeginFinality)
})

test('WHAT[FINALITY-002] finality eligibility is the combination of commitment, request, and experience typing', () => {
  // WHY umbrella (GLORY-037 + TODO-010 + GLORY-003/058): eligibility is not a
  // single flag — the same Life answers differently as the durable facts move
  // through planning → request → blessing, each stage typed by its own fact.
  const planned = foldLife([lifeOpened()]).CurrentLife
  assert.equal(classifyEnding(undefined, planned, false), EndingDisposition.ContinuePlanning, 'no accepted plan commitment → planning table')
  assert.equal(classifyEnding(undefined, planned, true), EndingDisposition.BeginFinality, 'accepted commitment, no request → begin finality')

  const inFlight = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted()]).CurrentLife
  assert.equal(classifyEnding(toolCallId('call-2'), inFlight, true), EndingDisposition.WaitForCurrentRequest, 'open request owns the cohort')

  const blessed = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()]).CurrentLife
  assert.equal(caseOf(classifyEnding(toolCallId('call-2'), blessed, true)), 'CompleteBlessedLife', 'accepted → rest path on the second suicide')
})

test('WHAT[FINALITY-005] the rest-path suicide is a drain, not a new cohort', () => {
  // FINALITY-017's rest path never builds a Finality Reviewer / barrier /
  // request: the second suicide after a blessing must classify as the blessed
  // rest drain, excluding every cohort-creating disposition.
  const blessed = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()]).CurrentLife
  const ending = classifyEnding(toolCallId('call-2'), blessed, true)
  assert.equal(caseOf(ending), 'CompleteBlessedLife')
  for (const disposition of ['BeginFinality', 'ResumeRequest', 'RecoverRequestWithoutReviewers', 'WaitForCurrentRequest']) {
    assert.notEqual(caseOf(ending), disposition, `${disposition} must not be reachable on the rest path`)
  }
})

test('WHAT[FINALITY-006] drain outcomes are two-typed: Revision (REVISE) vs Confirmed (PERFECT)', () => {
  // TODO-006: a checkpoint drain awaits the latest ConsumableReview whose
  // verdict is exactly one of two typed outcomes — REVISE returns the canonical
  // work record and keeps the Life going; PERFECT proceeds into Finality.
  assert.deepEqual(reviewerOutcomeContract.cases(), ['Revision', 'Confirmed'])
  assert.deepEqual(
    reviewerOutcomeContract.revision('defect A at src/a.ts'),
    { name: 'Revision', workRecord: 'defect A at src/a.ts' },
    'REVISE carries the canonical work record',
  )
  assert.equal(reviewerOutcomeContract.confirmed(REVIEWER, BARRIER).name, 'Confirmed')
})

test('WHAT[FINALITY-015] a blessing keeps the enlisted process-review standing: no dispose', () => {
  // GLORY-055/TODO-008: Blessing releases no process-review session — the
  // enlisted standing survives the blessed request so later checkpoints and
  // the second suicide still have a reviewer to serve.
  const blessed = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()]).CurrentLife
  assert.equal(blessed.Completed, false)
  assert.equal(mapEntries(blessed.EnlistedReviewers).length, 1, 'enlisted standing must survive blessing')
  assert.ok(blessed.LastBlessing != null, 'blessing evidence must be retained')
})

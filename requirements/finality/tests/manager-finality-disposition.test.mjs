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
//   - zero TodoWriteAccepted on first unblessed path  → ContinuePlanning
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
import { admitLabor, classifyEnding, EndingDisposition, LaborAdmission } from '../../../dist/Application/Manager/ManagerFinality.js'
import { Role, Roles_isAllowed, ToolPermission } from '../../../dist/Kernel/Roles.js'
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
  physicalUser,
  providerRun,
  reviewBarrierId,
  sessionId,
  stream,
  toolCallId,
} from '../../../tests/unit/support/domain.mjs'

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

test('FINALITY-001 only the Manager holds ToolPermission.Finality', () => {
  assert.equal(Roles_isAllowed(Role.Manager, ToolPermission.Finality), true)
  for (const role of [Role.Coder, Role.Inspector, Role.DevOps, Role.Browser, Role.Inquiry, Role.Reviewer, Role.Orchestrator, Role.Distiller, Role.Blogger]) {
    assert.equal(Roles_isAllowed(role, ToolPermission.Finality), false, `role ${role.cases()[role.tag]} must not hold Finality`)
  }
})

test('FINALITY-005 zero TodoWriteAccepted on the first unblessed path is fail closed', () => {
  const life = foldLife([lifeOpened()]).CurrentLife
  const ending = classifyEnding(undefined, life, false)
  assert.equal(ending, EndingDisposition.ContinuePlanning)
  assert.equal(caseOf(ending), 'ContinuePlanning')
})

test('FINALITY-016 a completed Life replays as AlreadyCompleted, never restarts', () => {
  const archived = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed(), lifeCompleted()])
  assert.equal(archived.CurrentLife, undefined)
  const completedLives = listItems(archived.CompletedLives)
  assert.equal(completedLives.length, 1)
  const done = completedLives[0]
  assert.equal(done.Completed, true, 'LifeCompleted must archive a completed Life')
  assert.equal(caseOf(classifyEnding(CALL, done, true)), 'AlreadyCompleted')
})

test('FINALITY-007/040 an open request resumes the same ToolCallId replay', () => {
  const life = foldLife([lifeOpened(), finalityRequested()]).CurrentLife
  const ending = classifyEnding(CALL, life, true)
  assert.equal(caseOf(ending), 'ResumeRequest')
  // The resumed request is the same durable request, not a new one.
  assert.equal(idValue.finalityRequest(ending.fields[0].RequestId), 'req-1')
})

test('FINALITY-007/057 an open request with no enlisted members is recoverable', () => {
  const life = foldLife([lifeOpened(), finalityRequested()]).CurrentLife
  assert.equal(caseOf(classifyEnding(toolCallId('call-2'), life, true)), 'RecoverRequestWithoutReviewers')
})

test('FINALITY-040 a request already in motion waits for the current cohort', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted()]).CurrentLife
  assert.equal(classifyEnding(toolCallId('call-2'), life, true), EndingDisposition.WaitForCurrentRequest)
})

test('FINALITY-054/055 rejection keeps the same Life and a new suicide begins fresh Finality', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityRejected()]).CurrentLife
  assert.equal(caseOf(life.ActiveFinality.Resolution), 'Rejected')
  // Same Life continues: no blessing, no new request — next suicide starts a new cohort.
  assert.equal(classifyEnding(toolCallId('call-2'), life, true), EndingDisposition.BeginFinality)
  assert.equal(admitLabor(life), LaborAdmission.LaborMayContinue)
})

test('FINALITY-060/062 a blessing leaves the Life open and the second suicide is the rest path', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()]).CurrentLife
  assert.equal(life.Completed, false)
  assert.ok(life.LastBlessing != null)
  assert.equal(caseOf(classifyEnding(toolCallId('call-2'), life, true)), 'CompleteBlessedLife')
  // Blessing is resolved, not open: ordinary labor may continue (GLORY-061).
  assert.equal(admitLabor(life), LaborAdmission.LaborMayContinue)
})

test('FINALITY-040 an open request owns the Life: Manager labor is deferred', () => {
  const life = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted()]).CurrentLife
  assert.equal(admitLabor(life), LaborAdmission.FinalityOwnsLife)
  // Resolved historical requests do not block labor (GLORY-055).
  const rejected = foldLife([lifeOpened(), finalityRequested(), finalityReviewerEnlisted(), finalityRejected()]).CurrentLife
  assert.equal(admitLabor(rejected), LaborAdmission.LaborMayContinue)
})

test('FINALITY-065 a new Life inherits no blessing/roster/request and starts fresh Finality', () => {
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

test('FINALITY-020 disposition never derives from narrative text', () => {
  // The pure dispatcher only reads typed projections; a Life without any
  // Finality fact set but with obligations is still BeginFinality, and the
  // zero-checkpoint gate is a typed count, not prose inspection.
  const life = foldLife([lifeOpened()]).CurrentLife
  assert.equal(classifyEnding(undefined, life, true), EndingDisposition.BeginFinality)
})

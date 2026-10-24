import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { memberBinding } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { permissions as rolePermissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-022-'))
  const commonDir = join(root, '.Herdchestra')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), commonDir)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const framesOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const eventsOf = (text) => framesOf(text).flatMap((frame) => frame.events ?? [])
const openedActiveId = (text) => {
  const folded = life.readProbe(text)
  assert.equal(folded.ok, true, folded.reason)
  assert.notEqual(folded.activeId, '')
  return folded.activeId
}

test('WHAT[work-038] the review target is the next milestone derived from the current stage', () => {
  assert.equal(typeof surface.PlanningSurface.reviewTargetFor, 'function')
  assert.equal(surface.PlanningSurface.reviewTargetFor('S1'), 'S2')
  assert.equal(surface.PlanningSurface.reviewTargetFor('S2'), 'S3')
  assert.equal(surface.PlanningSurface.reviewTargetFor('S3'), 'authorized')
})

test('WHAT[work-038] empty findings grant promotion eligibility; non-empty findings keep the tenure with the gaps returned', async () => {
  assert.equal(typeof surface.PlanningSurface.executeReview, 'function')

  const opened = life.encodeOpening('plan', 'work-22a', '/root', 0n)
  const incumbencyId = openedActiveId(opened)

  // Reviewed is the kernel's own event. Stayed keeps S1 and does not authorise.
  const stayedProbe = life.reviewProbe(opened, incumbencyId, 'Stayed')
  assert.equal(stayedProbe.decision, 'Write')
  assert.equal(stayedProbe.names, 'Reviewed')
  assert.equal(stayedProbe.authorized, false)
  const stayedEvent = JSON.parse(stayedProbe.bytes).events.find((event) => event.type === 'Reviewed')
  assert.equal(stayedEvent.outcome, 'Stayed')
  assert.equal(stayedEvent.stage, 'S1')
  assert.equal(stayedEvent.incumbencyId, incumbencyId)
  const stayedFold = life.readProbe(`${opened}${stayedProbe.bytes}`)
  assert.equal(stayedFold.ok, true)
  assert.equal(stayedFold.next, 'Review', 'remaining obligations keep the tenure in review')
  assert.equal(stayedFold.activeId, incumbencyId)
  assert.equal(stayedFold.authorized, false)

  // Multiple reviews are permitted: later reviews update obligations without repeating promotion.
  const secondProbe = life.reviewProbe(`${opened}${stayedProbe.bytes}`, incumbencyId, 'Promoted')
  assert.ok(secondProbe.decision === 'Write' || secondProbe.names === 'Reviewed' || !/already reviewed/i.test(secondProbe.detail))

  await withStore(async (store, commonDir) => {
    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, commonDir, 'work-22a', 0n)
    assert.equal(opened.ok, true, opened.error)
    const activeId = openedActiveId(life.encodeOpening('plan', 'work-22a', commonDir, 0n))
    // reviewFinishedOnly: draft writes stay locked until the first review.
    assert.equal(surface.PlanningSurface.isReviewFinished(commonDir, 'work-22a', activeId), false)
    const finding = {
      key: 'The tradeoff table covers storage engines',
      value: 'Add a storage engine comparison to the plan',
    }
    // The reviewer is a distinct bound identity, not the incumbency owner:
    // the same call succeeds and keeps the tenure with the gaps returned.
    const stayed = await surface.PlanningSurface.executeReview(commonDir, 'work-22a', activeId, 'run-1:call-1', [finding], null)
    assert.equal(stayed.ok, true, stayed.error)
    assert.equal(stayed.outcome, 'Stayed')
    assert.equal(stayed.target, 'S2', 'the review target is the next milestone, even when the tenure stays')
    assert.equal(stayed.firstReview, true, 'the first review fixes eligibility')
    assert.deepEqual(stayed.gaps, [finding])
    assert.equal(surface.PlanningSurface.isReviewFinished(commonDir, 'work-22a', activeId), true, 'the first review unlocks draft writes')

    // Later reviews apply keyed ledger updates item by item and are never
    // refused: an empty later review changes nothing, and eligibility is never
    // re-fixed (firstReview stays false).
    const later = await surface.PlanningSurface.executeReview(commonDir, 'work-22a', activeId, 'run-2:call-2', [], null)
    assert.equal(later.ok, true, later.error)
    assert.equal(later.firstReview, false, 'later reviews do not fix eligibility again')
    assert.deepEqual(later.gaps, [finding], 'an empty later review leaves the existing obligation untouched')

    // An overwrite changes the value of the same key item by item.
    const revised = { ...finding, value: 'Revised storage comparison' }
    const overwrite = await surface.PlanningSurface.executeReview(commonDir, 'work-22a', activeId, 'run-3:call-3', [revised], null)
    assert.equal(overwrite.ok, true, overwrite.error)
    assert.equal(overwrite.firstReview, false)
    assert.deepEqual(overwrite.gaps, [revised])

    // null deletion: a null value removes the key (no empty-string alias).
    const deletion = { key: 'The tradeoff table covers storage engines', value: null }
    const afterDelete = await surface.PlanningSurface.executeReview(commonDir, 'work-22a', activeId, 'run-4:call-4', [deletion], null)
    assert.equal(afterDelete.ok, true, afterDelete.error)
    assert.deepEqual(afterDelete.gaps, [], 'a null value deletes the key')

    // A->B->A must not replay the earlier A fact (invocation identity, not content).
    const againA = await surface.PlanningSurface.executeReview(commonDir, 'work-22a', activeId, 'run-5:call-5', [finding], null)
    assert.equal(againA.ok, true, againA.error)
    assert.equal(againA.firstReview, false)
    assert.deepEqual(againA.gaps, [finding], 'distinct invocation with earlier content is a new review fact')

    // An exact retry (same fence) replays.
    const retry = await surface.PlanningSurface.executeReview(commonDir, 'work-22a', activeId, 'run-5:call-5', [finding], null)
    assert.equal(retry.ok, true, retry.error)
    assert.deepEqual(retry.gaps, [finding])
  })
})

test('WHAT[work-038] a fresh tenure with empty findings is eligible for promotion', async () => {
  const opened = life.encodeOpening('plan', 'work-22b', '/root', 0n)
  const incumbencyId = openedActiveId(opened)
  const promoted = life.reviewProbe(opened, incumbencyId, 'Promoted')
  assert.equal(promoted.decision, 'Write')
  assert.equal(promoted.names, 'Reviewed')
  assert.equal(promoted.authorized, false, 'S1 eligibility never advances the projection or authorises delivery')
  const promotedEvent = JSON.parse(promoted.bytes).events.find((event) => event.type === 'Reviewed')
  assert.equal(promotedEvent.outcome, 'Promoted')
  assert.equal(promotedEvent.stage, 'S1')

  const folded = life.readProbe(`${opened}${promoted.bytes}`)
  assert.equal(folded.ok, true)
  assert.equal(folded.next, 'Continue')
  assert.equal(folded.activeId, incumbencyId)
  assert.equal(folded.authorized, false)

  await withStore(async (store, commonDir) => {
    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, commonDir, 'work-22b', 0n)
    assert.equal(opened.ok, true, opened.error)
    const activeId = openedActiveId(life.encodeOpening('plan', 'work-22b', commonDir, 0n))
    const promotedLive = await surface.PlanningSurface.executeReview(commonDir, 'work-22b', activeId, 'run-b:call-b', [], null)
    assert.equal(promotedLive.ok, true, promotedLive.error)
    assert.equal(promotedLive.outcome, 'Promoted')
    assert.equal(promotedLive.target, 'S2')
    assert.equal(promotedLive.firstReview, true)
    assert.deepEqual(promotedLive.gaps, [])
  })
})

test('WHAT[work-038] deliver performs the promotion; review only grants eligibility', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const commonDir = await journalSurface.JournalSurface_initializeStateRoot(directory)
    const sessionID = 'ses-plan-review-promotion'
    await acceptAuthorityRoot(runtime, sessionID, 'plan', 'user-plan-root')
    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, directory, sessionID, 0n)
    assert.equal(opened.ok, true, opened.error)

    // S1: the first empty review grants eligibility only; the stage stays S1.
    const s1 = life.inspected(commonDir, sessionID)
    assert.equal(s1.Stage, 'S1')
    const s1Review = await surface.PlanningSurface.executeReview(commonDir, sessionID, s1.ActiveId, 'run-s1:call-s1', [], null)
    assert.equal(s1Review.ok, true, s1Review.error)
    assert.equal(s1Review.outcome, 'Promoted')
    assert.equal(s1Review.firstReview, true)
    assert.equal(life.inspected(commonDir, sessionID).Stage, 'S1', 'review never advances the stage')

    // Deliver performs the S1 -> S2 promotion (same tenure, in place).
    const s1Deliver = await surface.PlanningSurface.executeDeliver(
      commonDir,
      directory,
      sessionID,
      s1.ActiveId,
      'run-rel-1',
      'call-rel-1',
      false,
      async () => ({ ok: false, error: 'unexpected spawn' }),
      1n,
    )
    assert.equal(s1Deliver.ok, true, s1Deliver.error)
    assert.equal(s1Deliver.retirementOutcome, 'Promoted')
    assert.equal(life.inspected(commonDir, sessionID).Stage, 'S2', 'deliver advanced S1 -> S2')
    const resolvedDraftPath = surface.PlanningSurface.resolvePlanPath(directory, sessionID).path
    assert.match(s1Deliver.message, /Promoted: the plan advanced to stage S2/)
    assert.match(s1Deliver.message, /S2/)
    assert.ok(
      s1Deliver.message.includes(resolvedDraftPath),
      'deliver message must contain rendered S2 stage semantics with the resolved draft path',
    )

    // S2 -> S3 promotion.
    const s2 = life.inspected(commonDir, sessionID)
    const s2Review = await surface.PlanningSurface.executeReview(commonDir, sessionID, s2.ActiveId, 'run-s2:call-s2', [], null)
    assert.equal(s2Review.ok, true, s2Review.error)
    const s2Deliver = await surface.PlanningSurface.executeDeliver(
      commonDir,
      directory,
      sessionID,
      s2.ActiveId,
      'run-rel-2',
      'call-rel-2',
      false,
      async () => ({ ok: false, error: 'unexpected spawn' }),
      2n,
    )
    assert.equal(s2Deliver.ok, true, s2Deliver.error)
    assert.equal(s2Deliver.retirementOutcome, 'Promoted')
    assert.equal(life.inspected(commonDir, sessionID).Stage, 'S3', 'deliver advanced S2 -> S3')

    // S3 -> authorized: the S3 deliver grants the delivery right in place.
    const s3 = life.inspected(commonDir, sessionID)
    const s3Review = await surface.PlanningSurface.executeReview(commonDir, sessionID, s3.ActiveId, 'run-s3:call-s3', [], null)
    assert.equal(s3Review.ok, true, s3Review.error)
    const s3Deliver = await surface.PlanningSurface.executeDeliver(
      commonDir,
      directory,
      sessionID,
      s3.ActiveId,
      'run-rel-3',
      'call-rel-3',
      false,
      async () => ({ ok: false, error: 'unexpected spawn' }),
      3n,
    )
    assert.equal(s3Deliver.ok, true, s3Deliver.error)
    assert.equal(s3Deliver.retirementOutcome, 'Authorized')
  })
})

test('WHAT[work-038] review admission admits both Manager (ReviewAssessment) and Plan (Review) roles, now as a programming member', () => {
  assert.equal(rolePredicate('review', 'plan'), true)
  assert.equal(rolePredicate('review', 'manager'), true)
  for (const role of ['engineer', 'devops', 'orchestrator']) {
    assert.equal(rolePredicate('review', role), false, `${role} must be denied admission to review`)
  }

  // The review verb is a programming-surface member (`this.review`), not a
  // standalone host tool.
  assert.equal(memberBinding('Manager', rolePermissions('manager'), 'review'), 'review')
  assert.equal(memberBinding('Plan', rolePermissions('plan'), 'review'), 'review')
  assert.equal(memberBinding('Orchestrator', rolePermissions('orchestrator'), 'review'), undefined)
})

test('WHAT[work-038] a Manager program invokes this.review and updates the durable keyed ledger', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const commonDir = await journalSurface.JournalSurface_initializeStateRoot(directory)
    const sessionID = `ses-mgr-review-member-${Math.random().toString(36).slice(2, 8)}`
    await acceptAuthorityRoot(runtime, sessionID, 'manager', `root-${sessionID}`)
    await openIncumbency(runtime, sessionID, undefined, 'S4', 'owner')

    const runReview = (findings, callID, messageID) =>
      hooks.tool['todowrite-for-manager'].execute(
        {
          name: `Manager review ${callID}`,
          planB: 'The review is a durable write; wait for its receipt before assessing.',
          program: `class Js extends JsProgram { async run() { return await this.review(${JSON.stringify(findings)}); } }`
        },
        { sessionID, agent: 'manager', callID, messageID },
      )

    // First review fixes eligibility: non-empty findings stay, ledger records the key.
    const first = await runReview([{ key: 'tradeoff-table', value: 'Add a storage comparison' }], 'call-mgr-review-1', 'msg-mgr-review-1')
    assert.match(String(first), /outcome = "Stayed"/, 'a non-empty first review stays')
    assert.match(String(first), /firstReview = true/, 'the first review fixes eligibility')

    let inspected = life.inspected(commonDir, sessionID)
    assert.equal(inspected.ReviewOutcome, 'Stayed')
    assert.deepEqual(inspected.Obligations, [{ key: 'tradeoff-table', value: 'Add a storage comparison' }])

    // Multiple reviews are distinct facts: overwrite the same key under a new call.
    await runReview([{ key: 'tradeoff-table', value: 'Revised comparison' }], 'call-mgr-review-2', 'msg-mgr-review-2')
    inspected = life.inspected(commonDir, sessionID)
    assert.deepEqual(inspected.Obligations, [{ key: 'tradeoff-table', value: 'Revised comparison' }])

    // null deletes the key (no empty-string alias).
    await runReview([{ key: 'tradeoff-table', value: null }], 'call-mgr-review-3', 'msg-mgr-review-3')
    inspected = life.inspected(commonDir, sessionID)
    assert.deepEqual(inspected.Obligations, [])

    // A->B->A under a new call is a new fact, not a replay of the earlier A.
    await runReview([{ key: 'tradeoff-table', value: 'Add a storage comparison' }], 'call-mgr-review-4', 'msg-mgr-review-4')
    inspected = life.inspected(commonDir, sessionID)
    assert.deepEqual(inspected.Obligations, [{ key: 'tradeoff-table', value: 'Add a storage comparison' }])

    // Failure data: an invalid findings argument is refused as a data envelope.
    const invalid = await runReview('not-an-array', 'call-mgr-review-5', 'msg-mgr-review-5')
    assert.match(String(invalid), /code = "tool\/review\/invalid-findings"/, 'invalid findings refuse with a stable code')
  })
})

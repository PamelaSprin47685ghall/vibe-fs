import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-022-'))
  const commonDir = join(root, '.git')
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

test('WHAT[work-038] empty findings promote the tenure; non-empty findings keep it with the gaps returned', async () => {
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
  assert.equal(stayedFold.next, 'Continue')
  assert.equal(stayedFold.activeId, incumbencyId)
  assert.equal(stayedFold.authorized, false)

  // The same incumbency cannot review a second time: the kernel refuses.
  const secondProbe = life.reviewProbe(`${opened}${stayedProbe.bytes}`, incumbencyId, 'Promoted')
  assert.equal(secondProbe.decision, 'Refuse')
  assert.match(secondProbe.detail, /already reviewed/i)

  await withStore(async (store, commonDir) => {
    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, commonDir, 'work-22a', 0n)
    assert.equal(opened.ok, true, opened.error)
    const activeId = openedActiveId(life.encodeOpening('plan', 'work-22a', commonDir, 0n))
    const finding = {
      acceptance_criteria: 'The tradeoff table covers storage engines',
      work_plan: 'Add a storage engine comparison to the plan',
    }
    // The reviewer is a distinct bound identity, not the incumbency owner:
    // the same call succeeds and keeps the tenure with the gaps returned.
    const stayed = await surface.PlanningSurface.executeReview(commonDir, 'work-22a', activeId, [finding], null)
    assert.equal(stayed.ok, true, stayed.error)
    assert.equal(stayed.outcome, 'Stayed')
    assert.deepEqual(stayed.gaps, [finding])
  })
})

test('WHAT[work-038] a fresh tenure with empty findings is promoted', async () => {
  const opened = life.encodeOpening('plan', 'work-22b', '/root', 0n)
  const incumbencyId = openedActiveId(opened)
  const promoted = life.reviewProbe(opened, incumbencyId, 'Promoted')
  assert.equal(promoted.decision, 'Write')
  assert.equal(promoted.names, 'Reviewed')
  assert.equal(promoted.authorized, false, 'S1 Promoted advances the projection; it does not authorise delivery')
  const promotedEvent = JSON.parse(promoted.bytes).events.find((event) => event.type === 'Reviewed')
  assert.equal(promotedEvent.outcome, 'Promoted')
  assert.equal(promotedEvent.stage, 'S1')

  const folded = life.readProbe(`${opened}${promoted.bytes}`)
  assert.equal(folded.ok, true)
  assert.equal(folded.next, 'Continue')
  assert.equal(folded.activeId, incumbencyId)
  assert.equal(folded.authorized, false)
  const finalPromotion = life.finalPromotionProbe(opened)
  assert.equal(finalPromotion.authorized, true)
  assert.equal(finalPromotion.detail, 'S3', 'only the S3 promotion authorises delivery')

  await withStore(async (store, commonDir) => {
    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, commonDir, 'work-22b', 0n)
    assert.equal(opened.ok, true, opened.error)
    const activeId = openedActiveId(life.encodeOpening('plan', 'work-22b', commonDir, 0n))
    const promotedLive = await surface.PlanningSurface.executeReview(commonDir, 'work-22b', activeId, [], null)
    assert.equal(promotedLive.ok, true, promotedLive.error)
    assert.equal(promotedLive.outcome, 'Promoted')
    assert.deepEqual(promotedLive.gaps, [])
  })
})

test('WHAT[work-038] ReviewShell admission admits both Manager (ReviewAssessment) and Plan (Review) roles', () => {
  assert.equal(rolePredicate('review', 'plan'), true)
  assert.equal(rolePredicate('review', 'manager'), true)
  for (const role of ['engineer', 'devops', 'orchestrator', 'blogger']) {
    assert.equal(rolePredicate('review', role), false, `${role} must be denied admission to review`)
  }
})

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
  mkdirSync(join(root, '.git'), { recursive: true })
  try {
    await body(workspace.acquire(join(root, '.git')), root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const eventsOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const incumbencyIdOf = (text) => eventsOf(text).find((event) => event.event_type === 'IncumbencyOpened').payload.incumbencyId

test('WHAT[work-038] the review target is the next milestone derived from the current stage', () => {
  assert.equal(typeof surface.PlanningSurface.reviewTargetFor, 'function')
  assert.equal(surface.PlanningSurface.reviewTargetFor('S1'), 'S2')
  assert.equal(surface.PlanningSurface.reviewTargetFor('S2'), 'S3')
  assert.equal(surface.PlanningSurface.reviewTargetFor('S3'), 'authorized')
})

test('WHAT[work-038] empty findings promote the tenure; non-empty findings keep it with the gaps returned', async () => {
  assert.equal(typeof surface.PlanningSurface.executeReview, 'function')

  const opened = life.encodeOpening('plan', 'work-22a', '/root', 0n)
  const incumbencyId = incumbencyIdOf(opened)

  // Reviewed is the kernel's own event. Stayed keeps S1 and does not authorise.
  const stayedProbe = life.reviewProbe(opened, incumbencyId, 'Stayed')
  assert.equal(stayedProbe.decision, 'Write')
  assert.equal(stayedProbe.names, 'Reviewed')
  assert.equal(stayedProbe.authorized, false)
  const stayedEvent = JSON.parse(stayedProbe.bytes)
  assert.equal(stayedEvent.event_type, 'Reviewed')
  assert.equal(stayedEvent.payload.outcome, 'Stayed')
  assert.equal(stayedEvent.payload.stage, 'S1')
  assert.equal(stayedEvent.payload.incumbencyId, incumbencyId)
  const stayedFold = life.readProbe(`${opened}${stayedProbe.bytes}`)
  assert.equal(stayedFold.position, 'ActiveRebound')
  assert.equal(stayedFold.authorized, false)

  // The same incumbency cannot review a second time: the kernel refuses.
  const secondProbe = life.reviewProbe(`${opened}${stayedProbe.bytes}`, incumbencyId, 'Promoted')
  assert.equal(secondProbe.decision, 'Refuse')
  assert.match(secondProbe.detail, /already reviewed/i)

  await withStore(async (store) => {
    const seeded = await life.seedOpen(store, 'plan', 'work-22a', '/root', 0n)
    assert.equal(seeded.ok, true, seeded.error)
    const activeId = store.TryCurrent('Lifecycle').get('work-22a').Active.Id
    const batches = []
    const append = store.Append.bind(store)
    store.Append = async (envelopes) => {
      const batch = []
      for (const envelope of envelopes) batch.push(envelope)
      batches.push(batch)
      return append(envelopes)
    }

    const findings = [
      {
        acceptance_criteria: 'The tradeoff table covers storage engines',
        work_plan: 'Add a storage engine comparison to the plan',
      },
    ]
    // executeReview still resolves its once-only gate through the retired Plan
    // projection, so a lifecycle-only work cannot reach LifecycleKernel.review
    // from that entry. The kernel path is seedReview, which commits Reviewed.
    const stayed = await life.seedReview(store, 'work-22a', 'Stayed')
    assert.equal(stayed.ok, true, stayed.error)
    assert.equal(stayed.reviewed, true)
    assert.deepEqual(batches.map((batch) => batch.map((envelope) => envelope.EventType)), [['Reviewed']])
    assert.equal(batches[0][0].Payload.outcome, 'Stayed')
    assert.equal(batches[0][0].Payload.stage, 'S1')
    assert.equal(batches[0][0].Payload.incumbencyId, activeId)

    const folded = store.TryCurrent('Lifecycle').get('work-22a')
    assert.equal(String(folded.Active.OpeningStage), 'S1')
    assert.equal(String(folded.Active.Review.Outcome), 'Stayed')
    assert.equal(String(folded.Active.Review.Stage), 'S1')

    const refused = await surface.PlanningSurface.executeReview(store, 'work-22a', activeId, findings, null)
    assert.equal(refused.ok, false)
    assert.match(refused.error, /already|once|submitted|no active incumbency/i)
    assert.equal(batches.length, 1, 'a second review writes nothing')

    const again = await life.seedReview(store, 'work-22a', 'Promoted')
    assert.equal(again.ok, false)
    assert.match(again.error, /already reviewed/i)
    assert.equal(batches.length, 1)
  })
})

test('WHAT[work-038] a fresh tenure with empty findings is promoted', async () => {
  const opened = life.encodeOpening('plan', 'work-22b', '/root', 0n)
  const incumbencyId = incumbencyIdOf(opened)
  const promoted = life.reviewProbe(opened, incumbencyId, 'Promoted')
  assert.equal(promoted.decision, 'Write')
  assert.equal(promoted.names, 'Reviewed')
  assert.equal(promoted.authorized, false, 'S1 Promoted advances the projection; it does not authorise delivery')
  assert.equal(JSON.parse(promoted.bytes).payload.outcome, 'Promoted')
  assert.equal(JSON.parse(promoted.bytes).payload.stage, 'S1')

  const folded = life.readProbe(`${opened}${promoted.bytes}`)
  assert.equal(folded.position, 'ActiveRebound')
  assert.equal(folded.authorized, false)
  const finalPromotion = life.finalPromotionProbe(opened)
  assert.equal(finalPromotion.authorized, true)
  assert.equal(finalPromotion.detail, 'S3', 'only the S3 promotion authorises delivery')

  await withStore(async (store) => {
    const seeded = await life.seedOpen(store, 'plan', 'work-22b', '/root', 0n)
    assert.equal(seeded.ok, true, seeded.error)
    const before = store.TryCurrent('Lifecycle').get('work-22b')
    const batches = []
    const append = store.Append.bind(store)
    store.Append = async (envelopes) => {
      const batch = []
      for (const envelope of envelopes) batch.push(envelope)
      batches.push(batch)
      return append(envelopes)
    }
    const result = await life.seedReview(store, 'work-22b', 'Promoted')
    assert.equal(result.ok, true, result.error)
    assert.deepEqual(batches[0].map((envelope) => envelope.EventType), ['Reviewed'])
    assert.equal(batches[0][0].Payload.outcome, 'Promoted')
    assert.equal(batches[0][0].Payload.incumbencyId, before.Active.Id)
    const after = store.TryCurrent('Lifecycle').get('work-22b')
    assert.equal(after.Active.Id, before.Active.Id, 'promotion keeps the same tenure')
    assert.equal(String(after.Active.OpeningStage), 'S1', 'the opening stage is unchanged; the projection advances')
    assert.equal(String(after.Active.Review.Outcome), 'Promoted')
    assert.equal(String(after.Active.Review.Stage), 'S1')
  })
})

test('WHAT[work-038] ReviewShell admission admits both Manager (ReviewAssessment) and Plan (Review) roles', () => {
  assert.equal(rolePredicate('review', 'plan'), true)
  assert.equal(rolePredicate('review', 'manager'), true)
  for (const role of ['engineer', 'devops', 'orchestrator', 'blogger']) {
    assert.equal(rolePredicate('review', role), false, `${role} must be denied admission to review`)
  }
})

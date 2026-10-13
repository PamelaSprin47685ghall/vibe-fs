import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, realpathSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as planning from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const eventsOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const incumbencyIdOf = (text) =>
  [...eventsOf(text)].reverse().find((event) => event.event_type === 'IncumbencyOpened').payload.incumbencyId

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-work-035-'))
  execFileSync('git', ['init', '--quiet', root])
  try {
    await body(workspace.acquire(realpathSync(join(root, '.git'))), root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const climb = (workId, relays) => {
  let text = surface.encodeOpening('plan', workId, '/root', 0n)
  for (let step = 0; step < relays; step += 1) {
    const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
    const relayed = surface.deliverProbe(`${text}${reviewed.bytes}`, `run-${step}`, `call-${step}`, BigInt((step + 1) * 10))
    text = `${text}${reviewed.bytes}${relayed.bytes}`
  }
  return text
}

test('WHAT[work-035] a delivered document recovers DeliveredIdempotent and never ActiveRebound', () => {
  let text = climb('work-rec-1', 2)
  const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  text = `${text}${reviewed.bytes}`
  const delivered = surface.deliverStateProbe(text, 'run-final', 'call-final', 30n)
  assert.equal(delivered.decision, 'Write', delivered.detail)
  text = `${text}${delivered.bytes}`

  const read = surface.readProbe(text)
  assert.equal(read.position, 'DeliveredIdempotent')
  assert.notEqual(read.position, 'ActiveRebound')
  assert.equal(read.next, 'StopDelivered')
  const view = surface.documentView(text)
  assert.equal(view.delivered, true)
  assert.equal(view.active, false)
  assert.equal(view.digest, 'sha256-receipt')
  assert.equal(view.path, 'plan.md')
  assert.equal(view.projection, 'S3')
})

test('WHAT[work-035] an active work rebinds the incumbency and stage; draft text does not choose the stage', async () => {
  await withStore(async (store, root) => {
    const opened = await surface.seedOpen(store, 'plan', 'work-rec-2', root, 7n)
    assert.equal(opened.ok, true, opened.error)
    const reviewed = await surface.seedReview(store, 'work-rec-2', 'Promoted')
    assert.equal(reviewed.ok, true, reviewed.error)
    const relayed = await surface.seedDeliver(store, 'work-rec-2', 'run-2', 'call-2', 11n)
    assert.equal(relayed.ok, true, relayed.error)

    const written = planning.PlanningSurface.rewritePlanAtomic(root, 'work-rec-2', '# Stage S3 - Final Deliver - Finished Work')
    assert.equal(written.ok, true, written.error)
    const stored = surface.workView(store, 'work-rec-2')
    assert.equal(stored.found, true)
    assert.equal(stored.active, true)
    assert.equal(stored.sequence, 'S1,S2,S3')
    assert.equal(stored.delivered, false)

    const bytes = surface.encodeOpening('plan', 'work-rec-2', root, 7n)
    const promoted = surface.reviewProbe(bytes, incumbencyIdOf(bytes), 'Promoted')
    const successor = surface.deliverProbe(`${bytes}${promoted.bytes}`, 'run-2', 'call-2', 11n)
    const folded = surface.readProbe(`${bytes}${promoted.bytes}${successor.bytes}`)
    assert.equal(folded.position, 'ActiveRebound')
    const view = surface.documentView(`${bytes}${promoted.bytes}${successor.bytes}`)
    assert.equal(view.activeId.length > 0, true)
    assert.equal(view.openingStage, 'S2', 'recovery reads the folded stage, not the draft text')
    assert.equal(view.authorized, false)
    assert.equal(planning.PlanningSurface.readPlan(root, 'work-rec-2').ok, true)
  })
})

test('WHAT[work-035] a missing draft stays ActiveRebound and is not read as delivered', async () => {
  await withStore(async (store, root) => {
    const opened = await surface.seedOpen(store, 'plan', 'work-rec-3', root, 1n)
    assert.equal(opened.ok, true, opened.error)
    const missing = planning.PlanningSurface.readPlan(root, 'work-rec-3')
    assert.equal(missing.ok, false)
    const stored = surface.workView(store, 'work-rec-3')
    assert.equal(stored.active, true)
    assert.equal(stored.delivered, false)
    const folded = surface.readProbe(surface.encodeOpening('plan', 'work-rec-3', root, 1n))
    assert.equal(folded.position, 'ActiveRebound')
    assert.notEqual(folded.position, 'DeliveredIdempotent')
    assert.equal(surface.documentView(surface.encodeOpening('plan', 'work-rec-3', root, 1n)).openingStage, 'S1')
  })
})

test('WHAT[work-035] a missing work recovers Nothing and does not guess', async () => {
  await withStore(async (store) => {
    const missing = surface.workView(store, 'work-rec-4')
    assert.equal(missing.found, false)
    assert.equal(missing.active, false)
    assert.equal(missing.delivered, false)
    assert.equal(missing.sequence, '')
  })
  const rejected = surface.readProbe('\n')
  assert.equal(rejected.position, 'Rejected', 'a newline with no WorkOpened is not a guessed opening')
  assert.notEqual(rejected.position, 'ActiveRebound')
  assert.notEqual(rejected.position, 'DeliveredIdempotent')
})

test('WHAT[work-035] a delivered document that also names an active incumbency is Conflict', () => {
  let text = climb('work-rec-5', 2)
  const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  const delivered = surface.deliverStateProbe(`${text}${reviewed.bytes}`, 'run-5', 'call-5', 30n)
  text = `${text}${reviewed.bytes}${delivered.bytes}`
  assert.equal(surface.readProbe(text).position, 'DeliveredIdempotent')

  const forged = `${text}${surface.encodeOpening('plan', 'work-rec-5', '/root', 40n)}`
  const conflict = surface.readProbe(forged)
  assert.equal(conflict.position, 'Rejected')
  assert.notEqual(conflict.position, 'DeliveredIdempotent')
  assert.notEqual(conflict.position, 'ActiveRebound')
})

test('WHAT[work-035] recovery effects keep DeliveredIdempotent, ActiveRebound and omit Nothing', async () => {
  const effects = []
  let delivered = climb('work-batch-deliv', 2)
  const reviewed = surface.reviewProbe(delivered, incumbencyIdOf(delivered), 'Promoted')
  const receipt = surface.deliverStateProbe(`${delivered}${reviewed.bytes}`, 'run-b', 'call-b', 30n)
  delivered = `${delivered}${reviewed.bytes}${receipt.bytes}`
  effects.push(surface.readProbe(delivered))
  effects.push(surface.readProbe(climb('work-batch-active', 2)))
  assert.equal(effects.length, 2)
  assert.equal(effects[0].position, 'DeliveredIdempotent')
  assert.equal(effects[1].position, 'ActiveRebound')
  assert.equal(surface.documentView(delivered).workId, 'work-batch-deliv')
  assert.equal(surface.documentView(climb('work-batch-active', 2)).workId, 'work-batch-active')

  await withStore(async (store) => {
    const missing = surface.workView(store, '')
    assert.equal(missing.found, false)
  })
})

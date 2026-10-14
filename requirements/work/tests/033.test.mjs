import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, realpathSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { DatabaseSync } from 'node:sqlite'
import test from 'node:test'
import { SqliteJournal } from 'wanxiangli/eventstore'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as planning from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const framesOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const eventsOf = (text) => framesOf(text).flatMap((frame) => frame.events ?? [])
const incumbencyIdOf = (text) => [...eventsOf(text)].reverse().find((event) => event.type === 'IncumbencyOpened').incumbencyId
const sorted = (keys) => [...keys].sort((left, right) => (left < right ? -1 : left > right ? 1 : 0))

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-033-'))
  execFileSync('git', ['init', '--quiet', root])
  try {
    await body(workspace.acquire(realpathSync(join(root, '.git'))), root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

test('WHAT[work-033] lifecycle payload keys are ordered by Unicode code point', () => {
  const opened = surface.encodeOpening('plan', 'work-1', '/repo/root', 42n)
  const [workOpened, incumbencyOpened] = eventsOf(opened)
  assert.deepEqual(Object.keys(workOpened), sorted(Object.keys(workOpened)))
  assert.deepEqual(Object.keys(workOpened), ['authorityInitial', 'root', 'sequence', 'type', 'workId'])
  assert.deepEqual(Object.keys(incumbencyOpened), ['incumbencyId', 'openingCursor', 'ownerId', 'stage', 'type', 'workId'])
  assert.equal(incumbencyOpened.openingCursor, 42)

  const reviewed = surface.reviewProbe(opened, incumbencyIdOf(opened), 'Promoted')
  const review = eventsOf(reviewed.bytes)[0]
  assert.deepEqual(Object.keys(review), ['incumbencyId', 'outcome', 'reviewerId', 'stage', 'type'])

  const relayed = surface.deliverProbe(`${opened}${reviewed.bytes}`, 'run-1', 'call-1', 100n)
  const [retired, successor] = eventsOf(relayed.bytes)
  assert.deepEqual(Object.keys(retired), sorted(Object.keys(retired)))
  assert.equal(retired.outcome, 'Relayed')
  assert.equal(retired.retirementCursor, 100)
  assert.ok(Object.keys(retired).includes('incumbencyId'))
  assert.ok(Object.keys(retired).includes('retirementId'))
  assert.deepEqual(Object.keys(successor), ['incumbencyId', 'openingCursor', 'ownerId', 'stage', 'type', 'workId'])

  const authorized = surface.finalPromotionProbe(opened)
  assert.equal(authorized.authorized, true)

  const full = (() => {
    let text = opened
    for (let step = 0; step < 2; step += 1) {
      const reviewStep = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
      const relayStep = surface.deliverProbe(`${text}${reviewStep.bytes}`, `run-${step}`, `call-${step}`, BigInt(step + 1))
      text = `${text}${reviewStep.bytes}${relayStep.bytes}`
    }
    const last = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
    const planRoot = mkdtempSync(join(tmpdir(), 'wxs-work-033-keys-'))
    let receipt
    try {
      const written = planning.PlanningSurface.rewritePlanAtomic(planRoot, 'work-1', '# plan\n')
      assert.equal(written.ok, true, written.error)
      const selected = planning.PlanningSurface.readAndValidatePlan(planRoot, 'work-1')
      assert.equal(selected.ok, true, selected.error)
      receipt = surface.deliverStateProbe(`${text}${last.bytes}`, 'run-final', 'call-final', 30n, { digest: selected.digest, path: selected.path })
    } finally {
      rmSync(planRoot, { recursive: true, force: true })
    }
    const reopened = surface.reopenProbe(`${text}${last.bytes}${receipt.bytes}`, incumbencyIdOf(text))
    return { receipt, reopened }
  })()
  const receipt = eventsOf(full.receipt.bytes).find((event) => event.type === 'IncumbencyRetired')
  for (const key of ['digest', 'execSessionId', 'incumbencyId', 'outcome', 'path', 'retirementCursor']) {
    assert.equal(Object.keys(receipt).includes(key), true, key)
  }
  const reopened = eventsOf(full.reopened.bytes).find((event) => event.type === 'Reopened')
  assert.deepEqual(Object.keys(reopened), sorted(Object.keys(reopened)))
  assert.deepEqual(Object.keys(reopened), ['retirementId', 'type'])
})

test('WHAT[work-033] canonical lines round-trip and keep int64 cursor precision', () => {
  const large = 9007199254740995n
  const opened = surface.encodeOpening('plan', 'work-1', '/repo/root', large)
  assert.equal(opened.endsWith('\n'), true, 'a canonical lifecycle line ends with LF')
  const decoded = surface.decodeDocument(opened)
  assert.equal(decoded.ok, true, decoded.fault)
  assert.equal(decoded.names, 'WorkOpened,IncumbencyOpened')
  const openedEvent = eventsOf(opened)[1]
  assert.equal(openedEvent.openingCursor, '9007199254740995')
  assert.equal(BigInt(openedEvent.openingCursor), large)
  assert.equal(surface.documentView(opened).openingCursor, large)

  const truncated = opened.replace(/\n$/, '')
  assert.equal(truncated.endsWith('\n'), false)
})

test('WHAT[work-033] real bytes fold through the codec and the live store', async () => {
  const opened = surface.encodeOpening('plan', 'work-demo', '/workspace', 0n)
  let text = opened
  for (let step = 0; step < 2; step += 1) {
    const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
    const relayed = surface.deliverProbe(`${text}${reviewed.bytes}`, `run-${step}`, `call-${step}`, BigInt((step + 1) * 10))
    text = `${text}${reviewed.bytes}${relayed.bytes}`
  }
  const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  text = `${text}${reviewed.bytes}`
  const planRoot = mkdtempSync(join(tmpdir(), 'wxs-work-033-plan-'))
  let delivered
  let selected
  try {
    const written = planning.PlanningSurface.rewritePlanAtomic(planRoot, 'work-demo', '# plan\n')
    assert.equal(written.ok, true, written.error)
    selected = planning.PlanningSurface.readAndValidatePlan(planRoot, 'work-demo')
    assert.equal(selected.ok, true, selected.error)
    delivered = surface.deliverStateProbe(text, 'run-demo', 'call-demo', 30n, { digest: selected.digest, path: selected.path })
    assert.equal(delivered.decision, 'Write', delivered.detail)
  } finally {
    rmSync(planRoot, { recursive: true, force: true })
  }
  text = `${text}${delivered.bytes}`
  assert.equal(surface.documentView(text).digest, selected.digest)

  const folded = surface.readProbe(text)
  assert.equal(folded.ok, true)
  assert.equal(folded.next, 'Done')
  assert.equal(folded.activeId, '')
  const view = surface.documentView(text)
  assert.equal(view.found, true)
  assert.equal(view.workId, 'work-demo')
  assert.equal(view.retiredCount, 3)
  assert.equal(view.delivered, true)
  assert.equal(view.digest.length, 64)
  assert.equal(view.projection, 'S3')

  await withStore(async (store) => {
    const seeded = await surface.seedOpen(store, 'plan', 'work-demo', '/workspace', 0n)
    assert.equal(seeded.ok, true, seeded.error)
    const bound = await surface.bindDevOps(store, 'work-demo', 'devops-fixed', null)
    assert.equal(bound.ok, true, bound.error)
    for (let step = 0; step < 2; step += 1) {
      const review = await surface.seedReview(store, 'work-demo', 'Promoted')
      assert.equal(review.ok, true, review.error)
      const relay = await surface.seedDeliver(store, 'work-demo', `run-${step}`, `call-${step}`, BigInt((step + 1) * 10))
      assert.equal(relay.ok, true, relay.error)
      assert.equal(relay.decision, 'relayed')
    }
    const last = await surface.seedReview(store, 'work-demo', 'Promoted')
    assert.equal(last.ok, true, last.error)
    const blocked = await surface.seedDeliver(store, 'work-demo', 'run-demo', 'call-demo', 30n)
    assert.equal(blocked.ok, false, 'an authorized tenure does not deliver without a draft and a session')
    assert.match(blocked.error, /draft|session/i)
    const stored = surface.workView(store, 'work-demo')
    assert.equal(stored.found, true)
    assert.equal(stored.sequence, 'S1,S2,S3')
    assert.equal(stored.active, true)
    assert.equal(stored.reviewed, 'Promoted')
    assert.equal(stored.delivered, false, 'the refused deliver writes no retirement')
    assert.equal(surface.roadDevOps(store, 'work-demo').devopsId, 'devops-fixed')
  })
})

test('WHAT[work-033] an illegal first stage is rejected by the fold before any live append', async () => {
  const opened = surface.encodeOpening('plan', 'work-mock', '/repo', 0n)
  const illegal = opened.replace('"stage":"S1"', '"stage":"S2"')
  const read = surface.readProbe(illegal)
  assert.equal(read.ok, false, 'the first incumbency must open at S1')
  assert.equal(read.opened, false)
  assert.equal(read.names, '')
  assert.match(read.reason, /Invalid first stage/)

  await withStore(async (store) => {
    const seeded = await surface.seedOpen(store, 'plan', 'work-mock', '/repo', 0n)
    assert.equal(seeded.ok, true, seeded.error)
    const database = new DatabaseSync(surface.journalPath(store.gitCommonDir))
    const journal = new SqliteJournal(database)
    try {
      const appended = journal.read('work-mock')
      assert.equal(appended.length, 1)
      const events = JSON.parse(appended[0]).events
      assert.equal(events[1].stage, 'S1', 'the kernel does not append an S2 opening')
    } finally {
      journal.close()
    }
  })
})

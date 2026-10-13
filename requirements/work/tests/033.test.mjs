import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, realpathSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const eventsOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const incumbencyIdOf = (text) =>
  [...eventsOf(text)].reverse().find((event) => event.event_type === 'IncumbencyOpened').payload.incumbencyId
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
  assert.deepEqual(Object.keys(workOpened.payload), sorted(Object.keys(workOpened.payload)))
  assert.deepEqual(Object.keys(workOpened.payload), ['authorityInitial', 'root', 'sequence', 'workId'])
  assert.deepEqual(Object.keys(incumbencyOpened.payload), ['incumbencyId', 'openingCursor', 'stage', 'workId'])
  assert.equal(incumbencyOpened.payload.openingCursor, '42')

  const reviewed = surface.reviewProbe(opened, incumbencyIdOf(opened), 'Promoted')
  const review = JSON.parse(reviewed.bytes)
  assert.deepEqual(Object.keys(review.payload), ['incumbencyId', 'outcome', 'stage'])

  const relayed = surface.deliverProbe(`${opened}${reviewed.bytes}`, 'run-1', 'call-1', 100n)
  const [retired, successor] = eventsOf(relayed.bytes)
  assert.deepEqual(Object.keys(retired.payload), sorted(Object.keys(retired.payload)))
  assert.equal(retired.payload.outcome, 'Relayed')
  assert.equal(retired.payload.retirementCursor, '100')
  assert.ok(Object.keys(retired.payload).includes('incumbencyId'))
  assert.ok(Object.keys(retired.payload).includes('retirementId'))
  assert.deepEqual(Object.keys(successor.payload), ['incumbencyId', 'openingCursor', 'stage', 'workId'])

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
    const receipt = surface.deliverStateProbe(`${text}${last.bytes}`, 'run-final', 'call-final', 30n)
    const reopened = surface.reopenProbe(`${text}${last.bytes}${receipt.bytes}`, incumbencyIdOf(text))
    return { receipt, reopened }
  })()
  const receipt = JSON.parse(full.receipt.bytes)
  for (const key of ['digest', 'execSessionId', 'incumbencyId', 'outcome', 'path', 'retirementCursor']) {
    assert.equal(Object.keys(receipt.payload).includes(key), true, key)
  }
  const reopened = JSON.parse(full.reopened.bytes)
  assert.deepEqual(Object.keys(reopened.payload), ['incumbencyId'])
  assert.equal(reopened.event_type, 'Reopened')
})

test('WHAT[work-033] canonical lines round-trip and keep int64 cursor precision', () => {
  const large = 9007199254740995n
  const opened = surface.encodeOpening('plan', 'work-1', '/repo/root', large)
  assert.equal(opened.endsWith('\n'), true, 'a canonical lifecycle line ends with LF')
  const decoded = surface.decodeDocument(opened)
  assert.equal(decoded.ok, true, decoded.fault)
  assert.equal(decoded.names, 'WorkOpened,IncumbencyOpened')
  const openedEvent = eventsOf(opened)[1]
  assert.equal(openedEvent.payload.openingCursor, '9007199254740995')
  assert.equal(BigInt(openedEvent.payload.openingCursor), large)
  assert.equal(surface.documentView(opened).openingCursor, large)

  const truncated = opened.replace(/\n$/, '')
  const invalid = surface.decodeDocument(truncated)
  assert.equal(invalid.ok, false)
  assert.equal(surface.readProbe(truncated).position, 'StorageInvalid')
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
  const delivered = surface.deliverStateProbe(text, 'run-demo', 'call-demo', 30n)
  assert.equal(delivered.decision, 'Write', delivered.detail)
  text = `${text}${delivered.bytes}`

  const folded = surface.readProbe(text)
  assert.equal(folded.position, 'DeliveredIdempotent')
  const view = surface.documentView(text)
  assert.equal(view.found, true)
  assert.equal(view.workId, 'work-demo')
  assert.equal(view.retiredCount, 3)
  assert.equal(view.delivered, true)
  assert.equal(view.digest, 'sha256-receipt')
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
  assert.equal(read.position, 'Rejected', 'the first incumbency must open at S1')
  const decoded = surface.decodeDocument(illegal)
  assert.equal(decoded.ok, true, 'the bytes are legal; the fold is what rejects them')

  await withStore(async (store) => {
    const batches = []
    const append = store.Append.bind(store)
    store.Append = async (envelopes) => {
      const batch = []
      for (const envelope of envelopes) batch.push(envelope)
      batches.push(batch)
      return append(envelopes)
    }
    const seeded = await surface.seedOpen(store, 'plan', 'work-mock', '/repo', 0n)
    assert.equal(seeded.ok, true, seeded.error)
    assert.equal(batches.length, 1)
    assert.equal(batches[0][1].Payload.stage, 'S1', 'the kernel does not append an S2 opening')
  })
})

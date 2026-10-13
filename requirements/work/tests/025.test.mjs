import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle

const linesOf = (text) => text.split('\n').filter((line) => line.length > 0)
const eventsOf = (text) => linesOf(text).map((line) => JSON.parse(line))
const incumbencyIdOf = (text) => eventsOf(text).find((event) => event.event_type === 'IncumbencyOpened').payload.incumbencyId

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-007-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const workOf = (store, workId) => store.TryCurrent('Lifecycle').get(workId)

const batchesOf = (store) => {
  const batches = []
  const append = store.Append.bind(store)
  store.Append = async (envelopes) => {
    const batch = []
    for (const envelope of envelopes) batch.push(envelope)
    batches.push(batch)
    return append(envelopes)
  }
  return batches
}

const climbTo = async (store, root, workId, stage) => {
  const opened = await life.seedOpen(store, 'plan', workId, root, 0n)
  assert.equal(opened.ok, true, opened.error)
  const steps = { S1: 0, S2: 1, S3: 2 }[stage]
  for (let step = 0; step < steps; step += 1) {
    const before = workOf(store, workId)
    const reviewed = await life.seedReview(store, workId, 'Promoted')
    assert.equal(reviewed.ok, true, reviewed.error)
    const relayed = await surface.PlanningSurface.executeDeliver(
      store, root, workId, before.Active.Id, `${workId}-run-${step}`, `${workId}-call-${step}`, false,
      async () => { throw new Error('spawn must not run while climbing an unauthorised tenure') },
      BigInt((step + 1) * 10),
    )
    assert.equal(relayed.ok, true, `climb relay ${step} failed: ${relayed.error}`)
    assert.equal(relayed.relayed, true)
  }
  return workOf(store, workId)
}

test('WHAT[work-025] deliver without authorization relays: retires Relayed and opens the next tenure at the same stage', async () => {
  // The kernel fold, not the retired Plan events, is what an unauthorised
  // deliver writes. S2 is reached by promoting S1, then relaying.
  const text = life.encodeOpening('plan', 'work-h1', '/repo/root', 0n)
  const promoted = life.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  assert.equal(promoted.decision, 'Write')
  assert.equal(promoted.names, 'Reviewed')
  const relayed = life.deliverProbe(`${text}${promoted.bytes}`, 'run-1', 'call-1', 10n)
  assert.equal(relayed.decision, 'Write')
  assert.equal(relayed.detail, 'relayed')
  assert.equal(relayed.names, 'IncumbencyRetired,IncumbencyOpened')
  assert.equal(relayed.bytes.includes('ConfirmationCommitted'), false)
  assert.equal(relayed.bytes.includes('Delivered'), false)

  const folded = life.readProbe(`${text}${promoted.bytes}${relayed.bytes}`)
  assert.equal(folded.position, 'ActiveRebound')
  const written = eventsOf(relayed.bytes)
  assert.equal(written[0].payload.outcome, 'Relayed')
  assert.equal(written[1].payload.stage, 'S2', 'relay keeps the promoted stage')
  assert.equal(written[1].payload.openingCursor, written[0].payload.retirementCursor)

  await withStore(async (store, root) => {
    const before = await climbTo(store, root, 'work-h1', 'S2')
    assert.equal(String(before.Active.OpeningStage), 'S2')
    let spawnCalls = 0
    const batches = batchesOf(store)
    const res = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-h1', before.Active.Id, 'run-1', 'call-1', false,
      async () => { spawnCalls += 1; throw new Error('spawn must not be called on the relay path') },
      20n,
    )
    assert.equal(res.ok, true, res.error)
    assert.equal(res.relayed, true)
    assert.equal(res.retirementOutcome, 'Relayed')
    assert.equal(spawnCalls, 0)
    assert.equal(batches.length, 1, 'one relay is one append')
    assert.deepEqual(batches[0].map((envelope) => envelope.EventType), ['IncumbencyRetired', 'IncumbencyOpened'])
    assert.equal(batches[0].some((envelope) => envelope.EventType === 'ConfirmationCommitted'), false)
    const after = workOf(store, 'work-h1')
    assert.equal(String(after.Active.OpeningStage), 'S2')
    assert.equal(after.CurrentDelivered, false)
    assert.equal(after.Active.Id, res.nextIncumbencyId)
  })
})

test('WHAT[work-025] relay is blocked by live resources or a wrong incumbency id', async () => {
  await withStore(async (store, root) => {
    const opened = await life.seedOpen(store, 'plan', 'work-h2', root, 0n)
    assert.equal(opened.ok, true, opened.error)
    const before = workOf(store, 'work-h2')
    const batches = batchesOf(store)

    const blocked = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-h2', before.Active.Id, 'run-b', 'call-b', true,
      async () => { throw new Error('spawn must not be called') },
      10n,
    )
    assert.equal(blocked.ok, true, 'a live blocker records RetirementBlocked; it does not fail the call')
    assert.equal(blocked.relayed, false)
    assert.equal(blocked.delivered, false)
    assert.equal(blocked.retirementOutcome, '')
    assert.match(blocked.message, /blocked by live resources/i)
    assert.deepEqual(batches.map((batch) => batch.map((envelope) => envelope.EventType)), [['RetirementBlocked']])
    assert.equal(batches[0].some((envelope) => envelope.EventType === 'IncumbencyRetired'), false)
    const stayed = workOf(store, 'work-h2')
    assert.equal(stayed.Active.Id, before.Active.Id, 'a blocked relay does not retire the tenure')
    assert.equal(String(stayed.Active.OpeningCursor), '0')

    const wrong = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-h2', 'wrong-inc', 'run-w', 'call-w', false,
      async () => { throw new Error('spawn must not be called') },
      10n,
    )
    assert.equal(wrong.ok, false)
    assert.match(wrong.error, /does not match invocation target|unknown incumbency/i)
    assert.equal(batches.length, 1, 'a wrong incumbency id writes nothing')
  })
})

test('WHAT[work-025] missing retirement evidence causes no spawn and no Plan writes', async () => {
  await withStore(async (store, root) => {
    const opened = await life.seedOpen(store, 'plan', 'work-h3', root, 0n)
    assert.equal(opened.ok, true, opened.error)
    const before = workOf(store, 'work-h3')
    const batches = batchesOf(store)
    let spawnCalls = 0
    const res = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-h3', before.Active.Id, 'run-3', 'call-3', false,
      async () => { spawnCalls += 1; return 'exec-never' },
      undefined,
    )
    assert.equal(res.ok, false)
    assert.match(res.error, /retirement cursor evidence is required/i)
    assert.equal(spawnCalls, 0)
    assert.equal(batches.length, 0, 'no lifecycle events may be written without retirement evidence')
    assert.equal(workOf(store, 'work-h3').Active.Id, before.Active.Id)
  })
})

test('WHAT[work-025] the first successor cursor opens at the exact consumed-through retirement sequence, not +1', async () => {
  const text = life.encodeOpening('plan', 'work-h4', '/repo/root', 0n)
  const relayed = life.deliverProbe(text, 'run-4', 'call-4', 10n)
  assert.equal(relayed.decision, 'Write')
  const [retired, opened] = eventsOf(relayed.bytes)
  assert.equal(retired.event_type, 'IncumbencyRetired')
  assert.equal(retired.payload.outcome, 'Relayed')
  assert.equal(retired.payload.retirementCursor, '10')
  assert.equal(opened.event_type, 'IncumbencyOpened')
  assert.equal(opened.payload.openingCursor, '10')
  assert.notEqual(opened.payload.openingCursor, '11')
  assert.notEqual(opened.payload.openingCursor, '20')

  await withStore(async (store, root) => {
    const seeded = await life.seedOpen(store, 'plan', 'work-h4', root, 0n)
    assert.equal(seeded.ok, true, seeded.error)
    const before = workOf(store, 'work-h4')
    const batches = batchesOf(store)
    const res = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-h4', before.Active.Id, 'run-4', 'call-4', false,
      async () => { throw new Error('no spawn') },
      10n,
    )
    assert.equal(res.ok, true, res.error)
    assert.equal(res.relayed, true)
    assert.equal(batches.length, 1)
    assert.equal(batches[0][0].Payload.outcome, 'Relayed')
    assert.equal(String(batches[0][0].Payload.retirementCursor), '10')
    assert.equal(String(batches[0][1].Payload.openingCursor), '10')
    const after = workOf(store, 'work-h4')
    assert.equal(String(after.Active.OpeningCursor), '10')
    assert.equal(after.Active.Id, res.nextIncumbencyId)
  })
})

test('WHAT[work-025] continuous relay across a single work advances tenures at the same stage without spawning', async () => {
  // S1 reviewed Promoted projects to S2; every later relay keeps that stage.
  await withStore(async (store, root) => {
    const workId = 'work-relay-ladder'
    let work = await climbTo(store, root, workId, 'S2')
    assert.equal(String(work.Active.OpeningStage), 'S2')
    const batches = batchesOf(store)
    const seen = new Set()
    let cursor = 10n
    for (let i = 0; i < 3; i += 1) {
      cursor += 10n
      const res = await surface.PlanningSurface.executeDeliver(
        store, root, workId, work.Active.Id, `run-ladder-${i}`, `call-ladder-${i}`, false,
        async () => { throw new Error('spawn must never run during a relay ladder') },
        cursor,
      )
      assert.equal(res.ok, true, `relay ${i} failed: ${res.error}`)
      assert.equal(res.relayed, true)
      assert.equal(res.retiredIncumbencyId, '')
      assert.equal(res.retirementOutcome, 'Relayed')
      work = workOf(store, workId)
      assert.equal(String(work.Active.OpeningStage), 'S2')
      assert.equal(String(work.Active.OpeningCursor), String(cursor))
      assert.equal(work.Active.Id, res.nextIncumbencyId)
      assert.equal(work.CurrentDelivered, false)
      seen.add(res.nextIncumbencyId)
      const batch = batches[i]
      assert.deepEqual(batch.map((envelope) => envelope.EventType), ['IncumbencyRetired', 'IncumbencyOpened'])
      assert.equal(String(batch[1].Payload.openingCursor), String(batch[0].Payload.retirementCursor))
      assert.equal(batch.some((envelope) => envelope.EventType === 'ConfirmationCommitted'), false)
    }
    assert.equal(seen.size, 3)
    assert.equal(batches.length, 3)
  })
})

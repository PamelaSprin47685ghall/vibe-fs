import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle

const framesOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const eventsOf = (text) => framesOf(text).flatMap((frame) => frame.events ?? [])

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-007-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root, commonDir)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const openedActiveId = (text) => {
  const folded = life.readProbe(text)
  assert.equal(folded.ok, true, folded.reason)
  assert.notEqual(folded.activeId, '')
  return folded.activeId
}

const openWork = async (store, commonDir, workId) => {
  const opened = await surface.PlanningSurface.ensurePlanWorkOpened(store, commonDir, workId, 0n)
  assert.equal(opened.ok, true, opened.error)
  return openedActiveId(life.encodeOpening('plan', workId, commonDir, 0n))
}

test('WHAT[work-025] deliver without authorization relays: retires Relayed and opens the next tenure at the same stage', async () => {
  const text = life.encodeOpening('plan', 'work-h1', '/repo/root', 0n)
  const promoted = life.reviewProbe(text, openedActiveId(text), 'Promoted')
  assert.equal(promoted.decision, 'Write')
  assert.equal(promoted.names, 'Reviewed')
  const relayed = life.deliverProbe(`${text}${promoted.bytes}`, 'run-1', 'call-1', 10n)
  assert.equal(relayed.decision, 'Write')
  assert.equal(relayed.detail, 'Relayed')
  assert.equal(relayed.names, 'IncumbencyRetired,IncumbencyOpened')
  assert.equal(relayed.bytes.includes('ConfirmationCommitted'), false)
  assert.equal(relayed.bytes.includes('Delivered'), false)

  const folded = life.readProbe(`${text}${promoted.bytes}${relayed.bytes}`)
  assert.equal(folded.ok, true)
  assert.equal(folded.next, 'Review')
  assert.equal(folded.activeId.length > 0, true)
  const written = eventsOf(relayed.bytes)
  assert.equal(written[0].outcome, 'Relayed')
  assert.equal(written[1].stage, 'S2', 'relay keeps the promoted stage')
  assert.equal(written[1].openingCursor, written[0].retirementCursor)
})

test('WHAT[work-025] continuous relay across a single work advances tenures at the same stage without spawning', () => {
  let text = life.encodeOpening('plan', 'work-ladder', '/repo/root', 0n)
  const promoted = life.reviewProbe(text, openedActiveId(text), 'Promoted')
  assert.equal(promoted.decision, 'Write', promoted.detail)
  text = `${text}${promoted.bytes}`
  let cursor = 10n
  for (let step = 0; step < 3; step += 1) {
    cursor += 10n
    const relayed = life.deliverProbe(text, `run-ladder-${step}`, `call-ladder-${step}`, cursor)
    assert.equal(relayed.decision, 'Write', relayed.detail)
    assert.equal(relayed.detail, 'Relayed')
    assert.equal(relayed.names, 'IncumbencyRetired,IncumbencyOpened')
    const [retired, opened] = eventsOf(relayed.bytes)
    assert.equal(retired.outcome, 'Relayed')
    assert.equal(opened.stage, 'S2')
    assert.equal(opened.openingCursor, retired.retirementCursor)
    assert.equal(opened.openingCursor, Number(cursor))
    text = `${text}${relayed.bytes}`
    const folded = life.readProbe(text)
    assert.equal(folded.ok, true, folded.reason)
    assert.equal(folded.activeId, opened.incumbencyId)
  }
})

test('WHAT[work-025] relay is blocked by live resources or a wrong incumbency id', async () => {
  await withStore(async (store, root, commonDir) => {
    const activeId = await openWork(store, commonDir, 'work-h2')

    const blocked = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-h2', activeId, 'run-b', 'call-b', true,
      async () => { throw new Error('spawn must not be called') },
      10n,
    )
    assert.equal(blocked.ok, true, blocked.error)
    assert.equal(blocked.relayed, false)
    assert.equal(blocked.delivered, false)
    assert.equal(blocked.retirementOutcome, 'Blocked')

    const wrong = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-h2', 'wrong-inc', 'run-w', 'call-w', false,
      async () => { throw new Error('spawn must not be called') },
      10n,
    )
    assert.equal(wrong.ok, false)
    assert.match(wrong.error, /does not match invocation target/)
  })
})

test('WHAT[work-025] missing retirement evidence causes no spawn and no Plan writes', async () => {
  await withStore(async (store, root, commonDir) => {
    const activeId = await openWork(store, commonDir, 'work-h3')
    let spawnCalls = 0
    const res = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-h3', activeId, 'run-3', 'call-3', false,
      async () => { spawnCalls += 1; return 'exec-never' },
      undefined,
    )
    assert.equal(res.ok, false)
    assert.match(res.error, /retirement cursor evidence is required/i)
    assert.equal(spawnCalls, 0)
  })
})

test('WHAT[work-025] the first successor cursor opens at the exact consumed-through retirement sequence, not +1', async () => {
  const text = life.encodeOpening('plan', 'work-h4', '/repo/root', 0n)
  const relayed = life.deliverProbe(text, 'run-4', 'call-4', 10n)
  assert.equal(relayed.decision, 'Write')
  const [retired, opened] = eventsOf(relayed.bytes)
  assert.equal(retired.type, 'IncumbencyRetired')
  assert.equal(retired.outcome, 'Relayed')
  assert.equal(retired.retirementCursor, 10)
  assert.equal(opened.type, 'IncumbencyOpened')
  assert.equal(opened.openingCursor, 10)
  assert.notEqual(opened.openingCursor, 11)
  assert.notEqual(opened.openingCursor, 20)
})


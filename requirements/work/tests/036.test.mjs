import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const life = lifecycle.LifecycleSurface ?? lifecycle

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-020-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

test('WHAT[work-036] the first prompt of a plan session opens the work and starts the first tenure at S1', async () => {
  assert.equal(typeof surface.PlanningSurface.ensurePlanWorkOpened, 'function')

  // Opening is exactly WorkOpened + the first IncumbencyOpened. DevOps binding
  // is a later ask, not part of opening.
  const sessionId = 'ses-plan-entry-1'
  const bytes = life.encodeOpening('plan', sessionId, '/workspace/repo', 7n)
  const events = bytes.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
  assert.deepEqual(events.map((event) => event.event_type), ['WorkOpened', 'IncumbencyOpened'])
  assert.equal(events.some((event) => event.event_type === 'DevOpsBound'), false)
  assert.equal(events[0].payload.workId, sessionId)
  assert.equal(events[1].payload.stage, 'S1')
  assert.equal(events[1].payload.openingCursor, '7')
  const probed = life.readProbe(bytes)
  assert.equal(probed.position, 'ActiveRebound')
  assert.equal(probed.sequence, 'S1,S2,S3')

  await withStore(async (store, root) => {
    const batches = []
    const append = store.Append.bind(store)
    store.Append = async (envelopes) => {
      const batch = []
      for (const envelope of envelopes) batch.push(envelope)
      batches.push(batch)
      return append(envelopes)
    }

    const missing = await surface.PlanningSurface.ensurePlanWorkOpened(store, root, sessionId, undefined)
    assert.equal(missing.ok, false, 'opening without an observed cursor must refuse')
    assert.match(missing.error, /opening cursor was not observed/i)
    assert.equal(batches.length, 0)

    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(store, root, sessionId, 7n)
    assert.equal(opened.ok, true, opened.error)
    assert.equal(batches.length, 1, 'the two opening events share one append')
    assert.deepEqual(batches[0].map((envelope) => envelope.EventType), ['WorkOpened', 'IncumbencyOpened'])
    assert.equal(batches[0].some((envelope) => envelope.EventType === 'DevOpsBound'), false)
    assert.equal(String(batches[0][1].Payload.openingCursor), '7')
    assert.equal(batches[0][1].Payload.stage, 'S1')

    const work = store.TryCurrent('Lifecycle').get(sessionId)
    assert.equal(work.WorkId, sessionId)
    assert.equal(String(work.Active.OpeningStage), 'S1')
    assert.equal(String(work.Active.OpeningCursor), '7')
    assert.equal(work.DevOps ?? null, null, 'opening does not bind DevOps')

    const again = await surface.PlanningSurface.ensurePlanWorkOpened(store, root, sessionId, 8n)
    assert.equal(again.ok, true)
    assert.equal(batches.length, 1, 'an already-open work must not append duplicate events')
  })
})

test('WHAT[work-036] the entry never forges an S1 fallback when the store is unavailable', async () => {
  let appendCalled = false
  const brokenStore = {
    TryCurrentThrows: true,
    TryCurrent: () => null,
    TryHead: () => null,
    Append: async () => {
      appendCalled = true
      return { tag: 0 }
    },
  }
  const res = await surface.PlanningSurface.ensurePlanWorkOpened(brokenStore, '/workspace/repo', 'ses-plan-entry-2', 7n)
  assert.equal(res.ok, false)
  assert.match(res.error, /cannot be read/i)
  assert.equal(appendCalled, false)
})

test('WHAT[work-036] subsequent manager transform passes on an already-open work do not append duplicate WorkOpened or IncumbencyOpened events', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const managerSession = 'ses-manager-open-idempotency'
    await hooks['chat.message'](
      { sessionID: managerSession, agent: 'manager' },
      {
        message: { id: 'msg-mgr-1', sessionID: managerSession, role: 'user', agent: 'manager' },
        parts: [{ id: 'prt-mgr-1', type: 'text', text: 'first instruction' }],
      },
    )

    const user1 = {
      info: { id: 'msg-mgr-1', sessionID: managerSession, role: 'user', agent: 'manager', time: { created: 1 } },
      parts: [{ id: 'prt-mgr-1', type: 'text', text: 'first instruction' }],
    }
    const asst1 = {
      info: {
        id: 'asst-mgr-1',
        sessionID: managerSession,
        parentID: 'msg-mgr-1',
        role: 'assistant',
        agent: 'manager',
        providerID: 'provider',
        modelID: 'manager-model',
        time: { created: 2 },
      },
      parts: [],
    }
    runtime.pushHostMessage(managerSession, user1)
    runtime.pushHostMessage(managerSession, asst1)

    await hooks['experimental.chat.messages.transform']({ sessionID: managerSession }, { messages: [user1] })

    const commonDir = join(directory, '.git')
    const store = workspace.acquire(commonDir)
    const work1 = store.TryCurrent('Lifecycle').get(managerSession)
    assert.ok(work1, 'first transform opens the manager work')

    let duplicateAppends = 0
    const origAppend = store.Append.bind(store)
    store.Append = async (envelopes) => {
      for (const env of envelopes) {
        if (env.EventType === 'WorkOpened' || env.EventType === 'IncumbencyOpened') duplicateAppends += 1
      }
      return origAppend(envelopes)
    }

    asst1.info.finish = 'stop'
    asst1.info.time.completed = 3
    asst1.parts = [{ id: 'prt-asst-1', type: 'text', text: 'step 1 done' }]
    const asst2 = {
      info: {
        id: 'asst-mgr-2',
        sessionID: managerSession,
        parentID: 'msg-mgr-1',
        role: 'assistant',
        agent: 'manager',
        providerID: 'provider',
        modelID: 'manager-model',
        time: { created: 4 },
      },
      parts: [],
    }
    runtime.pushHostMessage(managerSession, asst2)
    await hooks['experimental.chat.messages.transform']({ sessionID: managerSession }, { messages: [user1, asst1] })
    assert.equal(duplicateAppends, 0, 'subsequent transform pass must not re-append WorkOpened / IncumbencyOpened')
  })
})

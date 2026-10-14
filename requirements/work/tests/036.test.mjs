import assert from 'node:assert/strict'
import { existsSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { DatabaseSync } from 'node:sqlite'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { DurableWork } from 'wanxiangli/durable-work'
import { SqliteJournal } from 'wanxiangli/eventstore'
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
  const events = bytes.split('\n').filter((line) => line.length > 0).flatMap((line) => JSON.parse(line).events ?? [])
  assert.deepEqual(events.map((event) => event.type), ['WorkOpened', 'IncumbencyOpened'])
  assert.equal(events.some((event) => event.type === 'DevOpsBound'), false)
  assert.equal(events[0].workId, sessionId)
  assert.equal(events[1].stage, 'S1')
  assert.equal(events[1].openingCursor, 7)
  const probed = life.readProbe(bytes)
  assert.equal(probed.next, 'Review')
  assert.equal(probed.sequence, 'S1,S2,S3')

  await withStore(async (store, root) => {
    const openingAppends = () => {
      const path = life.journalPath(store.gitCommonDir)
      if (!existsSync(path)) return []
      const database = new DatabaseSync(path)
      const journal = new SqliteJournal(database)
      try {
        return journal.read(sessionId)
      } finally {
        journal.close()
      }
    }

    const missing = await surface.PlanningSurface.ensurePlanWorkOpened(store, root, sessionId, undefined)
    assert.equal(missing.ok, false, 'opening without an observed cursor must refuse')
    assert.match(missing.error, /opening cursor was not observed/i)
    assert.equal(openingAppends().length, 0)

    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(store, root, sessionId, 7n)
    assert.equal(opened.ok, true, opened.error)
    const appended = openingAppends()
    assert.equal(appended.length, 1, 'the two opening events share one append')
    const events = JSON.parse(appended[0]).events
    assert.deepEqual(events.map((event) => event.type), ['WorkOpened', 'IncumbencyOpened'])
    assert.equal(events.some((event) => event.type === 'DevOpsBound'), false)
    assert.equal(events[1].openingCursor, 7)
    assert.equal(events[1].stage, 'S1')

    const viewDatabase = new DatabaseSync(life.journalPath(store.gitCommonDir))
    const viewJournal = new SqliteJournal(viewDatabase)
    try {
      const view = new DurableWork(sessionId, viewJournal).inspect()
      assert.equal(view.workId, sessionId)
      assert.deepEqual(view.sequence, ['S1', 'S2', 'S3'])
      assert.equal(view.stage, 'S1')
      assert.equal(view.active.openingStage, 'S1')
      assert.equal(view.active.openingCursor, 7)
      assert.equal(view.devopsId ?? null, null, 'opening does not bind DevOps')
    } finally {
      viewJournal.close()
    }

    const again = await surface.PlanningSurface.ensurePlanWorkOpened(store, root, sessionId, 8n)
    assert.equal(again.ok, true)
    assert.equal(openingAppends().length, 1, 'an already-open work must not append duplicate events')
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

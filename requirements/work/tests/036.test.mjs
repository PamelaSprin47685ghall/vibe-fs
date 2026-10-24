import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, mkdtempSync, realpathSync, rmSync } from 'node:fs'
import { DatabaseSync } from 'node:sqlite'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { DurableWork } from 'wanxiangli/durable-work'
import { SqliteJournal } from 'wanxiangli/eventstore'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import { ensurePlanWorkOpenedForSession } from '../../../dist/OpenCode/Plugin/PlanWorkOpenWiring.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const life = lifecycle.LifecycleSurface ?? lifecycle

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-020-'))
  const commonDir = join(root, '.Herdchestra')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root, commonDir)
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

  await withStore(async (store, root, commonDir) => {
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

    const missing = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, root, sessionId, undefined)
    assert.equal(missing.ok, false, 'opening without an observed cursor must refuse')
    assert.match(missing.error, /opening cursor was not observed/i)
    assert.equal(openingAppends().length, 0)

    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, root, sessionId, 7n)
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

    const again = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, root, sessionId, 8n)
    assert.equal(again.ok, true)
    assert.equal(openingAppends().length, 1, 'an already-open work must not append duplicate events')

    const conflictingRoot = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, join(root, 'other'), sessionId, 9n)
    assert.equal(conflictingRoot.ok, false, 'an existing work under the same id must not hide a root collision')
    assert.match(conflictingRoot.error, /different root/i)
    assert.equal(openingAppends().length, 1, 'a refused root collision must append nothing')
  })
})

test('WHAT[work-036] the entry needs only the lifecycle journal directory, not an acquired workspace EventStore', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-no-event-store-'))
  const commonDir = join(root, '.Herdchestra')
  mkdirSync(commonDir, { recursive: true })
  try {
    const legacyEvents = join(commonDir, 'wanxiangshu', 'events')
    assert.equal(existsSync(legacyEvents), false, 'the legacy EventStore has not been activated')
    const res = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, root, 'ses-plan-entry-2', 7n)
    assert.equal(res.ok, true, res.error)
    assert.equal(existsSync(legacyEvents), false, 'lifecycle opening must not create the legacy event stream')
    assert.equal(existsSync(life.journalPath(commonDir)), true, 'the lifecycle journal is the only durable opening output')
    assert.equal(life.workView({ gitCommonDir: commonDir }, 'ses-plan-entry-2').found, true)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[work-036] the entry never forges an S1 fallback when the lifecycle directory is unavailable', async () => {
  const missing = join(tmpdir(), 'wxs-planning-missing-dir', 'never-created')
  const res = await surface.PlanningSurface.ensurePlanWorkOpened(missing, '/workspace/repo', 'ses-plan-entry-3', 7n)
  assert.equal(res.ok, false)
  assert.match(res.error, /cannot be read/i)
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

    const commonDir = join(directory, '.Herdchestra')
    const store = workspace.acquire(commonDir)
    const openingEvents = () => {
      const path = life.journalPath(store.gitCommonDir)
      if (!existsSync(path)) return []
      const database = new DatabaseSync(path)
      const journal = new SqliteJournal(database)
      try {
        return journal.read(managerSession).flatMap((line) => JSON.parse(line).events ?? [])
      } finally {
        journal.close()
      }
    }
    const openedCount = () =>
      openingEvents().filter((event) => event.type === 'WorkOpened' || event.type === 'IncumbencyOpened').length
    assert.equal(openedCount(), 2, 'first transform opens the manager work with exactly the two opening events')

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
    assert.equal(openedCount(), 2, 'subsequent transform pass must not re-append WorkOpened / IncumbencyOpened')
  })
})

test('WHAT[work-036] manager opening fails closed when the same session id already names a Plan work', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const managerSession = 'ses-manager-sequence-collision'
    const commonDir = join(directory, '.Herdchestra')
    const store = workspace.acquire(commonDir)
    const seeded = await life.seedOpen(store, 'plan', managerSession, directory, 1n)
    assert.equal(seeded.ok, true, seeded.error)

    await hooks['chat.message'](
      { sessionID: managerSession, agent: 'manager' },
      {
        message: { id: 'msg-manager-collision', sessionID: managerSession, role: 'user', agent: 'manager' },
        parts: [{ id: 'prt-manager-collision', type: 'text', text: 'continue as manager' }],
      },
    )
    const user = {
      info: { id: 'msg-manager-collision', sessionID: managerSession, role: 'user', agent: 'manager', time: { created: 1 } },
      parts: [{ id: 'prt-manager-collision', type: 'text', text: 'continue as manager' }],
    }
    runtime.pushHostMessage(managerSession, user)

    await assert.rejects(
      hooks['experimental.chat.messages.transform']({ sessionID: managerSession }, { messages: [user] }),
      /Manager lifecycle opening failed: existing lifecycle work has a different sequence/,
    )
    assert.deepEqual(life.workView(store, managerSession).sequence, 'S1,S2,S3')
  })
})

test('WHAT[work-036] the first plan prompt opens work at the git worktree root, not the state directory', async () => {
  const directory = realpathSync(mkdtempSync(join(tmpdir(), 'wxs-plan-open-root-')))
  try {
    execFileSync('git', ['init', '--quiet', directory])
    const planSession = 'ses-plan-first-prompt-root'
    await ensurePlanWorkOpenedForSession(directory, planSession, 3n)

    const path = life.journalPath(join(directory, '.Herdchestra'))
    const database = new DatabaseSync(path)
    const journal = new SqliteJournal(database)
    let opened
    try {
      const events = journal.read(planSession).flatMap((line) => JSON.parse(line).events ?? [])
      opened = events.find((event) => event.type === 'WorkOpened')
    } finally {
      journal.close()
    }
    assert.equal(opened.root, directory)
    assert.deepEqual(opened.sequence, ['S1', 'S2', 'S3'])
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[work-036] only a Manager session opens execution work; another managed role opens nothing', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const drive = async (agent) => {
      const sessionId = `ses-entry-closed-${agent}`
      const physical = `root-${sessionId}`
      await acceptAuthorityRoot(runtime, sessionId, agent, physical)
      const user = {
        info: { id: physical, sessionID: sessionId, role: 'user', agent, time: { created: 1 } },
        parts: [{ id: `prt-${agent}`, type: 'text', text: 'first instruction' }],
      }
      await hooks['chat.message']({ sessionID: sessionId, agent }, { message: user.info, parts: user.parts })
      runtime.pushHostMessage(sessionId, user)
      runtime.pushHostMessage(sessionId, {
        info: {
          id: `asst-${agent}`,
          sessionID: sessionId,
          parentID: physical,
          role: 'assistant',
          agent,
          providerID: 'provider',
          modelID: `${agent}-model`,
          time: { created: 2 },
        },
        parts: [],
      })
      await hooks['experimental.chat.messages.transform']({ sessionID: sessionId }, { messages: [user] })
      return sessionId
    }

    const store = workspace.acquire(join(directory, '.Herdchestra'))
    const eventsOf = (sessionId) => {
      const path = life.journalPath(store.gitCommonDir)
      if (!existsSync(path)) return []
      const database = new DatabaseSync(path)
      const journal = new SqliteJournal(database)
      try {
        return journal.read(sessionId).flatMap((line) => JSON.parse(line).events ?? [])
      } finally {
        journal.close()
      }
    }

    const engineer = await drive('engineer')
    const devops = await drive('devops')
    assert.deepEqual(eventsOf(engineer), [], 'an Engineer session owns no lifecycle work')
    assert.deepEqual(eventsOf(devops), [], 'a DevOps session owns no lifecycle work')

    const manager = await drive('manager')
    assert.deepEqual(eventsOf(manager).map((event) => event.type), ['WorkOpened', 'IncumbencyOpened'])
    assert.deepEqual(eventsOf(manager)[0].sequence, ['S4'])
  })
})

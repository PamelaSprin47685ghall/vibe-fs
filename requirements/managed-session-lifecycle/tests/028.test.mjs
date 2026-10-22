import test from 'node:test'
import assert from 'node:assert/strict'
import { chmodSync, mkdtempSync, readdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { dirname } from 'node:path'
import fs from 'node:fs'
import { syncBuiltinESMExports } from 'node:module'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { toolModule } from '../../delegation/tests/support/fork-runtime.mjs'
import {
  coldWorkSnapshot,
  deliverChildTurn,
  forkCall,
  joinCall,
  observeTerminals,
  withRealFork,
} from '../../delegation/tests/support/real-fork.mjs'
import { clearDeliveredValue, deliveredViewOf } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const withSettlementRuntime = async (owner, create, action) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-settlement-boundary-'))
  const runtime = await create(directory, [{ sessionId: owner, agent: 'manager' }])
  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'SETTLEMENT-CHARGE')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)
    await action(runtime, directory)
  } finally {
    await forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
}

test('WHAT[managed-session-lifecycle-028] the real completion reads the current view boundary exactly once', async () => {
  const owner = 'manager-settlement-exact-view-read'
  let reads = 0
  await withSettlementRuntime(owner,
    (directory, owners) => forkTool.createRuntimeWithViewRead(directory, owners, async () => { reads += 1 }),
    async (runtime) => {
      assert.equal(await forkTool.settle(runtime, owner, 'VIEW-READ-DONE', 'view-provider-run'), true)
      assert.equal(reads, 1)
      const works = forkTool.workSnapshot(runtime, owner)
      assert.ok(works.length > 0)
      assert.equal(works[0].lifecycle, 'CompletedAwaitingJoin')
      const joined = await forkTool.executeJoin(runtime, owner)
      assert.match(joined, /VIEW-READ-DONE|returned|fault|PERSIST|ERROR|CANCELLED/i)
    })
})

test('WHAT[managed-session-lifecycle-028] exact HandleWorkCompleted append EIO keeps unknown work Active and wakes join', async () => {
  const owner = 'manager-settlement-exact-eio'
  await withSettlementRuntime(owner, forkTool.createRuntime, async (runtime, directory) => {
    const delivered = await forkTool.prepareTerminalDelivery(runtime, owner, 'EXACT-EIO-DONE', 'eio-provider-run')
    const root = fs.realpathSync(directory)
    const append = fs.appendFileSync
    let injected = 0
    fs.appendFileSync = function (path, content, ...options) {
      if (String(content).includes('HandleWorkCompleted') && fs.realpathSync(dirname(path)).startsWith(`${root}/`)) {
        injected += 1
        throw Object.assign(new Error('actual HandleWorkCompleted append failed'), { code: 'EIO' })
      }
      return append.call(this, path, content, ...options)
    }
    syncBuiltinESMExports()
    try {
      await delivered()
    } finally {
      fs.appendFileSync = append
      syncBuiltinESMExports()
    }
    assert.equal(injected, 1, 'the production completion append must reach the exact EIO boundary')
    const works = forkTool.workSnapshot(runtime, owner)
    assert.ok(works.length > 0)
    assert.equal(works[0].lifecycle, 'Active', 'unknown persistence must retain the durable work')
    const joined = await forkTool.executeJoin(runtime, owner)
    assert.match(joined, /could not be gathered|未能被收集|materializ/i)
  })
})

test('WHAT[managed-session-lifecycle-028] PERSIST-settled Source wakes join without a durable journal revision', async () => {
  const owner = 'manager-settlement-exact-persist-source'
  await withSettlementRuntime(owner, forkTool.createRuntime, async (runtime) => {
    assert.equal(forkTool.workSnapshot(runtime, owner)[0].lifecycle, 'Active')
    assert.equal((await forkTool.injectPendingPersistFailure(runtime, owner)).ok, true)
    let timer
    const joined = await Promise.race([
      forkTool.executeJoin(runtime, owner),
      new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('join hung on PERSIST-settled Source with durable work Active')), 5000) }),
    ]).finally(() => clearTimeout(timer))
    assert.match(joined, /could not be gathered|未能被收集|materializ/i)
    assert.equal(forkTool.workSnapshot(runtime, owner)[0].lifecycle, 'Active')
  })
})

// managed-session-lifecycle: a proven child terminal must always produce an
// observable settlement. The incident class these tests pin: the child run
// reached Terminal(Completed) in the journal, but no HandleWorkCompleted was
// ever appended, so the parent join waited forever and only shutdown
// cancellation ended it.
//
// Every case below runs on the real plugin path. The child delivers through a
// real `this.deliver` (the only writer of the durable delivery witness), the
// terminal is published by production through `message.updated` → `session.idle`,
// and the parent joins through a real `this.join`. The only injected fault is a
// real append failure: the durable event log is made unwritable by the
// filesystem, so the writer's own EACCES is what breaks the settlement.

/** The durable event log of one workspace, made unwritable by the filesystem. */
const poisonEvents = (directory) => {
  const eventsDir = join(directory, '.Herdchestra', 'wanxiangshu', 'events')
  const files = readdirSync(eventsDir)
  for (const file of files) chmodSync(join(eventsDir, file), 0o444)
  return {
    restore: () => {
      for (const file of files) {
        try {
          chmodSync(join(eventsDir, file), 0o600)
        } catch {
          /* the workspace is removed right after */
        }
      }
    },
  }
}

/**
 * One real child turn, up to (but not including) the terminal publication.
 * The caller decides when the terminal is published — that is the seam the
 * settlement faults need.
 */
const deliverTurn = async (hooks, runtime, child, run, prose, material) => {
  const { assistant, result } = await deliverChildTurn(hooks, runtime, child, run, prose, material)
  assert.match(String(result), /# ok/, 'the exact Host-observed call must commit its delivery')
  return assistant
}

/** The assistant message the host observes as the completed turn. */
const publishMessageUpdate = async (hooks, assistant) => {
  assistant.info.time.completed = 3
  assistant.info.finish = 'stop'
  await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
}

/** The idle signal that makes the host publish the child's terminal. */
const publishSessionIdle = async (hooks, child) => {
  await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
}

/** Publish the terminal of one assistant message, the way the host does. */
const publishTerminal = async (hooks, assistant, child) => {
  await publishMessageUpdate(hooks, assistant)
  await publishSessionIdle(hooks, child)
}

/** A join that must terminate: a hung join is a failure, not a slow success. */
const joinWithin = async (hooks, owner, timeoutMs = 15000) => {
  let timer
  const outcome = await Promise.race([
    joinCall(hooks, owner),
    new Promise((resolve) => { timer = setTimeout(() => resolve('JOIN-HUNG'), timeoutMs) }),
  ]).finally(() => clearTimeout(timer))
  assert.notEqual(outcome, 'JOIN-HUNG', 'the join must terminate, never hang')
  return String(outcome)
}

test('WHAT[managed-session-lifecycle-028] child completion reads its delivery through the current view boundary', async () => {
  const owner = 'manager-settlement-workrecord-fault'
  await withRealFork(owner, async ({ hooks, createdIds, runtime, directory }) => {
    const terminals = await observeTerminals(runtime)
    try {
      const placed = await forkCall(hooks, owner, 'Ada', 'WORK-RECORD-FAULT-CHARGE')
      assert.match(placed, /Ada/)
      const child = createdIds[0]
      const run = `run-${child}-1`

      // The delivery is real: production commits the durable delivery
      // witness for it.
      const assistant = await deliverTurn(
        hooks,
        runtime,
        child,
        run,
        'PROSE',
        'WORK-RECORD-FAULT-DONE',
      )

      // The process-live delivery cell is lost — the child's view read now
      // yields nothing at all. The delivery itself stays proven: the durable
      // witness is the proof and it carries the material.
      clearDeliveredValue(child)
      assert.equal(deliveredViewOf(child), null, 'the live delivery cell is gone')

      await publishTerminal(hooks, assistant, child)
      await terminals.awaitRun(child, run)

      // The proven terminal still settles: the durable work is joinable and
      // the join returns the material that was delivered.
      const works = await coldWorkSnapshot(directory, owner)
      assert.ok(works.length > 0, 'the owner keeps a delivered view')
      assert.equal(works[0].lifecycle, 'CompletedAwaitingJoin', 'the completion cell is joinable')

      const joined = await joinWithin(hooks, owner)
      assert.match(joined, /ok = true/, `the join resolved with an explicit outcome: ${joined}`)
      assert.match(joined, /WORK-RECORD-FAULT-DONE/, 'the delivered material is returned')
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[managed-session-lifecycle-028] a proven terminal that faults during settlement resolves the run with an explicit failure instead of stranding the join', async () => {
  const owner = 'manager-settlement-delivery-fault'
  await withRealFork(owner, async ({ hooks, createdIds, runtime, directory }) => {
    const terminals = await observeTerminals(runtime)
    let poison = null
    try {
      const placed = await forkCall(hooks, owner, 'Ada', 'DELIVERY-FAULT-CHARGE')
      assert.match(placed, /Ada/)
      const child = createdIds[0]
      const run = `run-${child}-1`

      const assistant = await deliverTurn(
        hooks,
        runtime,
        child,
        run,
        'PROSE',
        'DELIVERY-FAULT-DONE',
      )
      await publishTerminal(hooks, assistant, child)
      await terminals.awaitRun(child, run)

      // The terminal was proven and its completion is durable: the work unit
      // is joinable.
      const before = await coldWorkSnapshot(directory, owner)
      assert.equal(before.length, 1)
      assert.equal(before[0].lifecycle, 'CompletedAwaitingJoin')

      // The settlement of that terminal now faults: the durable log is
      // unwritable, so the writer's own append fails.
      poison = poisonEvents(directory)

      // The join must terminate: the completion is consumable or the failure
      // is explicit, never an endless wait.
      const joined = await joinWithin(hooks, owner)
      assert.match(joined, /ok = false/, `the join surfaced an explicit failure: ${joined}`)
      assert.match(joined, /code = "not-ready"/, `the join reported the settlement failure: ${joined}`)

      // The failed settlement releases nothing: the work unit stays joinable
      // and its durable completion is untouched.
      const after = await coldWorkSnapshot(directory, owner)
      assert.equal(after.length, 1)
      assert.equal(after[0].lifecycle, 'CompletedAwaitingJoin')
    } finally {
      poison?.restore()
      terminals.dispose()
    }
  })
})

test('WHAT[managed-session-lifecycle-028] a PERSIST-settled Source with durable work still Active wakes the join instead of hanging on the journal revision', async () => {
  const owner = 'manager-settlement-persist-source'
  await withRealFork(owner, async ({ hooks, createdIds, runtime, directory }) => {
    const terminals = await observeTerminals(runtime)
    let poison = null
    try {
      const placed = await forkCall(hooks, owner, 'Ada', 'PERSIST-SOURCE-CHARGE')
      assert.match(placed, /Ada/)
      const child = createdIds[0]
      const run = `run-${child}-1`

      const assistant = await deliverTurn(
        hooks,
        runtime,
        child,
        run,
        'PROSE',
        'PERSIST-SOURCE-ANSWER',
      )

      // The turn is complete; the delivery is proven. The durable log is
      // unwritable from here on, so the settlement of the terminal cannot
      // commit its completion.
      await publishMessageUpdate(hooks, assistant)
      poison = poisonEvents(directory)

      // The idle signal cannot be persisted — the host reports the append
      // failure and the run settles as an explicit failure.
      let persistenceFailed = false
      try {
        await publishSessionIdle(hooks, child)
      } catch (error) {
        persistenceFailed = true
        assert.match(
          String(error),
          /persistence failed|CommitUnknown|append/i,
          `the host reported the durable append failure: ${error}`,
        )
      }

      // The run resolves with an explicit failure instead of silence.
      await terminals.awaitRun(child, undefined, { kind: 'Failed' })
      const failure = terminals.seen.find((entry) => entry.outcome?.kind === 'Failed')
      assert.ok(
        failure || persistenceFailed,
        'the settlement failure surfaced as an explicit terminal or an explicit persistence error',
      )

      // The join wakes on the settled Source; it does not wait for a journal
      // revision that can never advance.
      const joined = await joinWithin(hooks, owner)
      assert.match(joined, /ok = false/, `the join surfaced the settlement failure: ${joined}`)
      assert.match(joined, /code = "not-ready"/, `the join reported the settlement failure: ${joined}`)

      // The durable work is NOT released: it stays Active. An unknown durable
      // commit must not permit new work or masquerade as a success.
      const works = await coldWorkSnapshot(directory, owner)
      assert.equal(works.length, 1)
      assert.equal(works[0].lifecycle, 'Active')
    } finally {
      poison?.restore()
      terminals.dispose()
    }
  })
})

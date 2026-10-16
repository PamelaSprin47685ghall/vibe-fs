import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { execFile, spawnSync } from 'node:child_process'
import { promisify } from 'node:util'
import { fileURLToPath } from 'node:url'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as PtySurface from '../../../dist/Execution/Delegation/Fork/Host/HostForkPtySurface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import * as loadRecovery from '../../../dist/OpenCode/Host/LoadRecoverySurface.js'
import * as PtyToolSurface from '../../../dist/OpenCode/Tools/PtyToolSurface.js'
import { DatabaseSync } from 'node:sqlite'
import { withAdmittedChildren, withSharedAdmittedChildren } from './support/admitted-child-work.mjs'
import { Ownership } from 'wanxiangli/ownership'
import { loseOwnershipCommit } from './support/ownership-commit.mjs'
import { withAdmissionWorkers } from './support/pty-admission.mjs'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { hostPort } from '../../interaction-authority/tests/support/authority.mjs'

test('WHAT[managed-session-lifecycle-025] another process cannot reserve a PTY after the work round stops admission', { timeout: 20000 }, async () => {
  await withAdmissionWorkers('stop-first', async (producer, stopper) => {
    const stopped = await stopper.call('stop')
    assert.equal(stopped.finished, true, stopped.failure)
    const opened = await producer.call('open', 'late-pty')
    assert.equal(opened.ok, false, 'a completed work round must reject a foreign process reservation')
    assert.equal(await producer.call('spawns'), 0)
    assert.deepEqual(await producer.call('blockers'), [])
  })
})

const withRecoveryJournal = async (workspace, action) => {
  const opened = await journalSurface.JournalSurface_acquireSharedForWorkspace(workspace, process.pid, '2026-10-16T00:00:00Z')
  assert.equal(opened.ok, true, JSON.stringify(opened.error))
  const active = () => journalSurface.JournalSurface_snapshot(opened.journal).sessionProjections['devops-pty'].promptAuthority.activeLogicalRun
  try {
    return await action(opened.journal, active)
  } finally {
    journalSurface.JournalSurface_dispose(opened.journal)
  }
}

test('WHAT[managed-session-lifecycle-025] load recovery stops old round admission before voiding canonical work in another process', { timeout: 20000 }, async () => {
  await withAdmissionWorkers('void-stops-admission', async (producer, _other, workspace) => {
    await withRecoveryJournal(workspace, async (journal, active) => {
      assert.ok(active())
      await loadRecovery.settleChildRuns(journal, join(workspace, '.git'))
      assert.equal(active(), null)
      assert.equal((await producer.call('open', 'late-after-void')).ok, false)
      assert.equal(await producer.call('spawns'), 0)
    })
  })
})

test('WHAT[managed-session-lifecycle-025] load recovery preserves canonical work and an unbound PTY until its producer proves Ended', { timeout: 20000 }, async () => {
  await withAdmissionWorkers('void-retains-pty', async (producer, _other, workspace) => {
    const opened = await producer.call('open', 'foreign-before-void')
    assert.equal(opened.ok, true, opened.error)
    await withRecoveryJournal(workspace, async (journal, active) => {
      const before = active()
      await assert.rejects(() => loadRecovery.settleChildRuns(journal, join(workspace, '.git')), /local process binding/)
      assert.deepEqual(active(), before)
      assert.deepEqual(await producer.call('blockers'), [opened.id])
      assert.equal((await producer.call('open', 'late-before-ended')).ok, false)
      await producer.call('complete', 'foreign-before-void')
      await loadRecovery.settleChildRuns(journal, join(workspace, '.git'))
      assert.equal(active(), null)
      assert.equal((await producer.call('open', 'late-after-ended')).ok, false)
    })
  })
})

for (const afterCommit of [false, true]) {
  test(`WHAT[managed-session-lifecycle-025] load recovery cannot void a round after Drain acknowledgement is lost ${afterCommit ? 'after' : 'before'} COMMIT`, { timeout: 20000 }, async () => {
    await withAdmissionWorkers(`void-unknown-${afterCommit}`, async (producer, _other, workspace) => {
      await withRecoveryJournal(workspace, async (journal, active) => {
        const before = active()
        await loseOwnershipCommit(DatabaseSync, 'Drain', afterCommit, () => assert.rejects(
          () => loadRecovery.settleChildRuns(journal, join(workspace, '.git')), /Draining was not acknowledged: Unknown/,
        ))
        assert.deepEqual(active(), before)
        const late = await producer.call('open', 'after-unknown-void')
        assert.equal(late.ok, !afterCommit)
        if (late.ok) await producer.call('complete', 'after-unknown-void')
        await loadRecovery.settleChildRuns(journal, join(workspace, '.git'))
        assert.equal(active(), null)
        assert.equal((await producer.call('open', 'after-confirmed-void')).ok, false)
      })
    })
  })
}

test('WHAT[managed-session-lifecycle-025] a foreign committed reservation prevents premature terminal settlement', { timeout: 20000 }, async () => {
  await withAdmissionWorkers('reserve-first', async (producer, stopper, _workspace, start) => {
    const opened = await producer.call('open', 'running-pty')
    assert.equal(opened.ok, true, opened.error)
    const stopped = await stopper.call('stop')
    assert.equal(stopped.finished, false)
    assert.match(stopped.failure, /local process binding/)
    assert.deepEqual(await stopper.call('blockers'), [opened.id])
    await producer.call('complete', 'running-pty')
    assert.deepEqual(await stopper.call('blockers'), [])
    const late = await producer.call('open', 'after-drain')
    assert.equal(late.ok, false)
    const retry = await start()
    assert.equal((await retry.call('stop')).finished, true, 'the exact stopped round can settle after the producer proves Ended')
  })
})

const prepareConcurrentRound = async (producer, stopper) => {
  for (const [worker, name] of [[producer, 'warm-producer'], [stopper, 'warm-stopper']]) {
    const opened = await worker.call('open', name)
    assert.equal(opened.ok, true, opened.error)
    await worker.call('complete', name)
  }
}

for (const winner of ['reserve', 'drain']) {
  test(`WHAT[managed-session-lifecycle-025] two processes at the same ownership revision arbitrate Reserve/Drain with ${winner} first`, { timeout: 30000 }, async () => {
    await withAdmissionWorkers(`cas-${winner}`, async (producer, stopper, workspace, start) => {
      await prepareConcurrentRound(producer, stopper)
      const reserveRelease = join(workspace, 'release-reserve')
      const drainRelease = join(workspace, 'release-drain')
      const reserving = producer.call('open', 'concurrent-pty', { pause: { name: 'reserve', release: reserveRelease } })
      const reserve = await producer.barrier('reserve')
      const draining = stopper.call('stop', null, { pause: { name: 'drain', release: drainRelease } })
      const drain = await stopper.barrier('drain')
      assert.equal(reserve.revision, drain.revision, 'both candidates must have read the exact same durable revision')
      if (winner === 'reserve') {
        writeFileSync(reserveRelease, '')
        assert.equal((await reserving).ok, true)
        writeFileSync(drainRelease, '')
        const stopped = await draining
        assert.equal(stopped.finished, false)
        assert.match(stopped.failure, /Conflict/)
        const retry = await start()
        assert.equal((await retry.call('stop')).finished, false, 'a confirmed stop must retain the foreign live resource')
        assert.equal((await producer.call('open', 'after-confirmed-stop')).ok, false)
        await producer.call('complete', 'concurrent-pty')
        const settled = await start()
        assert.equal((await settled.call('stop')).finished, true)
      } else {
        writeFileSync(drainRelease, '')
        assert.equal((await draining).finished, true)
        writeFileSync(reserveRelease, '')
        const opened = await reserving
        assert.equal(opened.ok, false)
        assert.match(opened.error, /Conflict/)
        assert.equal(await producer.call('spawns'), 1, 'the losing Reserve must add no physical Spawn')
      }
      assert.deepEqual(await producer.call('blockers'), [])
    })
  })
}

test('WHAT[managed-session-lifecycle-025] two concurrent process reservations can grant only one first launch at the same revision', { timeout: 30000 }, async () => {
  await withAdmissionWorkers('reserve-reserve', async (producer, other, workspace) => {
    await prepareConcurrentRound(producer, other)
    const firstRelease = join(workspace, 'release-first')
    const secondRelease = join(workspace, 'release-second')
    const first = producer.call('open', 'first-concurrent', { pause: { name: 'first', release: firstRelease } })
    const arrived = (worker, name, operation) => Promise.race([
      worker.barrier(name),
      operation.then(value => { throw new Error(`Candidate did not reach ${name} barrier: ${JSON.stringify(value)}`) }),
    ])
    const a = await arrived(producer, 'first', first)
    const second = other.call('open', 'second-concurrent', { pause: { name: 'second', release: secondRelease } })
    const b = await arrived(other, 'second', second)
    assert.equal(a.revision, b.revision)
    writeFileSync(firstRelease, '')
    assert.equal((await first).ok, true)
    writeFileSync(secondRelease, '')
    const lost = await second
    assert.equal(lost.ok, false)
    assert.match(lost.error, /Conflict/)
    assert.equal(await producer.call('spawns') + await other.call('spawns'), 3)
    await producer.call('complete', 'first-concurrent')
  })
})

test('WHAT[managed-session-lifecycle-025] duplicate tool calls across processes never grant another launch, including after Ended', { timeout: 20000 }, async () => {
  await withAdmissionWorkers('duplicate', async (producer, other) => {
    assert.equal((await producer.call('open', 'same-tool-call')).ok, true)
    assert.equal((await other.call('open', 'same-tool-call')).ok, false)
    await producer.call('complete', 'same-tool-call')
    assert.equal((await other.call('open', 'same-tool-call')).ok, false)
    assert.equal(await producer.call('spawns'), 1)
    assert.equal(await other.call('spawns'), 0)
  })
})

test('WHAT[managed-session-lifecycle-025] detaching one runtime closes only its round subtree and keeps the other process eligible', { timeout: 20000 }, async () => {
  await withAdmissionWorkers('run-detach', async (producer, other) => {
    assert.equal((await producer.call('open', 'detached-pty')).ok, true)
    const kept = await other.call('open', 'kept-pty')
    assert.equal(kept.ok, true, kept.error)
    await producer.call('detachChild')
    assert.deepEqual(await other.call('blockers'), [kept.id])
    assert.equal((await producer.call('openCaptured', 'detached-late')).ok, false)
    assert.equal((await producer.call('open', 'replacement-runtime')).ok, true)
    assert.equal((await other.call('open', 'kept-next')).ok, true)
    await other.call('complete', 'kept-pty')
    await other.call('complete', 'kept-next')
    await producer.call('complete', 'replacement-runtime')
    assert.equal((await other.call('stop')).finished, true)
  })
})

test('WHAT[managed-session-lifecycle-025] a later Host-observed assistant run shares the admitted physical execution while an unknown run cannot borrow it', { timeout: 20000 }, async () => {
  await withAdmissionWorkers('later-provider', async producer => {
    assert.equal((await producer.call('openUnknown', 'unknown-provider')).ok, false)
    assert.equal(await producer.call('spawns'), 0)
    assert.equal((await producer.call('openLater', 'later-tool-call')).ok, true)
    await producer.call('complete', 'later-tool-call')
  })
})

for (const afterCommit of [false, true]) {
  test(`WHAT[managed-session-lifecycle-025] Reserve acknowledgement lost ${afterCommit ? 'after' : 'before'} COMMIT cannot launch in either process`, { timeout: 20000 }, async () => {
    await withAdmissionWorkers(`reserve-unknown-${afterCommit}`, async (producer, other) => {
      const { result } = await producer.call('open', 'unknown-reserve', { lose: { command: 'Reserve', afterCommit } })
      assert.equal(result.ok, false)
      assert.equal(await producer.call('spawns'), 0)
      const blockers = await other.call('blockers')
      assert.equal(blockers.length, afterCommit ? 1 : 0)
      if (afterCommit) {
        assert.equal((await other.call('open', 'unknown-reserve')).ok, false)
        assert.equal((await other.call('stop')).finished, false)
        assert.equal(await other.call('spawns'), 0)
      } else {
        assert.equal((await other.call('stop')).finished, true)
      }
    })
  })

  test(`WHAT[managed-session-lifecycle-025] Drain acknowledgement lost ${afterCommit ? 'after' : 'before'} COMMIT never reports terminal success`, { timeout: 20000 }, async () => {
    await withAdmissionWorkers(`drain-unknown-${afterCommit}`, async (producer, stopper, _workspace, start) => {
      const { result } = await stopper.call('stop', null, { lose: { command: 'Drain', afterCommit } })
      assert.equal(result.finished, false)
      assert.match(result.failure, /Unknown/)
      const late = await producer.call('open', 'after-unknown-drain')
      assert.equal(late.ok, !afterCommit)
      if (late.ok) await producer.call('complete', 'after-unknown-drain')
      const retry = await start()
      assert.equal((await retry.call('stop')).finished, true)
    })
  })

  test(`WHAT[managed-session-lifecycle-025] Drain acknowledgement lost ${afterCommit ? 'after' : 'before'} COMMIT still closes exact local PTYs and retains foreign blockers`, { timeout: 20000 }, async () => {
    await withAdmissionWorkers(`local-drain-unknown-${afterCommit}`, async (producer, other, _workspace, start) => {
      const local = await producer.call('open', 'local-at-terminal')
      const foreign = await other.call('open', 'foreign-at-terminal')
      assert.equal(local.ok, true, local.error)
      assert.equal(foreign.ok, true, foreign.error)
      const { result } = await producer.call('stop', null, { lose: { command: 'Drain', afterCommit } })
      assert.equal(result.finished, false)
      assert.match(result.failure, /Draining was not acknowledged: Unknown/)
      assert.deepEqual(await other.call('blockers'), [foreign.id], 'the controlled local process exits only after TERM; the foreign instance must remain')
      await other.call('complete', 'foreign-at-terminal')
      const retry = await start()
      assert.equal((await retry.call('stop')).finished, true)
    })
  })
}

test('WHAT[managed-session-lifecycle-025] a conflicting round Drain still closes local PTYs without clearing the winning foreign reservation', { timeout: 30000 }, async () => {
  await withAdmissionWorkers('local-drain-conflict', async (producer, other, workspace, start) => {
    await prepareConcurrentRound(producer, other)
    const local = await producer.call('open', 'local-before-conflict')
    assert.equal(local.ok, true, local.error)
    const reserveRelease = join(workspace, 'release-foreign')
    const drainRelease = join(workspace, 'release-local-stop')
    const reserving = other.call('open', 'winning-foreign', { pause: { name: 'foreign', release: reserveRelease } })
    const reserve = await other.barrier('foreign')
    const stopping = producer.call('stop', null, { pause: { name: 'stop', release: drainRelease } })
    const drain = await producer.barrier('stop')
    assert.equal(reserve.revision, drain.revision)
    writeFileSync(reserveRelease, '')
    const foreign = await reserving
    assert.equal(foreign.ok, true, foreign.error)
    writeFileSync(drainRelease, '')
    const stopped = await stopping
    assert.equal(stopped.finished, false)
    assert.match(stopped.failure, /Conflict/)
    assert.deepEqual(await other.call('blockers'), [foreign.id])
    await other.call('complete', 'winning-foreign')
    const retry = await start()
    assert.equal((await retry.call('stop')).finished, true)
  })
})

test('WHAT[managed-session-lifecycle-025] failed ownership journal initialization preserves its error, closes its native connection and grants no launch', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-init-025-'))
  let spawns = 0
  const owner = controlledOwner(directory, 'init-owner', (_id, kind) => { if (kind === 'Spawn') spawns++ })
  const execute = DatabaseSync.prototype.exec
  const close = DatabaseSync.prototype.close
  let failedConnection
  let closes = 0
  DatabaseSync.prototype.exec = function (sql) {
    if (!failedConnection && sql.startsWith('PRAGMA busy_timeout=0;')) {
      failedConnection = this
      throw new Error('ownership initialization was locked')
    }
    return execute.call(this, sql)
  }
  DatabaseSync.prototype.close = function () {
    const result = close.call(this)
    if (this === failedConnection) closes++
    return result
  }
  try {
    const result = await owner.fork('must-not-launch')
    assert.equal(result.ok, false)
    assert.match(result.error, /ownership initialization was locked/)
    assert.equal(closes, 1)
    assert.equal(failedConnection.isOpen, false)
    assert.equal(spawns, 0)
  } finally {
    DatabaseSync.prototype.exec = execute
    DatabaseSync.prototype.close = close
    await owner.detach()
    rmSync(directory, { recursive: true, force: true })
  }
})

const awaitPtyTarget = async marker => {
  const deadline = Date.now() + 5000
  while (!existsSync(marker)) {
    assert.ok(Date.now() < deadline, 'the real PTY target must publish its actual PID')
    await new Promise(resolve => setTimeout(resolve, 10))
  }
  const pid = Number(readFileSync(marker, 'utf8'))
  assert.ok(Number.isInteger(pid) && pid > 1)
  process.kill(pid, 0)
  return pid
}

const requirePtyTargetReaped = async pid => {
  const deadline = Date.now() + 2000
  while (true) {
    try { process.kill(pid, 0) } catch (error) {
      if (error.code !== 'ESRCH') throw error
      return
    }
    const status = spawnSync('ps', ['-p', String(pid), '-o', 'stat='], { encoding: 'utf8' })
    assert.ok(status.status === 1 || /^Z/.test(status.stdout.trim()), `target must be exited before Ended: ${status.stdout} ${status.stderr}`)
    assert.ok(Date.now() < deadline, 'the exact exited target PID must be reaped')
    await new Promise(resolve => setTimeout(resolve, 5))
  }
}

for (const afterCommit of [false, true]) {
  test(`WHAT[managed-session-lifecycle-025] a real local PTY exits despite Drain acknowledgement lost ${afterCommit ? 'after' : 'before'} COMMIT`, { timeout: 25000 }, async t => {
    if (spawnSync('bun', ['--version'], { encoding: 'utf8' }).status !== 0) return t.skip('Bun is required for the real PTY backend')
    await withAdmissionWorkers(`real-local-unknown-${afterCommit}`, async (producer, other, workspace) => {
      const marker = join(workspace, 'target-pid')
      const opened = await producer.call('open', `printf '%s' "$$" > '${marker}.pending'; mv '${marker}.pending' '${marker}'; exec sleep 30`)
      assert.equal(opened.ok, true, opened.error)
      try {
        const pid = await awaitPtyTarget(marker)
        const { result } = await producer.call('stop', null, { lose: { command: 'Drain', afterCommit } })
        assert.equal(result.finished, false)
        assert.match(result.failure, /Draining was not acknowledged: Unknown/)
        await requirePtyTargetReaped(pid)
        assert.deepEqual(await other.call('blockers'), [])
      } finally {
        await producer.call('close', opened.id)
      }
    }, { realBackend: true })
  })
}

for (const order of ['stop-first', 'reserve-first']) {
  test(`WHAT[managed-session-lifecycle-025] two Bun processes enforce ${order} admission with the real PTY backend`, { timeout: 25000 }, async t => {
    if (spawnSync('bun', ['--version'], { encoding: 'utf8' }).status !== 0) return t.skip('Bun is required for the real PTY backend')
    await withAdmissionWorkers(`real-${order}`, async (producer, stopper, workspace, start) => {
      const marker = join(workspace, 'target-pid')
      const command = `printf '%s' "$$" > '${marker}.pending'; mv '${marker}.pending' '${marker}'; exec sleep 30`
      if (order === 'stop-first') {
        assert.equal((await stopper.call('stop')).finished, true)
        assert.equal((await producer.call('open', command)).ok, false)
        assert.equal(existsSync(marker), false)
        return
      }
      const opened = await producer.call('open', command)
      assert.equal(opened.ok, true, opened.error)
      try {
        const pid = await awaitPtyTarget(marker)
        const stopped = await stopper.call('stop')
        assert.equal(stopped.finished, false)
        assert.match(stopped.failure, /local process binding/)
        assert.deepEqual(await stopper.call('blockers'), [opened.id])
        await producer.call('close', opened.id)
        await requirePtyTargetReaped(pid)
        assert.throws(() => process.kill(pid, 0), { code: 'ESRCH' })
        const retry = await start()
        assert.equal((await retry.call('stop')).finished, true)
        assert.deepEqual(await retry.call('blockers'), [])
      } finally {
        await producer.call('close', opened.id)
      }
    }, { realBackend: true })
  })
}

const devopsReturnDrain = label => withAdmittedChildren(label, 'manager-session', [
  { agentId: 'devops', sessionId: 'devops-session', role: 'devops' },
], journal => PtySurface.scenario('devops-return-drain', '', '', journal))

const ownershipHost = lifecycle.LifecycleSurface ?? lifecycle
const controlledOwner = (commonDir, sessionId, onCommand = () => {}) =>
  PtySurface.ownershipProbe(commonDir, sessionId, onCommand)

const loseCommitReceipt = async (afterCommit, action, shouldInject = () => true) => {
  const original = DatabaseSync.prototype.exec
  let injected = false
  DatabaseSync.prototype.exec = function (sql) {
    if (!injected && shouldInject() && /^COMMIT\b/i.test(sql.trim())) {
      injected = true
      if (afterCommit) original.call(this, sql)
      throw new Error('ownership commit receipt lost')
    }
    return original.call(this, sql)
  }
  try {
    const result = await action()
    assert.equal(injected, true, 'the production SQLite COMMIT must be reached')
    return result
  } finally {
    DatabaseSync.prototype.exec = original
  }
}

test('WHAT[managed-session-lifecycle-025] controlled PTY port completion clears actual HostForkRuntime ownership and name bookkeeping', async () => {
  // Scenario 1: fork a PTY, bind terminal name, assert outstanding before exit,
  // Complete the controlled backend port; this fixture starts no OS process.
  const res = await PtySurface.scenario('exit-cleanup', '', '')
  assert.equal(res.ok, true, 'exit-cleanup scenario must succeed')
  assert.equal(res.bindOk, true, 'terminal name must be bound successfully')

  // Before physical exit: PTY was tracked, owned, and findable by terminal name
  assert.ok(res.outstandingBefore.includes(res.id), 'SnapshotOutstandingPtyRuns must contain PTY before exit')
  assert.equal(res.ownedBefore, true, 'OwnsPty must be true before exit')
  assert.equal(res.byNameBefore, res.id, 'TryPtyByName must resolve to PTY id before exit')

  // After physical exit: HostForkRuntime bookkeeping is immediately cleared
  assert.equal(res.outstandingAfter.includes(res.id), false, 'SnapshotOutstandingPtyRuns must not contain PTY after exit')
  assert.equal(res.ownedAfter, false, 'OwnsPty must be false after exit')
  assert.equal(res.byNameAfter, undefined, 'TryPtyByName must be None after exit')
})

test('WHAT[managed-session-lifecycle-025] an unproven spawn exception retains its ownership blocker', async () => {
  const res = await PtySurface.scenario('fork-error', 'broken-cmd', 'spawn failed')
  assert.equal(res.ok, false, 'fork-error scenario must report failure')
  assert.equal(res.owned, true, 'an exception does not prove that the backend created no process')
})

test('WHAT[managed-session-lifecycle-025] the production backend refuses a PTY without a durable owner before reservation or spawn', async () => {
  const result = await PtySurface.scenario('missing-durable-owner', 'exit 0', '')
  assert.equal(result.ok, false)
  assert.match(result.error, /Durable PTY ownership is unavailable/)
  assert.equal(result.owned, false)
})

test('WHAT[managed-session-lifecycle-025] synchronous backend exit is observed before Fork returns', async () => {
  const res = await PtySurface.scenario('synchronous-exit', '', '')
  assert.equal(res.owned, false)
  assert.equal(res.live, false)
  assert.equal(res.completions, 1)
  assert.equal(res.snapshots, 0)
  assert.equal(res.bindAfter, false, 'a late name bind must not resurrect an exited terminal')
})

test('WHAT[managed-session-lifecycle-025] close without an exit witness fails and preserves the live blocker', async () => {
  const res = await PtySurface.scenario('close-without-exit', '', '')
  assert.match(res.error, /exit.*not.*observed/i)
  assert.equal(res.owned, true)
  assert.equal(res.live, true)
  assert.equal(res.completions, 0)
})

test('WHAT[managed-session-lifecycle-025] a failed startup observation cannot publish Ended for an unresolved process', async () => {
  const owner = controlledOwner(undefined, 'unknown-startup')
  const opened = await owner.fork('external-command')
  owner.failObservation([opened.id, 'PTY startup outcome is unknown: observer failed'])
  await assert.rejects(owner.closeAll(0), /startup outcome is unknown/)
  assert.deepEqual(owner.outstanding(), [opened.id])
  assert.equal(owner.exists(opened.id), true)
  assert.equal(owner.completions(), 0)
  owner.complete(opened.id)
  assert.deepEqual(owner.outstanding(), [])
  assert.equal(owner.completions(), 1)
})

for (const action of ['closeAll', 'detach', 'cancel', 'close']) {
  for (const afterCommit of [false, true]) {
    test(`WHAT[managed-session-lifecycle-025] ${action} Cancel receipt loss ${afterCommit ? 'after' : 'before'} COMMIT still closes each exact local process`, async () => {
      const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-cancel-failure-025-'))
      let owner
      try {
        const signalled = []
        owner = controlledOwner(directory, 'cancel-failure-owner', (id, kind) => {
          if (kind !== 'Signal') return
          signalled.push(id)
          owner.complete(id)
        })
        const first = await owner.fork('first-command')
        const second = await owner.fork('second-command')
        const close = () => action === 'close'
          ? owner.close([first.id, 0])
          : action === 'closeAll' ? owner.closeAll(0) : owner[action]()
        await loseOwnershipCommit(DatabaseSync, 'Cancel', afterCommit, () =>
          assert.rejects(close(), /CancellationRequested was not acknowledged: Unknown/))
        const closed = action === 'close' ? [first.id] : [first.id, second.id]
        assert.deepEqual(signalled, closed)
        for (const id of closed) {
          assert.equal(owner.exists(id), false)
          assert.equal(owner.owned(id), false)
        }
        assert.deepEqual(owner.outstanding(), action === 'close' ? [second.id] : [])
        if (action === 'close') {
          assert.equal(owner.exists(second.id), true)
          owner.complete(second.id)
        }
      } finally {
        if (owner) {
          for (const id of owner.outstanding()) owner.complete(id)
          await owner.closeAll(0)
        }
        rmSync(directory, { recursive: true, force: true })
      }
    })
  }
}

for (const afterCommit of [false, true]) {
  test(`WHAT[managed-session-lifecycle-025] Reserve receipt loss ${afterCommit ? 'after' : 'before'} COMMIT never authorizes Spawn`, async () => {
    const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-commit-025-'))
    try {
      let spawns = 0
      const owner = controlledOwner(directory, 'receipt-owner', () => { spawns += 1 })
      const opened = await loseCommitReceipt(afterCommit, () => owner.fork('external-command'))
      assert.equal(opened.ok, false)
      assert.equal(spawns, 0)
      const recovered = controlledOwner(directory, 'receipt-owner', () => { spawns += 1 })
      assert.equal(recovered.outstanding().length, afterCommit ? 1 : 0)
      assert.equal(spawns, 0)
      if (afterCommit) await assert.rejects(owner.detach(), /without a local process binding/)
      else await owner.detach()
      await recovered.detach()
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })
}

for (const afterCommit of [false, true]) {
  test(`WHAT[managed-session-lifecycle-025] Drain receipt loss ${afterCommit ? 'after' : 'before'} COMMIT cannot reopen a permanently detaching runtime`, async () => {
    const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-drain-admission-025-'))
    try {
      let spawns = 0
      const onCommand = (_, kind) => { if (kind === 'Spawn') spawns += 1 }
      const owner = controlledOwner(directory, 'drain-admission-owner', onCommand)
      await loseCommitReceipt(afterCommit, () => assert.rejects(owner.detach(), /Draining was not acknowledged: Unknown/))
      const blocked = await owner.fork('after-failed-detach')
      assert.equal(blocked.ok, false)
      assert.equal(spawns, 0)
      const successor = controlledOwner(directory, 'drain-admission-owner', onCommand)
      const opened = await successor.fork('new-runtime')
      assert.equal(opened.ok, true)
      assert.equal(spawns, 1)
      successor.complete(opened.id)
      await successor.detach()
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })
}

for (const afterCommit of [false, true]) {
  test(`WHAT[managed-session-lifecycle-025] detach reconciles Ended receipt loss ${afterCommit ? 'after' : 'before'} COMMIT before reporting success`, async () => {
    const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-detach-025-'))
    try {
      let ending = false
      const owner = controlledOwner(directory, 'detach-receipt-owner', (id, kind) => {
        if (kind !== 'Signal') return
        ending = true
        try { owner.complete(id) } finally { ending = false }
      })
      const opened = await owner.fork('external-command')
      await loseCommitReceipt(afterCommit, () => owner.detach(), () => ending)
      assert.equal(owner.exists(opened.id), false)
      assert.equal(owner.owned(opened.id), false)
      assert.deepEqual(owner.outstanding(), [])
      const recovered = controlledOwner(directory, 'detach-receipt-owner')
      assert.deepEqual(recovered.outstanding(), [])
      await recovered.detach()
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })
}

for (const action of ['detach', 'cancel']) {
  for (const withOwnPty of [false, true]) {
    test(`WHAT[managed-session-lifecycle-025] ${action} with ${withOwnPty ? 'one' : 'no'} local PTY preserves another runtime on the same port and durable root`, async () => {
      const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-shared-025-'))
      try {
        const signalled = []
        const pair = PtySurface.sharedOwnershipProbe(directory, 'shared-root', (id, kind) => {
          if (kind !== 'Signal') return
          signalled.push(id)
          pair.first.complete(id)
        })
        const first = withOwnPty ? await pair.first.fork('first-command') : undefined
        const second = await pair.second.fork('second-command')
        await pair.first[action]()
        assert.deepEqual(signalled, first ? [first.id] : [])
        assert.equal(pair.second.exists(second.id), true)
        assert.equal(pair.second.owned(second.id), true)
        assert.deepEqual(pair.second.outstanding(), [second.id])
        assert.equal(pair.second.completions(), 0)
        if (first) assert.equal(pair.first.owned(first.id), false)
        pair.second.complete(second.id)
        await pair.second.detach()
      } finally {
        rmSync(directory, { recursive: true, force: true })
      }
    })
  }
}

test('WHAT[managed-session-lifecycle-025] permanent detach removes its shared port listener while reusable drain keeps its listener', async () => {
  let pair
  pair = PtySurface.sharedOwnershipProbe(undefined, 'listener-lifetime-owner', (id, kind) => {
    if (kind === 'Signal') pair.second.complete(id)
  })
  assert.equal(pair.first.exitListeners(), 2)
  await pair.first.detach()
  assert.equal(pair.second.exitListeners(), 1)
  await pair.first.detach()
  assert.equal(pair.second.exitListeners(), 1)
  const cancelled = await pair.second.fork('first-command')
  assert.equal(cancelled.ok, true)
  await pair.second.closeAll(0)
  assert.equal(pair.second.exitListeners(), 1)
  const next = await pair.second.fork('next-command')
  assert.equal(next.ok, true)
  pair.second.complete(next.id)
  assert.equal(pair.second.completions(), 2)
  assert.equal(pair.second.owned(next.id), false)
  assert.deepEqual(pair.second.outstanding(), [])
  await pair.second.detach()
  assert.equal(pair.second.exitListeners(), 0)
})

for (const hasInitial of [false, true]) {
  test(`WHAT[managed-session-lifecycle-025] a terminated parent runtime ${hasInitial ? 'with' : 'without'} an initial PTY refuses new reserve or spawn and releases its listener`, async () => {
    let spawns = 0
    const owner = controlledOwner(undefined, 'terminated-admission-owner', (id, kind) => {
      if (kind === 'Spawn') spawns += 1
      if (kind === 'Signal') owner.complete(id)
    })
    if (hasInitial) assert.equal((await owner.fork('first-command')).ok, true)
    await owner.cancel()
    const next = await owner.fork('must-not-start')
    if (next.ok) owner.complete(next.id)
    assert.equal(next.ok, false)
    assert.match(next.error, /cancelled/)
    assert.equal(spawns, Number(hasInitial))
    assert.deepEqual(owner.outstanding(), [])
    assert.equal(owner.exitListeners(), 0)
  })
}

test('WHAT[managed-session-lifecycle-025] cancellation that retains the canonical fixed DevOps keeps PTY reuse and completion delivery', async () => {
  const parent = 'companion-listener-owner'
  await withAdmittedChildren('companion-listener', parent, [
    { agentId: 'devops', sessionId: 'devops-session', role: 'devops' },
  ], async journal => {
    const owner = PtySurface.companionOwnershipProbe(journal, parent, (id, kind) => {
      if (kind === 'Signal') owner.complete(id)
    })
    try {
      assert.equal((await owner.fork('first-command')).ok, true)
      await owner.cancel()
      assert.equal(owner.exitListeners(), 1)
      const next = await owner.fork('next-command')
      assert.equal(next.ok, true)
      owner.complete(next.id)
      assert.equal(owner.completions(), 2)
      assert.deepEqual(owner.outstanding(), [])
    } finally {
      await owner.detach()
    }
    assert.equal(owner.exitListeners(), 0)
  })
})

test('WHAT[managed-session-lifecycle-025] failed terminal cancellation retains observation until exit while keeping new PTY admission closed', async () => {
  const owner = controlledOwner(undefined, 'failed-terminal-admission-owner')
  const opened = await owner.fork('first-command')
  await assert.rejects(owner.cancel(), /exit was not observed/)
  assert.equal(owner.exitListeners(), 1)
  const next = await owner.fork('must-not-start')
  if (next.ok) owner.complete(next.id)
  owner.complete(opened.id)
  assert.equal(next.ok, false)
  assert.match(next.error, /cancelled/)
  assert.deepEqual(owner.outstanding(), [])
  assert.equal(owner.exitListeners(), 0)
})

test('WHAT[managed-session-lifecycle-025] failed detach retains its exit listener until a late exit observation settles the last lease', async () => {
  const owner = controlledOwner(undefined, 'late-listener-owner')
  const opened = await owner.fork('external-command')
  await assert.rejects(owner.detach(), /exit was not observed/)
  assert.equal(owner.exitListeners(), 1)
  assert.equal(owner.owned(opened.id), true)
  owner.complete(opened.id)
  assert.equal(owner.owned(opened.id), false)
  assert.deepEqual(owner.outstanding(), [])
  assert.equal(owner.exitListeners(), 0)
})

test('WHAT[managed-session-lifecycle-025] detach rejects when the exact Ended observation still cannot be committed', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-detach-failure-025-'))
  const original = DatabaseSync.prototype.exec
  let ending = false
  const owner = controlledOwner(directory, 'failed-detach-owner', (id, kind) => {
    if (kind !== 'Signal') return
    ending = true
    owner.complete(id)
  })
  try {
    const opened = await owner.fork('external-command')
    DatabaseSync.prototype.exec = function (sql) {
      if (ending && /^COMMIT\b/i.test(sql.trim())) throw new Error('Ended commit remains unknown')
      return original.call(this, sql)
    }
    await assert.rejects(owner.detach(), /Ended was not acknowledged: Unknown/)
    assert.equal(owner.exists(opened.id), false)
    assert.equal(owner.owned(opened.id), true)
    assert.deepEqual(owner.outstanding(), [opened.id])
    assert.equal(owner.exitListeners(), 1)
    DatabaseSync.prototype.exec = original
    await owner.closeAll(0)
    assert.equal(owner.owned(opened.id), false)
    assert.deepEqual(owner.outstanding(), [])
    assert.equal(owner.exitListeners(), 0)
  } finally {
    DatabaseSync.prototype.exec = original
    rmSync(directory, { recursive: true, force: true })
  }
})

for (const afterCommit of [false, true]) {
  test(`WHAT[managed-session-lifecycle-025] Ended receipt loss ${afterCommit ? 'after' : 'before'} COMMIT replays the exact exit observation`, async () => {
    const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-ended-025-'))
    try {
      const owner = controlledOwner(directory, 'ended-receipt-owner')
      const opened = await owner.fork('external-command')
      await loseCommitReceipt(afterCommit, () => owner.complete(opened.id))
      assert.equal(owner.exists(opened.id), false)
      assert.equal(owner.owned(opened.id), true, 'an unknown acknowledgement keeps the exact local observation')
      assert.deepEqual(owner.outstanding(), afterCommit ? [] : [opened.id])
      const recovered = controlledOwner(directory, 'ended-receipt-owner')
      assert.deepEqual(recovered.outstanding(), afterCommit ? [] : [opened.id])
      await owner.closeAll(0)
      assert.deepEqual(owner.outstanding(), [])
      assert.deepEqual(recovered.outstanding(), [])
      assert.equal(owner.owned(opened.id), false)
      assert.equal(owner.completions(), 1, 'replaying Ended must not publish the process exit twice')
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })
}

test('WHAT[managed-session-lifecycle-025] a durable reservation precedes Spawn and survives runtime reconstruction without replay', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-recovery-025-'))
  try {
    let spawns = 0
    const first = controlledOwner(directory, 'restart-owner', (id, kind) => {
      if (kind !== 'Spawn') return
      spawns += 1
      const blockers = Array.from(ownershipHost.ownershipBlockersDurable(directory, 'fork-pty:restart-owner', 'restart-owner'))
      assert.equal(blockers.length, 1)
      assert.equal(blockers[0].Lease.ResourceId, `pty:${id}`)
      assert.equal(blockers[0].Phase, 'Reserved')
    })
    const opened = await first.fork('long-running')
    assert.equal(opened.ok, true)
    const restarted = controlledOwner(directory, 'restart-owner', () => { spawns += 1 })
    assert.deepEqual(restarted.outstanding(), [opened.id])
    assert.equal(spawns, 1, 'reconstruction must not launch the interrupted reservation again')
    await assert.rejects(restarted.closeAll(0), /no.*local process binding|without a local process binding/i)
    assert.deepEqual(restarted.outstanding(), [opened.id])
    first.complete(opened.id)
    assert.deepEqual(restarted.outstanding(), [])
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[managed-session-lifecycle-025] ending one runtime scope does not close a successor scope in the same durable registry', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-pty-successor-025-'))
  try {
    const first = controlledOwner(directory, 'successor-owner')
    const old = await first.fork('old-command')
    first.complete(old.id)
    await first.detach()
    assert.equal((await first.fork('closed-owner')).ok, false)
    const successor = controlledOwner(directory, 'successor-owner')
    const next = await successor.fork('next-command')
    assert.equal(next.ok, true)
    assert.deepEqual(successor.outstanding(), [next.id])
    successor.complete(next.id)
    assert.deepEqual(successor.outstanding(), [])
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[managed-session-lifecycle-025] a fresh process sees a prior reservation and refuses to close or replay an unbound instance', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-cold-pty-025-'))
  const helper = fileURLToPath(new URL('./support/durable-pty-recovery.mjs', import.meta.url))
  const run = async action => JSON.parse((await promisify(execFile)(process.execPath, [helper, directory, action], { timeout: 10000 })).stdout)
  try {
    const first = await run('reserve')
    assert.equal(first.ok, true)
    assert.equal(first.dispatched, 1)
    const cold = await run('inspect')
    assert.deepEqual(cold.outstanding, [first.id])
    assert.equal(cold.dispatched, 0)
    assert.match(cold.closeError, /without a local process binding/)
    assert.deepEqual((await run('inspect')).outstanding, [first.id])
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[managed-session-lifecycle-025] a run drain rejects new local PTYs until exit and permits the next DevOps run', async () => {
  const owner = controlledOwner(undefined, 'repeat-run')
  const opened = await owner.fork('first-run')
  let finishExit
  owner.registerExit([opened.id, new Promise(resolve => { finishExit = resolve })])
  let finished = false
  const closing = owner.closeAll(1000).then(() => { finished = true })
  assert.equal((await owner.fork('during-close')).ok, false)
  assert.equal(finished, false)
  assert.deepEqual(owner.outstanding(), [opened.id])
  owner.complete(opened.id)
  finishExit()
  await closing
  const next = await owner.fork('next-run')
  assert.equal(next.ok, true)
  owner.complete(next.id)
})

test('WHAT[managed-session-lifecycle-025] concurrent terminal-name binding closes the losing PTY and waits for its exit', async () => {
  const result = await PtyToolSurface.nameRace()
  assert.equal(result.loserLiveBeforeExit, true)
  assert.equal(result.loserOwnedBeforeExit, true)
  assert.equal(result.outstandingBeforeExit, 2)
  assert.equal(result.bothReturnedBeforeExit, false)
  assert.equal(result.bindingErrors, 1)
  assert.equal(result.outstandingAfterExit, 1)
})

test('WHAT[managed-session-lifecycle-025] DevOps run terminal settlement closes every PTY the DevOps child owns at the port boundary', async () => {
  // Observable boundary: the PTY backend port is where owner-initiated cleanup
  // becomes a physical effect. The DevOps child's own PTY must receive that
  // terminate effect and be gone from its runtime; the engineer session keeps its
  // own PTY alive. Asserting the effect and the resulting counts — not which
  // helper issued it — keeps this test honest under internal refactors.
  const res = await devopsReturnDrain('devops-terminal-drain')
  assert.equal(res.ok, true, 'devops-return-drain scenario must succeed')

  assert.ok(res.devopsBefore.includes(res.devopsPtyId), 'DevOps must own a PTY before the run returns')
  assert.ok(res.engineerBefore.includes(res.engineerPtyId), 'Engineer must own a PTY before the DevOps run returns')

  assert.deepEqual(
    res.devopsAfter, [],
    'every PTY the DevOps child owns must be released when its run reaches its terminal settlement',
  )
  assert.ok(res.engineerAfter.includes(res.engineerPtyId), 'the engineer session PTY must survive the DevOps run settlement')

  const devopsTerminate = res.calls.filter(call => call.kind === 'signal' && call.signal === 'SIGTERM')
  assert.equal(
    devopsTerminate.length >= 1,
    true,
    'settling the DevOps run must terminate its owned PTY at the port boundary instead of leaving the process alive',
  )
})

test('WHAT[managed-session-lifecycle-025] fixed DevOps work return drains its PTY residue without affecting other runtimes', async () => {
  // Scenario 2: DevOps session holds an active PTY, another session (engineer) holds an active PTY.
  // When the DevOps run completes its terminal settlement on return, its PTYs are drained and cleared.
  // The engineer session PTY remains active and unaffected.
  const res = await devopsReturnDrain('devops-fixed-drain')
  assert.equal(res.ok, true, 'devops-return-drain scenario must succeed')

  // Before settlement: both DevOps and Engineer have outstanding PTYs
  assert.ok(res.devopsBefore.includes(res.devopsPtyId), 'DevOps must have outstanding PTY before return')
  assert.ok(res.engineerBefore.includes(res.engineerPtyId), 'Engineer must have outstanding PTY')

  // After DevOps run settlement: DevOps PTY is drained and cleared
  assert.equal(res.devopsAfter.length, 0, 'DevOps PTYs must be drained upon run return')
  assert.equal(res.devopsAfter.includes(res.devopsPtyId), false)

  // Unrelated Engineer PTY is untouched and remains alive
  assert.ok(res.engineerAfter.includes(res.engineerPtyId), 'Engineer PTY must not be affected by DevOps drain')
})

test('WHAT[managed-session-lifecycle-025] registered production terminal callback drains DevOps once and ignores a late callback after completion', async () => {
  const scopeMod = await import('../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js')
  const verify = scopeMod.verifyDevOpsReturnDrain || (scopeMod.ToolRuntimeScopeSurface && scopeMod.ToolRuntimeScopeSurface.verifyDevOpsReturnDrain)
  assert.equal(typeof verify, 'function', 'verifyDevOpsReturnDrain must be exported on ToolRuntimeScope surface')

  const res = await withAdmittedChildren('scope-devops-drain', 'manager-road-1', [
    { agentId: 'devops', sessionId: 'devops-session-1', role: 'devops' },
    { agentId: 'engineer-1', sessionId: 'engineer-session-1', role: 'engineer' },
  ], (journal, _profiles, _directory, _reopen, owner) => verify(journal, {
    managerSessionId: 'manager-road-1',
    devopsChildSessionId: 'devops-session-1',
    engineerChildSessionId: 'engineer-session-1',
    devopsPtys: ['devops-pty-1'],
    engineerPtys: ['eng-pty-1'],
    completeRole: 'devops',
    lateTerminal: true,
    admitNextRound: async () => {
      const seed = authority.issueInheritedIdentitySeed('devops', owner)
      assert.equal(seed.ok, true, seed.error)
      const sent = await dispatch.sendAgentOwnerRootAwait(hostPort(async () => dispatch.admittedWithReceipt('next-run')), journal, 'devops-session-1', 'NEXT', seed.value)
      assert.equal(sent.ok, true, JSON.stringify(sent.error))
      const accepted = await dispatch.acceptAgentOwnerRoot(journal, 'devops-session-1', sent.key, 'physical-next-run')
      assert.equal(accepted.ok, true, JSON.stringify(accepted.error))
    },
  }))

  assert.equal(res.devopsBefore.length, 1, 'DevOps child must have outstanding PTY before return')
  assert.equal(res.engineerBefore.length, 1, 'Engineer child must have outstanding PTY')
  assert.equal(res.devopsAfter.length, 0, 'DevOps child PTY must be drained on run complete via ToolRuntimeScope wiring')
  assert.deepEqual(res.engineerAfter, res.engineerBefore, 'Engineer child PTY must remain alive and unaffected')
  assert.equal(res.finishedAfterFirst, true)
  assert.equal(res.terminationsAfterFirst, 1, 'concurrent duplicate delivery must prepare only once')
  assert.equal(res.terminationsAfterRetry, 1, 'the disposed run callback must not kill a later PTY')
  assert.equal(res.devopsAfterRetry.length, 1)
  assert.deepEqual(res.lateProof, { forked: false, sent: false, seesNext: false, seesNextName: false })
})

for (const terminalKind of ['Aborted', 'Missing', 'Empty', 'ForeignRoot', 'ForeignChild', 'Failed']) {
  test(`WHAT[managed-session-lifecycle-025] registered ${terminalKind} terminal preserves eligibility and can receive a later valid terminal`, async () => {
    const { ToolRuntimeScopeSurface } = await import('../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js')
    const res = await withAdmittedChildren(`terminal-${terminalKind}`, 'manager-road-1', [
      { agentId: 'devops', sessionId: 'devops-session-1', role: 'devops' },
      { agentId: 'engineer-1', sessionId: 'engineer-session-1', role: 'engineer' },
    ], journal => ToolRuntimeScopeSurface.verifyDevOpsReturnDrain(journal, { terminalKind, followWithValid: true }))
    const validFailure = terminalKind === 'Failed'
    assert.equal(res.finishedAfterFirst, validFailure)
    assert.equal(res.terminationsAfterFirst, validFailure ? 1 : 0)
    assert.equal(res.devopsAfter.length, validFailure ? 0 : 1)
    assert.equal(res.finishedAfterRetry, true)
    assert.equal(res.terminationsAfterRetry, 1)
    assert.equal(res.devopsAfterRetry.length, 0)
    assert.deepEqual(res.engineerAfter, res.engineerBefore)
  })
}

test('WHAT[managed-session-lifecycle-025] a cold DevOps terminal cannot forget durable PTYs without local physical bindings', async () => {
  const { ToolRuntimeScopeSurface } = await import('../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js')
  await withAdmittedChildren('cold-terminal-pty', 'manager-road-1', [
    { agentId: 'devops', sessionId: 'devops-session-1', role: 'devops' },
  ], async (journal, profiles, directory) => {
    const child = 'devops-session-1'
    const registry = `fork-pty:${child}`
    ownershipHost.executeOwnershipDurable(directory, registry, 'open-cold', { type: 'OpenScope', scopeId: child, parentId: null })
    ownershipHost.executeOwnershipDurable(directory, registry, 'reserve-cold', { type: 'Reserve', resourceId: 'pty:cold', scopeId: child, kind: 'process' })
    const result = await ToolRuntimeScopeSurface.coldDevOpsReturn(journal, directory)
    assert.match(result.failure, /no local process binding/)
    assert.equal(result.finished, false)
    assert.equal(result.delivered, false)
    const blockers = Array.from(ownershipHost.ownershipBlockersDurable(directory, registry, child))
    assert.equal(blockers.length, 1)
    assert.equal(blockers[0].Lease.ResourceId, 'pty:cold')
  })
})

test('WHAT[managed-session-lifecycle-025] a DevOps terminal cannot silently bypass a missing round-drain capability', async () => {
  const { ToolRuntimeScopeSurface } = await import('../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js')
  await withAdmittedChildren('missing-drain-capability', 'manager-road-1', [
    { agentId: 'devops', sessionId: 'devops-session-1', role: 'devops' },
  ], async journal => {
    const result = await ToolRuntimeScopeSurface.undrainedDevOpsReturn(journal)
    assert.equal(result.finished, false)
    assert.match(result.failure, /DevOps PTY round drain is unavailable/)
  })
})

test('WHAT[managed-session-lifecycle-025] a cold return with zero known PTYs still durably stops admission for the exact round', async () => {
  const { ToolRuntimeScopeSurface } = await import('../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js')
  await withSharedAdmittedChildren('cold-empty-round', 'manager-road-1', [
    { agentId: 'devops', sessionId: 'devops-session-1', role: 'devops' },
  ], async (journal, profiles, workspace) => {
    const result = await ToolRuntimeScopeSurface.coldDevOpsReturn(journal, workspace)
    assert.equal(result.finished, true, result.failure)
    const registry = 'fork-pty:devops-session-1'
    const database = new DatabaseSync(join(workspace, '.git', 'wanxiangshu', 'work.sqlite'))
    try {
      const records = database.prepare('SELECT body FROM journal_entries WHERE stream=? ORDER BY revision').all(`ownership:${registry}`)
        .map(row => new TextDecoder().decode(row.body))
      const restored = new Ownership(registry, records)
      const run = `devops-session-1:run:${profiles.get('devops').logicalRun}`
      assert.equal(restored.scope(run).phase, 'Draining')
      assert.deepEqual(restored.blockers(run), [])
      assert.equal(restored.scope('devops-session-1').phase, 'Open', 'the reusable session must stay open')
      assert.throws(() => ownershipHost.executeOwnershipDurable(join(workspace, '.git'), registry, 'late-cold-reserve', {
        type: 'Reserve', scopeId: run, resourceId: 'pty:late-cold', kind: 'process',
      }), error => error.message === 'Scope admission is closed')
    } finally {
      database.close()
    }
  })
})

test('WHAT[managed-session-lifecycle-025] real bun-pty TERM waits for exit and escalates to KILL before releasing ownership', async t => {
  if (spawnSync('bun', ['--version'], { encoding: 'utf8' }).status !== 0) {
    t.skip('Bun is required to run the production bun-pty backend')
    return
  }
  const directory = mkdtempSync(join(tmpdir(), 'wxs-real-pty-025-'))
  let completed = false
  t.after(() => {
    const pidFile = join(directory, 'pid')
    if (!completed && existsSync(pidFile)) {
      const pid = Number(readFileSync(pidFile, 'utf8'))
      if (Number.isInteger(pid) && pid > 1) {
        try { process.kill(-pid, 'SIGKILL') } catch (error) {
          if (error.code !== 'ESRCH') throw error
        }
      }
    }
    rmSync(directory, { recursive: true, force: true })
  })
  const helper = fileURLToPath(new URL('./support/real-pty-drain.mjs', import.meta.url))
  const { stdout } = await promisify(execFile)('bun', [helper, directory], { timeout: 15000 })
  const evidence = JSON.parse(stdout.trim())
  assert.equal(evidence.termObserved, true)
  assert.equal(evidence.processExited, true)
  assert.deepEqual(evidence.blockers, [])
  completed = true
})

for (const mode of [
  'ended-before', 'ended-after', 'shared-detach', 'shared-cancel',
  'cancel-close-before', 'cancel-close-after',
  'cancel-closeAll-before', 'cancel-closeAll-after',
  'cancel-detach-before', 'cancel-detach-after',
  'cancel-cancel-before', 'cancel-cancel-after',
  'leader-exit', 'leader-natural', 'owner-lost',
]) {
  test(`WHAT[managed-session-lifecycle-025] real bun-pty ${mode} preserves ownership until scope exit is known`, async t => {
    if (spawnSync('bun', ['--version'], { encoding: 'utf8' }).status !== 0) {
      t.skip('Bun is required to run the production bun-pty backend')
      return
    }
    const directory = mkdtempSync(join(tmpdir(), 'wxs-real-owner-025-'))
    let completed = false
    t.after(() => {
      if (!completed) {
        for (const label of ['first', 'second']) {
          const marker = join(directory, `${label}-pid`)
          if (!existsSync(marker)) continue
          const pid = Number(readFileSync(marker, 'utf8'))
          if (!Number.isInteger(pid) || pid <= 1) continue
          try { process.kill(-pid, 'SIGKILL') } catch (error) {
            if (error.code !== 'ESRCH') throw error
          }
        }
      }
      rmSync(directory, { recursive: true, force: true })
    })
    const helper = fileURLToPath(new URL('./support/real-pty-ownership.mjs', import.meta.url))
    const { stdout } = await promisify(execFile)('bun', [helper, directory, mode], { timeout: 20000 })
    completed = true
    const evidence = JSON.parse(stdout.trim())
    if (mode === 'owner-lost') {
      assert.equal(evidence.ownerLost, true)
      assert.equal(evidence.unknownRetained, true)
      assert.equal(evidence.blockers.length, 1)
      return
    }
    assert.deepEqual(evidence.blockers, [])
    if (mode === 'leader-exit' || mode === 'leader-natural') {
      assert.equal(evidence.leaderExited, true)
      assert.equal(evidence.retainedBeforeClose, true)
      assert.equal(evidence.descendantAlive, false, 'physical cleanup must include the live same-group descendant')
    } else if (mode.startsWith('shared-')) {
      assert.equal(evidence.firstExited, true)
      assert.equal(evidence.otherSurvived, true)
    } else {
      assert.equal(evidence.injected, true)
      assert.equal(evidence.processExited, true)
      if (mode.startsWith('cancel-')) assert.equal(evidence.failureReported, true)
    }
  })
}

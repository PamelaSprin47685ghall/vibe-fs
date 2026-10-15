import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { execFile, spawnSync } from 'node:child_process'
import { promisify } from 'node:util'
import { fileURLToPath } from 'node:url'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as PtySurface from '../../../dist/Execution/Delegation/Fork/Host/HostForkPtySurface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as PtyToolSurface from '../../../dist/OpenCode/Tools/PtyToolSurface.js'
import { DatabaseSync } from 'node:sqlite'
import { withAdmittedChildren } from './support/admitted-child-work.mjs'

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
    DatabaseSync.prototype.exec = original
    await owner.closeAll(0)
    assert.equal(owner.owned(opened.id), false)
    assert.deepEqual(owner.outstanding(), [])
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

test('WHAT[managed-session-lifecycle-025] ToolRuntimeScope production wiring drains DevOps child PTYs on run complete', async () => {
  const scopeMod = await import('../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js')
  const verify = scopeMod.verifyDevOpsReturnDrain || (scopeMod.ToolRuntimeScopeSurface && scopeMod.ToolRuntimeScopeSurface.verifyDevOpsReturnDrain)
  assert.equal(typeof verify, 'function', 'verifyDevOpsReturnDrain must be exported on ToolRuntimeScope surface')

  const res = await withAdmittedChildren('scope-devops-drain', 'manager-road-1', [
    { agentId: 'devops', sessionId: 'devops-session-1', role: 'devops' },
    { agentId: 'engineer-1', sessionId: 'engineer-session-1', role: 'engineer' },
  ], journal => verify(journal, {
    managerSessionId: 'manager-road-1',
    devopsChildSessionId: 'devops-session-1',
    engineerChildSessionId: 'engineer-session-1',
    devopsPtys: ['devops-pty-1'],
    engineerPtys: ['eng-pty-1'],
    completeRole: 'devops',
  }))

  assert.equal(res.devopsBefore.length, 1, 'DevOps child must have outstanding PTY before return')
  assert.equal(res.engineerBefore.length, 1, 'Engineer child must have outstanding PTY')
  assert.equal(res.devopsAfter.length, 0, 'DevOps child PTY must be drained on run complete via ToolRuntimeScope wiring')
  assert.deepEqual(res.engineerAfter, res.engineerBefore, 'Engineer child PTY must remain alive and unaffected')
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

for (const mode of ['ended-before', 'ended-after', 'shared-detach', 'shared-cancel']) {
  test(`WHAT[managed-session-lifecycle-025] real bun-pty ${mode} preserves ownership and confirms physical exit`, async t => {
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
    const evidence = JSON.parse(stdout.trim())
    assert.deepEqual(evidence.blockers, [])
    if (mode.startsWith('shared-')) {
      assert.equal(evidence.firstExited, true)
      assert.equal(evidence.otherSurvived, true)
    } else {
      assert.equal(evidence.injected, true)
      assert.equal(evidence.processExited, true)
    }
    completed = true
  })
}

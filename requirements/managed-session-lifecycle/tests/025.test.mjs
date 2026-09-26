import assert from 'node:assert/strict'
import test from 'node:test'
import * as PtySurface from '../../../dist/Execution/Delegation/Fork/Host/HostForkPtySurface.js'

test('WHAT[managed-session-lifecycle-025] PTY physical exit immediately clears HostForkRuntime ptyRuns and terminalByName bookkeeping', async () => {
  // Scenario 1: fork a PTY, bind terminal name, assert outstanding before exit,
  // deliver physical backend exit (port.Complete), and assert bookkeeping is immediately cleared
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

test('WHAT[managed-session-lifecycle-025] fixed DevOps work return drains its PTY residue without affecting other runtimes', async () => {
  // Scenario 2: DevOps session holds an active PTY, another session (engineer) holds an active PTY.
  // When the DevOps run completes its terminal settlement on return, its PTYs are drained and cleared.
  // The engineer session PTY remains active and unaffected.
  const res = await PtySurface.scenario('devops-return-drain', '', '')
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

  const res = await verify({
    managerSessionId: 'manager-road-1',
    devopsChildSessionId: 'devops-session-1',
    engineerChildSessionId: 'engineer-session-1',
    devopsPtys: ['devops-pty-1'],
    engineerPtys: ['eng-pty-1'],
    completeRole: 'devops',
  })

  assert.ok(res.devopsBefore.includes('devops-pty-1'), 'DevOps child must have outstanding PTY before return')
  assert.ok(res.engineerBefore.includes('eng-pty-1'), 'Engineer child must have outstanding PTY')
  assert.equal(res.devopsAfter.length, 0, 'DevOps child PTY must be drained on run complete via ToolRuntimeScope wiring')
  assert.ok(res.engineerAfter.includes('eng-pty-1'), 'Engineer child PTY must remain alive and unaffected')
})

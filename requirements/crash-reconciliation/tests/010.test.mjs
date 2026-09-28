import assert from 'node:assert/strict'
import test from 'node:test'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'

test('WHAT[crash-reconciliation-010] child resolution distinguishes active incomplete blocked terminal and abandoned', () => {
  for (const [durable, snapshot, observations, body, expected] of [
    ['active', 'active', ['active'], '', 'RecoveredActive'],
    ['active', 'unreadable', [], '', 'RecoveryIncomplete'],
    ['active', 'missing', ['aborted:transport', 'restore'], '', 'RecoveryIncomplete'],
    ['retired', 'missing', [], '', 'RecoveryBlocked'],
    ['active', 'terminal', [], '', 'RecoveryBlocked'],
    ['active', 'terminal', [], 'done', 'RecoveredTerminal'],
    ['abandoned', 'missing', [], '', 'RecoveredAbandoned'],
    ['active', 'missing', ['parent-cancelled'], '', 'RecoveredAbandoned'],
  ]) assert.equal(child.resolve(durable, snapshot, observations, body).result, expected)
})

test('WHAT[crash-reconciliation-010] handle and job family mappings preserve every recovery result branch', () => {
  for (const [branch, expected] of [['none', 'NoRecoveryRequired'], ['recovered', 'Recovered'], ['waiting', 'Waiting'], ['blocked', 'Blocked']]) {
    assert.equal(recovery.handleFamily(branch).state, expected)
    assert.equal(recovery.jobFamily(branch).state, expected)
  }
  assert.deepEqual(recovery.handleFamily('recovered').restoredHandles, ['h1'])
  assert.ok(recovery.handleFamily('waiting').reason.length > 0)
  assert.ok(recovery.handleFamily('blocked').reason.length > 0)
})

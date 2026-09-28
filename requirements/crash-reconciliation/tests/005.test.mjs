import assert from 'node:assert/strict'
import test from 'node:test'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'

test('WHAT[crash-reconciliation-005] missing unreadable and blank terminal evidence never produce readiness', () => {
  assert.equal(child.resolve('active', 'unreadable', [], '').result, 'RecoveryIncomplete')
  assert.equal(child.resolve('active', 'missing', [], '').result, 'RecoveryIncomplete')
  assert.equal(child.resolve('active', 'terminal', [], '').result, 'RecoveryBlocked')
  assert.equal(child.resolve('retired', 'missing', [], '').result, 'RecoveryBlocked')
})

test('WHAT[crash-reconciliation-005] family authorization preserves waiting and blocked outcomes', () => {
  for (const [input, expected] of [['Waiting', 'FamilyWaiting'], ['Blocked', 'FamilyBlocked']]) {
    assert.equal(recovery.authorize('parent', 1, [{ session: 'child', state: input }]).state, expected)
  }
  assert.equal(recovery.handleFamily('waiting').state, 'Waiting')
  assert.equal(recovery.jobFamily('waiting').state, 'Waiting')
})

test.todo('WHAT[crash-reconciliation-005] actual Waiting consumers cannot send publish completion or enter Ready effects while physical evidence is pending (GAP-149)')

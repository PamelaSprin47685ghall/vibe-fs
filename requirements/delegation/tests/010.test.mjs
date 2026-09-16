import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncDelegate from '../../../dist/Execution/Delegation/SyncDelegateSurface.js'

test('WHAT[DELEG-010] EXEC_026_agentNameFor_returns_bare_inspector_coder', () => {
  assert.equal(syncDelegate.agentNameFor('Inspector'), 'inspector')
  assert.equal(syncDelegate.agentNameFor('Coder'), 'coder')
})

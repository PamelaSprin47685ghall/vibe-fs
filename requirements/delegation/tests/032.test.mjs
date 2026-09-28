import assert from 'node:assert/strict'
import test from 'node:test'
import { permissions, isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

test('WHAT[delegation-032] office capability projection denies cross-role dispatch while retaining each role’s own work', () => {
  for (const [role, denied, allowed] of [
    ['engineer', ['Fork', 'Resume', 'Exec', 'Pty', 'Join', 'Horizon'], ['Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'BashHoneypot', 'Fission']],
    ['devops', ['Fork', 'Resume', 'Fission'], ['Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'Exec', 'Pty', 'Join', 'Horizon']],
  ]) {
    const actual = permissions(role)
    for (const capability of denied) {
      assert.equal(actual.includes(capability), false)
      assert.equal(isAllowed(role, capability), false)
    }
    for (const capability of allowed) {
      assert.equal(actual.includes(capability), true)
      assert.equal(isAllowed(role, capability), true)
    }
  }
})

test('WHAT[delegation-032] SyncDelegate selects standard Engineer capabilities without DevOps authority', () => {
  const vocabulary = sync.vocabulary('Engineer', 'Fast', 'sphinx-scope')
  assert.equal(vocabulary.role, 'engineer')
  assert.equal(vocabulary.agent, 'engineer')
  assert.equal(vocabulary.scope, 'sphinx-scope')
  assert.equal(isAllowed(vocabulary.agent, 'Write'), true)
  assert.equal(isAllowed(vocabulary.agent, 'Fission'), true)
  assert.equal(isAllowed(vocabulary.agent, 'Exec'), false)
})

test.todo('WHAT[delegation-032] actual direct, wrapped and forwarded requests cannot delegate across Engineer and DevOps boundaries (GAP-153)')

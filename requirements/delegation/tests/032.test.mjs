// requirements/delegation/tests/032.test.mjs
//
// WHAT[delegation-032] — Engineer completes its entrusted charge and returns directly
// to Manager without organizing a verification chain or dispatching DevOps.
// Direct, wrapped, or forwarded delegation from Engineer to DevOps is forbidden,
// and Sphinx internal SyncDelegate invokes a standard Engineer without granting DevOps privileges.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as fork from '../../../dist/Execution/Delegation/Fork/Surface.js'
import { permissions as officePermissions, isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[delegation-032] engineer_has_no_devops_delegation_or_execution_dispatch_tools', () => {
  // Engineer only returns to Manager, never delegates to DevOps
  const deniedCalling = fork.unavailableCalling('en', false)
  assert.match(deniedCalling, /Unknown or unavailable calling/)
})

test('WHAT[delegation-032] sync_delegate_in_sphinx_uses_the_canonical_engineer_not_a_special_readonly_role', () => {
  // SyncDelegate vocabulary for internal Engineer investigation
  const vocab = sync.vocabulary('Engineer', 'Fast', 'sphinx-scope')
  assert.equal(vocab.role, 'engineer')
  assert.equal(vocab.agent, 'engineer')
  assert.equal(vocab.scope, 'sphinx-scope')
  assert.equal(isAllowed(vocab.agent, 'Write'), true)
  assert.equal(isAllowed(vocab.agent, 'Fission'), true)
  assert.equal(isAllowed(vocab.agent, 'Exec'), false)
})

test('WHAT[delegation-032] engineer_and_devops_mutual_delegation_and_command_isolation_in_permissions_and_schema', () => {
  // 1. Engineer 权限包含 Read/Write/Edit/Glob/Grep/Move/Remove/BashHoneypot/Fission
  const engPerms = officePermissions('engineer')
  for (const expected of ['Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'BashHoneypot', 'Fission']) {
    assert.ok(engPerms.includes(expected), `Engineer must have ${expected}`)
    assert.equal(isAllowed('engineer', expected), true, `Engineer isAllowed ${expected} must be true`)
  }

  // 且绝不包含 Fork/Resume/Exec/Pty/Join/Horizon
  for (const forbidden of ['Fork', 'Resume', 'Exec', 'Pty', 'Join', 'Horizon']) {
    assert.equal(engPerms.includes(forbidden), false, `Engineer must not have ${forbidden}`)
    assert.equal(isAllowed('engineer', forbidden), false, `Engineer isAllowed ${forbidden} must be false`)
  }

  // 2. DevOps 权限包含 Exec/Pty/Join/Horizon 且绝不包含 Fork/Resume/Fission
  const devopsPerms = officePermissions('devops')
  for (const expected of ['Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'Exec', 'Pty', 'Join', 'Horizon']) {
    assert.ok(devopsPerms.includes(expected), `DevOps must have ${expected}`)
    assert.equal(isAllowed('devops', expected), true, `DevOps isAllowed ${expected} must be true`)
  }

  for (const forbidden of ['Fork', 'Resume', 'Fission']) {
    assert.equal(devopsPerms.includes(forbidden), false, `DevOps must not have ${forbidden}`)
    assert.equal(isAllowed('devops', forbidden), false, `DevOps isAllowed ${forbidden} must be false`)
  }

  // 3. permissionObj 双向 deny 断言
  // Engineer 严禁 fork, resume, run, terminal
  assert.equal(engPerms.includes('Fork'), false, 'Engineer must deny fork')
  assert.equal(engPerms.includes('Resume'), false, 'Engineer must deny resume')
  assert.equal(engPerms.includes('Exec'), false, 'Engineer must deny run')
  assert.equal(engPerms.includes('Pty'), false, 'Engineer must deny open-terminal')

  // DevOps 严禁 fork, resume, fission
  assert.equal(devopsPerms.includes('Fork'), false, 'DevOps must deny fork')
  assert.equal(devopsPerms.includes('Resume'), false, 'DevOps must deny resume')
  assert.equal(devopsPerms.includes('Fission'), false, 'DevOps must deny fission')

  // 相对的合法权限
  assert.equal(engPerms.includes('Fission'), true, 'Engineer must allow fission')
  assert.equal(devopsPerms.includes('Exec'), true, 'DevOps must allow run')
  assert.equal(devopsPerms.includes('Pty'), true, 'DevOps must allow open-terminal')
})

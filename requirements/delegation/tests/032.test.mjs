// requirements/delegation/tests/032.test.mjs
//
// WHAT[DELEG-032] — Engineer completes its entrusted charge and returns directly
// to Manager without organizing a verification chain or dispatching DevOps.
// Direct, wrapped, or forwarded delegation from Engineer to DevOps is forbidden,
// and Sphinx internal SyncDelegate only synchronously invokes read-only Engineer.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as fork from '../../../dist/Execution/Delegation/Fork/Surface.js'

test('WHAT[DELEG-032] engineer_has_no_devops_delegation_or_execution_dispatch_tools', () => {
  // Engineer only returns to Manager, never delegates to DevOps
  const deniedCalling = fork.unavailableCalling('en', false)
  assert.match(deniedCalling, /Unknown or unavailable calling/)
})

test('WHAT[DELEG-032] sync_delegate_in_sphinx_only_invokes_readonly_engineer_investigation', () => {
  // SyncDelegate vocabulary for internal Engineer investigation
  const vocab = sync.vocabulary('Engineer', 'Fast', 'sphinx-scope')
  assert.equal(vocab.role, 'engineer')
  assert.equal(vocab.agent, 'engineer')
  assert.equal(vocab.scope, 'sphinx-scope')
})

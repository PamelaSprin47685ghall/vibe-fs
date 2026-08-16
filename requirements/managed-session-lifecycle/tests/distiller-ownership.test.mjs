import assert from 'node:assert/strict'
import test from 'node:test'
import { distillerLifecycle } from './support/managed-surface.mjs'

test('WHAT[MANAGED-SESSION-010] EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible', () => {
  const observed = distillerLifecycle()
  assert.equal(observed.ok, true)
  assert.equal(observed.ownership, 'HostOwnedHidden')
  assert.equal(observed.linked, 1)
  assert.equal(observed.listable, 0)
})

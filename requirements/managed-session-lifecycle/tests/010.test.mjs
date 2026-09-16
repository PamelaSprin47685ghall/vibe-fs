import assert from 'node:assert/strict'
import test from 'node:test'
import * as distiller from '../../../dist/Execution/Session/DistillerOwnershipSurface.js'

test('WHAT[MANAGED-SESSION-010] EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible', () => {
  assert.equal(distiller.testDistillerHidden(), true)
})

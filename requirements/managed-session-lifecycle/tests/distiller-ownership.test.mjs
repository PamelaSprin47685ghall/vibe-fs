import assert from 'node:assert/strict'
import test from 'node:test'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'
import * as DistillationSurface from '../../../dist/OpenCode/Tools/DistillationSurface.js'

test('WHAT[MANAGED-SESSION-010] EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible', () => {
  assert.equal(DistillationSurface.contract.internalRuntime, true)
  assert.equal(DistillationSurface.contract.publicTarget, false)

  const linked = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link',
    handle: 'agent:distiller',
    child: 'ses_distiller_1',
    agent: DistillationSurface.contract.managedAgent,
    role: DistillationSurface.contract.handleRole,
    ownership: 'HostOwnedHidden',
  })

  assert.equal(linked.ok, true)
  assert.equal(HandleSurface.linkedChildren(linked.state).length, 1)
  assert.deepEqual(HandleSurface.views(linked.state).listable, [])
})

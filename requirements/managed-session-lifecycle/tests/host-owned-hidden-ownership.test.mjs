import assert from 'node:assert/strict'
import test from 'node:test'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'

test('WHAT[MANAGED-SESSION-010] EXEC_014_host_owned_hidden_child_is_parent_invisible', () => {
  const linked = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link',
    handle: 'agent:executor',
    child: 'ses_executor_1',
    agent: 'executor',
    role: 'DevOps',
    ownership: 'HostOwnedHidden',
  })

  assert.equal(linked.ok, true)
  assert.equal(HandleSurface.linkedChildren(linked.state).length, 1)
  assert.deepEqual(HandleSurface.views(linked.state).listable, [], 'HostOwnedHidden children never enter the parent list')
  assert.equal(HandleSurface.read(linked.state, 'agent:executor').child, 'ses_executor_1')
})

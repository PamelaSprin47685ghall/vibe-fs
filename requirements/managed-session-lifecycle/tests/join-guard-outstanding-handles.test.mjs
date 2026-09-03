import assert from 'node:assert/strict'
import test from 'node:test'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'

const linkedProjection = () => {
  const linked = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link', handle: 'agent:child-1', child: 'ses_child', agent: 'coder', role: 'Coder',
  })
  assert.equal(linked.ok, true)
  return linked.state
}

test('WHAT[MANAGED-SESSION-006] EXEC_016_listable_handles_are_outstanding_for_manager', () => {
  const projection = linkedProjection()
  assert.deepEqual(HandleSurface.views(projection), { listable: ['agent:child-1'], joinable: [], active: ['agent:child-1'] })
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'

const link = (projection, agentId, child, targetAgent = 'coder') => {
  const result = HandleSurface.apply(projection, {
    op: 'link', handle: `agent:${agentId}`, child, agent: targetAgent, role: 'Coder',
  })
  assert.equal(result.ok, true)
  return result.state
}

test('WHAT[MANAGED-SESSION-008] EXEC_009_consume_abandoned_writes_HandleRetired_second_AlreadyRetired', () => {
  let projection = link(HandleSurface.empty(), 'h1', 'ses_c')
  projection = HandleSurface.apply(projection, { op: 'abandon', handle: 'agent:h1', reason: 'ParentCancelled' }).state
  assert.equal(HandleSurface.reportableAbandonedCount(projection), 1)
  const consumed = HandleSurface.apply(projection, { op: 'retire', handle: 'agent:h1' })
  assert.equal(consumed.ok, true)
  projection = consumed.state
  assert.equal(HandleSurface.isRetired(projection, 'agent:h1'), true)
  assert.equal(HandleSurface.reportableAbandonedCount(projection), 0)
  assert.deepEqual(HandleSurface.apply(projection, { op: 'retire', handle: 'agent:h1' }).error, { kind: 'TransitionRejected', reason: 'HandleIsRetired' })
})

test('WHAT[MANAGED-SESSION-015] EXEC_018_creation_order_follows_HandleLinked_fold_sequence', () => {
  const projection = link(link(link(HandleSurface.empty(), 'later-id-zzz', 'ses_z', 'zebra-agent'), 'earlier-id-aaa', 'ses_a', 'alpha-agent'), 'mid-id-mmm', 'ses_m', 'mid-agent')
  const children = HandleSurface.linkedChildren(projection)
  assert.equal(children.find((item) => item.handle === 'agent:later-id-zzz').creationOrder, 0)
  assert.equal(children.find((item) => item.handle === 'agent:earlier-id-aaa').creationOrder, 1)
  assert.equal(children.find((item) => item.handle === 'agent:mid-id-mmm').creationOrder, 2)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_abandoned_retire_clears_reportable_single_report', () => {
  let projection = link(HandleSurface.empty(), 'h1', 'ses_c')
  projection = HandleSurface.apply(projection, { op: 'abandon', handle: 'agent:h1', reason: 'ParentCancelled' }).state
  assert.equal(HandleSurface.reportableAbandonedCount(projection), 1)
  projection = HandleSurface.apply(projection, { op: 'retire', handle: 'agent:h1' }).state
  assert.equal(HandleSurface.reportableAbandonedCount(projection), 0)
  assert.equal(HandleSurface.isRetired(projection, 'agent:h1'), true)
})

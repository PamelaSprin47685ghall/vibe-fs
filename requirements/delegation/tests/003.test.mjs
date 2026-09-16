import assert from 'node:assert/strict'
import test from 'node:test'
import * as forkTool from '../../../dist/Execution/Delegation/ForkToolSurface.js'

test('WHAT[DELEG-003] FORK_road_with_calling_is_independent_and_omitted_calling_continues_byname', () => {
  const res1 = forkTool.validateParams({ road: 'R1', calling: 'coder' })
  assert.equal(res1.isIndependentRoad, true)
  const res2 = forkTool.validateParams({ byname: 'agent-1' })
  assert.equal(res2.isContinuation, true)
})

test('WHAT[DELEG-003] FORK_TOOL_requires_calling_and_resume_rejects_calling', () => {
  assert.throws(() => forkTool.fork({ calling: null }), /calling required for fork/)
  assert.throws(() => forkTool.resume({ byname: 'b1', calling: 'coder' }), /calling forbidden for resume/)
})

// Join completion type consequence through JoinSurface and HandleSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

test('WHAT[DELEG-015] JOIN_COMPLETION_failed_is_rendered_as_agent_failed', () => {
  const wire = join.renderBatch('english', [{ kind: 'failed', agentId: 'a1', agentName: 'Ada', role: 'Coder', runId: 'run-a1', code: 'E', message: 'no' }])
  assert.match(wire, /could not complete/)
  assert.doesNotMatch(wire, /has returned/)
})
test('WHAT[DELEG-015] JOIN_COMPLETION_terminal_projection_is_joinable_until_retired', () => {
  assert.equal(handles.crashScenario('completed').joinable, 1)
  assert.equal(handles.crashScenario('retired').joinable, 0)
})
test('WHAT[DELEG-013] JOIN_COMPLETION_completed_is_rendered_as_entry_local_work_record', () => {
  const wire = join.renderBatch('english', [
    {
      kind: 'completed',
      agentId: 'a1',
      agentName: 'Ada',
      role: 'Coder',
      runId: 'run-a1',
      workRecord: 'Task completed with verifiable evidence.'
    }
  ])
  assert.match(wire, /Task completed with verifiable evidence/)
  assert.match(wire, /has returned/)
})
test('WHAT[DELEG-013] JOIN_COMPLETION_abandoned_is_rendered_as_agent_did_not_return', () => {
  const wire = join.renderBatch('english', [
    {
      kind: 'abandoned',
      agentId: 'a2',
      agentName: 'Bob',
      role: 'DevOps',
      reason: 'ParentCancelled'
    }
  ])
  assert.match(wire, /did not return/)
})

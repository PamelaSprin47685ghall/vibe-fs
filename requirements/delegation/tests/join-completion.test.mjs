// Join completion type consequence through JoinSurface and HandleSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

test('WHAT[DELEG-015] JOIN_COMPLETION_failed_is_rendered_as_agent_failed', () => {
  const wire = join.renderBatch('english', [{ kind: 'failed', agentId: 'a1', agentName: 'Ada', code: 'E', message: 'no' }])
  assert.match(wire, /could not complete/)
  assert.doesNotMatch(wire, /has returned/)
})
test('WHAT[DELEG-015] JOIN_COMPLETION_terminal_projection_is_joinable_until_retired', () => {
  assert.equal(handles.crashScenario('completed').joinable, 1)
  assert.equal(handles.crashScenario('retired').joinable, 0)
})

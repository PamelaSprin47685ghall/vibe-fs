// JoinTool outcomes through the delegation-owned JoinSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const text = (items) => join.renderBatch('english', items)
test('WHAT[DELEG-019] JOIN_TOOL_completed_agent_is_natural_language', () => {
  const wire = text([{ kind: 'completed', agentId: 'a1', agentName: 'Ada', role: 'Coder', runId: 'run-a1', workRecord: 'done' }])
  assert.match(wire, /Ada has returned/)
  assert.match(wire, /done/)
  assert.doesNotMatch(wire, /\bstatus\s*=/)
})
test('WHAT[DELEG-019] JOIN_TOOL_failed_agent_preserves_failure_message', () => {
  const wire = text([{ kind: 'failed', agentId: 'a1', agentName: 'Ada', role: 'Coder', runId: 'run-a1', code: 'E1', message: 'broken' }])
  assert.match(wire, /Ada could not complete/)
  assert.match(wire, /broken/)
})
test('WHAT[DELEG-019] JOIN_TOOL_abandoned_agent_is_not_completed', () => {
  const wire = text([{ kind: 'abandoned', agentId: 'a1', agentName: 'Ada', reason: 'cancelled' }])
  assert.match(wire, /did not return/)
  assert.doesNotMatch(wire, /has returned/)
})
test('WHAT[DELEG-019] JOIN_TOOL_empty_and_errors_are_natural_language', () => {
  assert.match(join.renderForkError('english', 'Empty'), /nothing away to receive/)
  assert.match(join.renderForkError('english', 'NotFound'), /No one by that name/)
  assert.match(join.renderInterrupted('english', 'OperatorAbort'), /waiting was interrupted/)
})
test('WHAT[DELEG-019] JOIN_TOOL_pty_outcomes_are_distinct', () => {
  const wire = text([{ kind: 'pty-aborted', ptyId: 'p1', terminalLabel: 'watch', outcome: 'abort', message: 'stop' }])
  assert.match(wire, /watch was interrupted/)
  assert.match(wire, /stop/)
})

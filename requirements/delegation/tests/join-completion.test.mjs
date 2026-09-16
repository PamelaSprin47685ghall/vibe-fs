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

test('WHAT[DELEG-014] JOIN_COMPLETION_batch_preserves_order_and_bounded_work_records', () => {
  const wire = join.renderBatch('english', [
    { kind: 'completed', agentId: 'a1', agentName: 'Ada', role: 'Coder', runId: 'run-a1', workRecord: 'First evidence.' },
    { kind: 'completed', agentId: 'a2', agentName: 'Bob', role: 'DevOps', runId: 'run-a2', workRecord: 'Second evidence.' }
  ])
  assert.match(wire, /Ada has returned/)
  assert.match(wire, /Bob has returned/)
  assert.ok(wire.indexOf('Ada') < wire.indexOf('Bob'))
  assert.match(wire, /First evidence/)
  assert.match(wire, /Second evidence/)
  assert.doesNotMatch(wire, /\[\[result\]\]|\[error\]|work_record\s*=/)
})

test('WHAT[DELEG-015] JOIN_COMPLETION_interrupted_is_not_fork_error', () => {
  const wireUser = join.renderInterrupted('english', 'UserMessageArrived')
  assert.match(wireUser, /Something nearer has arrived/)
  assert.doesNotMatch(wireUser, /error|failed/i)

  const wireDeadline = join.renderInterrupted('english', 'DeadlineExpired')
  assert.match(wireDeadline, /waiting ended/)
  assert.doesNotMatch(wireDeadline, /operator/i)

  const wireAbort = join.renderInterrupted('english', 'OperatorAbort')
  assert.match(wireAbort, /waiting was interrupted/)
})

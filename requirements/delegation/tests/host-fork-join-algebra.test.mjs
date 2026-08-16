// Host join algebra is observable through the delegation JoinSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

test('WHAT[DELEG-019] HOST_JOIN_empty_is_nothing_to_join', () => {
  assert.match(join.renderForkError('english', 'NothingToJoin'), /nothing away to receive/)
})
test('WHAT[DELEG-019] HOST_JOIN_interrupted_reason_is_not_cancelled', () => {
  const wire = join.renderInterrupted('english', 'UserMessageArrived')
  assert.match(wire, /Something nearer has arrived/)
  assert.doesNotMatch(wire, /cancelled/i)
})
test('WHAT[DELEG-019] HOST_JOIN_deadline_is_distinct_from_operator_abort', () => {
  const wire = join.renderInterrupted('english', 'DeadlineExpired')
  assert.match(wire, /waiting ended/)
  assert.doesNotMatch(wire, /operator/i)
})
test('WHAT[DELEG-019] HOST_JOIN_cancelled_is_explicit', () => {
  assert.match(join.renderForkError('english', 'Cancelled'), /wait was cancelled/)
})
test('WHAT[DELEG-019] HOST_JOIN_unknown_agent_is_not_found', () => {
  assert.match(join.renderForkError('english', 'NotFound'), /No one by that name/)
})

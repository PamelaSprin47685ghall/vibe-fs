// Fork tool payload and unknown-calling consequences through ForkSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as fork from '../../../dist/Execution/Delegation/Fork/Surface.js'

test('WHAT[DELEG-019] FORK_TOOL_payload_has_assignment_and_requirements', () => {
  const wire = fork.render('en', {
    Assignment: 'inspect',
    CommissionerRecord: 'manager record',
    Attachment: 'attachment',
    RootRequirements: ['one', 'two'],
    Payload: 'payload',
  })
  assert.match(wire, /inspect/)
  assert.match(wire, /one/)
  assert.match(wire, /two/)
})
test('WHAT[DELEG-019] FORK_TOOL_unknown_calling_is_generic_denial', () => {
  assert.match(fork.unavailableCalling('en', false), /Unknown or unavailable calling/)
})
test('WHAT[DELEG-019] FORK_TOOL_orchestrator_unknown_calling_is_generic_denial', () => {
  assert.match(fork.unavailableCalling('en', true), /Unknown or unavailable calling/)
})

test('WHAT[DELEG-003] FORK_road_with_calling_is_independent_and_omitted_calling_continues_byname', () => {
  const independent = fork.chooseRoad('Manager', 'Ada', 'inspect the retry path')
  assert.equal(independent.ok, true)
  assert.equal(independent.road, 'Independent')
  assert.equal(independent.byname, 'Ada')
  assert.equal(independent.charge, 'inspect the retry path')
  assert.equal(independent.authorityTransferred, false)

  const continuation = fork.chooseRoad('', 'Ada', 'continue the retry path')
  assert.equal(continuation.ok, true)
  assert.equal(continuation.road, 'Continuation')
  assert.equal(continuation.byname, 'Ada')
  assert.equal(continuation.calling, null)
})

test('WHAT[DELEG-006] FORK_continuation_reuses_bound_managed_agent_and_does_not_rebind_tier', () => {
  const result = fork.reuseBinding('Ada', 'deep-inspector', 'fast-inspector', 'deep', 'continue the charge')
  assert.equal(result.ok, true)
  assert.equal(result.byname, 'Ada')
  assert.equal(result.managedAgent, 'deep-inspector')
  assert.equal(result.requestedAgent, 'fast-inspector')
  assert.equal(result.tier, 'deep')
  assert.equal(result.authorityTransferred, false)
  assert.equal(Object.hasOwn(result, 'agentId'), false)
})


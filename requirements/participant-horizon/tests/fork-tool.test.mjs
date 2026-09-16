// Fork calling denial is a public natural-language consequence; bindings stay
// inside the delegation-owned ForkSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as fork from '../../../dist/Execution/Delegation/Fork/Surface.js'

const assertDeniedGenerically = (orchestrator, calling) => {
  const result = fork.unavailableCalling('en', orchestrator)
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /Reviewer|fast-|deep-|error\s*=/i)
  assert.ok(calling)
}

test('WHAT[PARTICIPANT-HORIZON-009] FORK_manager-unavailable_is_denied_generically', () => {
  assertDeniedGenerically(false, 'examiner')
})

test('WHAT[PARTICIPANT-HORIZON-009] FORK_manager-unknown_is_denied_generically', () => {
  assertDeniedGenerically(false, 'wizard')
})

test('WHAT[PARTICIPANT-HORIZON-009] FORK_orchestrator-unknown_is_denied_generically', () => {
  assertDeniedGenerically(true, 'coder')
})

test('WHAT[PARTICIPANT-HORIZON-014] FORK_unknown_calling_does_not_expose_machine_binding_affordance', () => {
  const result = fork.unavailableCalling('en', true)
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /Reviewer|fast-|deep-|error\s*=/i)
})

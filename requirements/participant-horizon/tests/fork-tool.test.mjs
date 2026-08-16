// Fork calling denial is a public natural-language consequence; bindings stay
// inside the delegation-owned ForkSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as fork from '../../../dist/Execution/Delegation/Fork/Surface.js'

for (const [label, orchestrator, calling] of [
  ['manager-unavailable', false, 'examiner'],
  ['manager-unknown', false, 'wizard'],
  ['orchestrator-unknown', true, 'coder'],
]) {
  test(`WHAT[PARTICIPANT-HORIZON-009] FORK_${label}_is_denied_generically`, () => {
    const result = fork.unavailableCalling('en', orchestrator)
    assert.match(result, /Unknown or unavailable calling/)
    assert.doesNotMatch(result, /Reviewer|fast-|deep-|error\s*=/i)
    assert.ok(calling)
  })
}

test('WHAT[PARTICIPANT-HORIZON-014] FORK_unknown_calling_does_not_expose_machine_binding_affordance', () => {
  const result = fork.unavailableCalling('en', true)
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /Reviewer|fast-|deep-|error\s*=/i)
})

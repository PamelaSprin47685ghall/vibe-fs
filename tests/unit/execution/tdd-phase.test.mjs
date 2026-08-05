// tests/unit/execution/tdd-phase.test.mjs — TDD phase codec + assignment.

// Closed TddPhase = Red | Green. Wire is exact lowercase only; no default green.
// composeAssignment injects the phase constraint into the child prompt body.
// Used by named `coder` (required) and Manager `fork` (optional).

import assert from 'node:assert/strict'
import test from 'node:test'
import { tddPhase } from '../support/domain.mjs'

test('TDD_PHASE_parse_red_and_green_succeed', () => {
  const red = tddPhase.parse('red')
  const green = tddPhase.parse('green')
  assert.equal(red.ok, true)
  assert.equal(green.ok, true)
  assert.equal(tddPhase.wireName(red.value), 'red')
  assert.equal(tddPhase.wireName(green.value), 'green')
})

test('TDD_PHASE_parse_rejects_missing_case_and_aliases', () => {
  for (const raw of [undefined, null, '', '   ', 'RED', 'Green', 'test', 'refactor', 'blue']) {
    const result = tddPhase.parse(raw ?? '')
    assert.equal(result.ok, false, `expected reject for ${JSON.stringify(raw)}`)
    assert.match(result.error, /missing required argument: tdd|UnknownTddPhase/)
  }
})

test('TDD_PHASE_red_assignment_forbids_production_fix', () => {
  assert.match(tddPhase.redAssignment, /TDD phase: RED/)
  assert.match(tddPhase.redAssignment, /Do not implement the production fix/)
  assert.match(tddPhase.redAssignment, /Do not weaken existing assertions/)
})

test('TDD_PHASE_green_assignment_forbids_weakening_tests', () => {
  assert.match(tddPhase.greenAssignment, /TDD phase: GREEN/)
  assert.match(tddPhase.greenAssignment, /Do not delete, skip, loosen, or rewrite the test/)
  assert.match(tddPhase.greenAssignment, /smallest production change/)
})

test('TDD_PHASE_compose_assignment_puts_constraint_before_prompt', () => {
  const red = tddPhase.parse('red')
  assert.equal(red.ok, true)
  const composed = tddPhase.composeAssignment(red.value, 'cover missing index')
  assert.ok(composed.startsWith('TDD phase: RED'))
  assert.match(composed, /Do not implement the production fix/)
  assert.match(composed, /cover missing index/)
  assert.ok(composed.indexOf('TDD phase: RED') < composed.indexOf('cover missing index'))
})

test('TDD_PHASE_compose_assignment_green_keeps_caller_prompt', () => {
  const green = tddPhase.parse('green')
  assert.equal(green.ok, true)
  const composed = tddPhase.composeAssignment(green.value, 'minimal fix only')
  assert.ok(composed.startsWith('TDD phase: GREEN'))
  assert.match(composed, /Do not delete, skip, loosen, or rewrite the test/)
  assert.match(composed, /minimal fix only/)
})

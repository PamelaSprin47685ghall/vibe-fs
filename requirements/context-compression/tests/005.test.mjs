import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import { budget } from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

test('WHAT[context-compression-005] a recorded failure advances the budget by one', () => {
  assert.equal(budget.recordFailure(budget.initial).failures, 1)
  assert.equal(budget.isValidRecord(0, 1), true)
  assert.equal(budget.isValidRecord(0, 2), false)
  assert.equal(budget.isValidRecord(3, 4), true)
})

test('WHAT[context-compression-005] a completed answer discussing overflow remains valid prose', () => {
  assert.equal(compression.terminalValidityIsValid(
    'The request failed with context_overflow earlier; I have summarised the findings instead.',
  ), true)
})

test.todo('WHAT[context-compression-005] actual Completed, Failed and Aborted attempts follow their typed outcomes independently of error prose; GAP-104')

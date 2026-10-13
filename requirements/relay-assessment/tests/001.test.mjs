import assert from 'node:assert/strict'
import test from 'node:test'
import { REVIEW_OUTCOMES } from '../../work/tests/support/contract.mjs'

test('WHAT[relay-assessment-001] empty findings are Promoted and nonempty findings are Stayed', () => {
  const outcome = (findings) => (findings.length === 0 ? 'Promoted' : 'Stayed')
  assert.equal(outcome([]), 'Promoted')
  assert.equal(outcome([['criteria', 'plan']]), 'Stayed')
  assert.deepEqual(REVIEW_OUTCOMES, ['Promoted', 'Stayed'])
  assert.equal(REVIEW_OUTCOMES.includes('Accepted'), false)
})

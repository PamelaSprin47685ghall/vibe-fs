import assert from 'node:assert/strict'
import test from 'node:test'
import { REVIEW_OUTCOMES, reviewFromFindings } from './support/contract.mjs'

test('WHAT[work-043] empty findings are Promoted and nonempty findings are Stayed', () => {
  const empty = reviewFromFindings([])
  assert.equal(empty.outcome, 'Promoted')
  const gaps = reviewFromFindings([['criteria', 'plan']])
  assert.equal(gaps.outcome, 'Stayed')
  assert.equal(reviewFromFindings(null).ok, false)
  assert.equal(reviewFromFindings([['criteria']]).ok, false)
  assert.deepEqual(REVIEW_OUTCOMES, ['Promoted', 'Stayed'])
  assert.equal(REVIEW_OUTCOMES.includes('Accepted'), false)
})

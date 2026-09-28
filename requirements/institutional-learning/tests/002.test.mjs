import assert from 'node:assert/strict'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

test('WHAT[institutional-learning-002] current evaluator returns one disposition for explicitly named rule and unmatched experience', () => {
  assert.deepEqual(learning.evaluate('known-rule explained this result', ['known-rule']), { disposition: 'ABSORB' })
  assert.deepEqual(learning.evaluate('unrelated local anecdote', ['known-rule']), { disposition: 'DISCARD' })
})

test.todo('WHAT[institutional-learning-002] GAP-181: BIRTH evaluation and one revision-conflict reevaluation, followed by explicit failure on a second conflict')

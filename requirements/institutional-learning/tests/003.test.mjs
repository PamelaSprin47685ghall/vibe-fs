import assert from 'node:assert/strict'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

test('WHAT[institutional-learning-003] current evaluator consumes the supplied rule names rather than an independent hidden rule-name list', () => {
  const experience = 'known-rule applies to this success'
  assert.equal(learning.evaluate(experience, ['known-rule']).disposition, 'ABSORB')
  assert.equal(learning.evaluate(experience, []).disposition, 'DISCARD')
  assert.equal(learning.evaluate(experience, ['different-rule']).disposition, 'DISCARD')
})

test.todo('WHAT[institutional-learning-003] GAP-181: semantic mechanism extraction and actual input-capability isolation; substring matching is not an abstraction oracle')

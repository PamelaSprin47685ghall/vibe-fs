import assert from 'node:assert/strict'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

test('WHAT[institutional-learning-005] current evaluator discards an unmatched local timestamp and path', () => {
  assert.equal(learning.evaluate('one-off timestamp 2026-08-20 in /tmp/a', ['known-rule']).disposition, 'DISCARD')
})

test.todo('WHAT[institutional-learning-005] GAP-181: review and exercise meaningful BIRTH candidates against trigger, distinction, novelty and attention-cost boundaries')

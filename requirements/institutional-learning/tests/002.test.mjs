import assert from 'node:assert/strict'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

test('WHAT[institutional-learning-002] current evaluator returns one disposition for explicitly named rule and unmatched experience', () => {
  assert.deepEqual(learning.evaluate('known-rule explained this result', ['known-rule']), { disposition: 'ABSORB' })
  assert.deepEqual(learning.evaluate('unrelated local anecdote', ['known-rule']), { disposition: 'DISCARD' })
})

const candidate = {
  tipName: 'fresh-tip',
  enforcerTextEn: 'enforce the distilled mechanism',
  enforcerTextZh: '执行提炼出的机制',
  mainTextEn: 'main handling text',
  mainTextZh: 'Main 处置正文',
  trigger: 'the same mechanism recurs',
  negative: 'a one-off local path is not this rule',
}

test('WHAT[institutional-learning-002] GAP-181: BIRTH evaluation with one revision-drift reevaluation, then explicit failure on the second conflict', () => {
  const stable = learning.learn('a reusable mechanism', candidate, [['known-rule']])
  assert.equal(stable.disposition, 'BIRTH')
  assert.equal(stable.reevaluated, false)

  const reevaluated = learning.learn('a reusable mechanism', candidate, [
    ['known-rule'],
    ['known-rule', 'other-new-rule'],
    ['known-rule', 'other-new-rule'],
  ])
  assert.equal(reevaluated.disposition, 'BIRTH')
  assert.equal(reevaluated.reevaluated, true)

  const conflicted = learning.learn('a reusable mechanism', candidate, [
    ['known-rule'],
    ['known-rule', 'other-new-rule'],
    ['known-rule', 'other-new-rule', 'another-rule'],
  ])
  assert.equal(conflicted.conflict, 'revision-conflict')
  assert.equal(conflicted.disposition, undefined)
})

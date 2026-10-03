import assert from 'node:assert/strict'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

test('WHAT[institutional-learning-003] current evaluator consumes the supplied rule names rather than an independent hidden rule-name list', () => {
  const experience = 'known-rule applies to this success'
  assert.equal(learning.evaluate(experience, ['known-rule']).disposition, 'ABSORB')
  assert.equal(learning.evaluate(experience, []).disposition, 'DISCARD')
  assert.equal(learning.evaluate(experience, ['different-rule']).disposition, 'DISCARD')
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

test('WHAT[institutional-learning-003] GAP-181: candidate admission is mechanical — TipName conflict and incomplete bilingual leaves fall back; the abstraction itself stays with the caller', () => {
  assert.deepEqual(learning.evaluate('anything', ['known-rule'], candidate), { disposition: 'BIRTH' })
  assert.equal(learning.evaluate('anything', ['known-rule'], { ...candidate, tipName: 'known-rule' }).disposition, 'DISCARD')
  assert.equal(learning.evaluate('anything', ['known-rule'], { ...candidate, enforcerTextZh: '' }).disposition, 'DISCARD')
  assert.equal(learning.evaluate('anything', ['known-rule'], { ...candidate, mainTextEn: undefined }).disposition, 'DISCARD')
  // The caller owns the semantic judgment: an admissible candidate wins even
  // when the experience also mentions an existing rule name.
  assert.deepEqual(learning.evaluate('known-rule covered it', ['known-rule'], candidate), { disposition: 'BIRTH' })
})

test.todo('WHAT[institutional-learning-003] GAP-181: semantic mechanism extraction and actual input-capability isolation; substring matching is not an abstraction oracle')

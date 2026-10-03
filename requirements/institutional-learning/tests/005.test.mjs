import assert from 'node:assert/strict'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

test('WHAT[institutional-learning-005] current evaluator discards an unmatched local timestamp and path', () => {
  assert.equal(learning.evaluate('one-off timestamp 2026-08-20 in /tmp/a', ['known-rule']).disposition, 'DISCARD')
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

test('WHAT[institutional-learning-005] GAP-181: BIRTH requires a future-recognizable trigger and a negative/distinction guard; a candidate without them falls back', () => {
  assert.equal(learning.evaluate('a mechanism', ['known-rule'], candidate).disposition, 'BIRTH')
  assert.equal(learning.evaluate('a mechanism', ['known-rule'], { ...candidate, trigger: '' }).disposition, 'DISCARD')
  assert.equal(learning.evaluate('a mechanism', ['known-rule'], { ...candidate, negative: ' ' }).disposition, 'DISCARD')
})

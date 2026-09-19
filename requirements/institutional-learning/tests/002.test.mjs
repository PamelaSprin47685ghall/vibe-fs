import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[institutional-learning-002] one enhancer evaluation yields exactly one ABSORB BIRTH or DISCARD disposition with no score state', () => {
  assert.equal(learning.evaluate('known-rule explained the mechanism', ['known-rule']).disposition, 'ABSORB')
  assert.equal(learning.evaluate('unrelated local anecdote', ['known-rule']).disposition, 'DISCARD')
  const enhancer = read('src/Wanxiangshu/Enforcer/InstitutionalLearning/Enhancer.fs')
  assert.doesNotMatch(enhancer, /\bScore\b|\bThreshold\b|\bRetryLoop\b|^\s*(?:while|for)\s/m)
})

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[institutional-learning-005] no reusable trigger or nonduplicate mechanism degrades to DISCARD rather than attention-tax debt', () => {
  assert.equal(learning.evaluate('one-off timestamp 2026-08-20 in /tmp/a', ['known-rule']).disposition, 'DISCARD')
})

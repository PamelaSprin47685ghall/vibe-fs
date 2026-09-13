import assert from 'node:assert/strict'
import test from 'node:test'
import { coveredFunction } from './src/a.js'

test('tests only a.js', () => {
  assert.equal(coveredFunction(5), 'positive')
})

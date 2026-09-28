import assert from 'node:assert/strict'
import test from 'node:test'
import { testFileProblems } from './support/structure.mjs'

test('WHAT[requirement-system-018] files map to live clauses without requiring a file for every clause', () => {
  assert.deepEqual(testFileProblems(['tests/001.test.mjs'], new Set(['001', '002'])), [])
  assert.deepEqual(testFileProblems([], new Set(['001'])), [])
})

test('WHAT[requirement-system-018] wrong names, retired clause numbers and split files are rejected', () => {
  assert.equal(testFileProblems(['tests/misc.test.mjs'], new Set(['001'])).length, 1)
  assert.match(testFileProblems(['tests/002.test.mjs'], new Set(['001']))[0], /clause 002 is absent/)
  const duplicate = testFileProblems(['tests/001.test.mjs', 'tests/nested/001.test.mjs'], new Set(['001']))
  assert.equal(duplicate.length, 1)
  assert.match(duplicate[0], /more than one file/)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import { testOwnershipProblems } from './support/structure.mjs'

test('WHAT[requirement-system-004] ownership accepts multiline titles, modifiers and template suffixes', () => {
  const source = [
    'test(\n "WHAT[sample-001] ordinary", () => {})',
    'test.skip("WHAT[sample-001] gated", () => {})',
    'it(`WHAT[sample-001] ${name}`, () => {})',
    'test.beforeEach(() => {})',
    'integrationTest("WHAT[sample-001] adapter", () => {})',
  ].join('\n')
  assert.deepEqual(testOwnershipProblems(source, 'sample-001'), [])
})

test('WHAT[requirement-system-004] ownership rejects missing, multiple and wrong primary clauses', () => {
  for (const title of ['no owner', 'WHAT[sample-002] wrong', 'WHAT[sample-001] WHAT[sample-002] two']) {
    const issues = testOwnershipProblems(`test(${JSON.stringify(title)}, () => {})`, 'sample-001')
    assert.equal(issues.length, 1, title)
    assert.equal(issues[0].expected, 'sample-001')
  }
  assert.deepEqual(testOwnershipProblems('// test("WHAT[wrong-001]", () => {})', 'sample-001'), [])
})

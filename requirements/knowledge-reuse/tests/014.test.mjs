// requirements/knowledge-reuse/tests/014.test.mjs
//
// Laws: knowledge-reuse-014

import assert from 'node:assert/strict'
import test from 'node:test'

import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

test('WHAT[knowledge-reuse-014] T26_large_traces_and_diffs_truncate_with_explicit_truncation_notice', () => {
  assert.equal(typeof casebook.truncateDiffForBudget, 'function', 'casebook must export truncateDiffForBudget')
  const largeDiff = 'x'.repeat(20000)
  const truncated = casebook.truncateDiffForBudget(largeDiff, 1000)
  assert.ok(truncated.text.length <= 1000)
  assert.equal(truncated.isTruncated, true)
  assert.match(truncated.notice, /truncated|截断/i)
})

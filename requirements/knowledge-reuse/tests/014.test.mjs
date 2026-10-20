import assert from 'node:assert/strict'
import test from 'node:test'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

test('WHAT[knowledge-reuse-014] the truncator retains the tail and includes an explicit notice within its character budget', () => {
  const text = 'HEAD\n' + 'x'.repeat(2000) + '\nTAIL'
  const short = casebook.truncateDiffForBudget(text, 100)
  assert.equal(short.isTruncated, true)
  assert.ok(short.text.length <= 100)
  assert.ok(short.text.endsWith('\nTAIL'))
  assert.match(short.text, /truncated|截断/)
  const exact = casebook.truncateDiffForBudget(text, text.length)
  assert.equal(exact.text, text)
  assert.equal(exact.isTruncated, false)
})

test('WHAT[knowledge-reuse-014] truncation never cuts a code point in half, so the truncated diff is well-formed text', () => {
  // A head or tail cut inside a surrogate pair leaves a lone surrogate; any UTF-8 transport to the
  // Bookkeeper turns it into U+FFFD, so the maintained diff would no longer quote the real change.
  for (let budget = 40; budget < 80; budget += 1) {
    const cut = casebook.truncateDiffForBudget('😀'.repeat(60), budget)
    assert.equal(cut.isTruncated, true)
    assert.equal(cut.text.isWellFormed(), true, `budget ${budget}`)
    assert.ok(cut.text.length <= budget, `budget ${budget}`)
  }
})

test.todo('WHAT[knowledge-reuse-014] GAP-160: actual request budgets cover the complete payload, preserve every changed path, and leave old case unchanged when useful maintenance is impossible')

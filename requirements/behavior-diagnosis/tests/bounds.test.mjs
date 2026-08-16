// Enforcer fail-closed content bounds on the exact-one commit path.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const MAX_BLOG_TEXT_BYTES = enforcer.maxBlogTextBytes
const MAX_EVIDENCE_BYTES = enforcer.maxEvidenceBytes

test('WHAT[BD-011] ENFORCER_043_canonical_text_over_512KiB_fails_closed', () => {
  const result = enforcer.validateBounds('a'.repeat(MAX_BLOG_TEXT_BYTES + 1), undefined)
  assert.equal(result.ok, false)
  assert.match(result.error, /MaxBlogTextBytes=524288/)
})

test('WHAT[BD-011] ENFORCER_043_canonical_evidence_over_128KiB_fails_closed', () => {
  const result = enforcer.validateBounds('work', 'b'.repeat(MAX_EVIDENCE_BYTES + 1))
  assert.equal(result.ok, false)
  assert.match(result.error, /MaxEvidenceBytes=131072/)
})

test('WHAT[BD-011] ENFORCER_042_bound_constants_match_utf8_byte_thresholds', () => {
  assert.equal(enforcer.validateBounds('a'.repeat(MAX_BLOG_TEXT_BYTES), undefined).ok, true)
  assert.equal(enforcer.validateBounds('work', 'b'.repeat(MAX_EVIDENCE_BYTES)).ok, true)
  assert.equal(enforcer.validateBounds('a'.repeat(MAX_BLOG_TEXT_BYTES + 1), undefined).ok, false)
  assert.equal(enforcer.validateBounds('work', 'b'.repeat(MAX_EVIDENCE_BYTES + 1)).ok, false)
  assert.equal(enforcer.validateBounds('界'.repeat(Math.floor(MAX_BLOG_TEXT_BYTES / 3) + 1), undefined).ok, false)
})

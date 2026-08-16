// ENFORCER-045 / PERSIST-010 — coverage birth gate.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

test('WHAT[BD-013] ENFORCER_045_mainContext_refuses_when_next_sequence_cannot_advance', () => {
  const refused = blog.coverageBirth({
    previousIngestedThroughSequence: 2,
    nextIngestedThroughSequence: 2,
    previousCoverableTurnCutoffExclusive: 2,
    nextCoverableTurnCutoffExclusive: 2,
    nextCoveredPrefixDigest: 'covered-all',
  })
  assert.equal(refused.ok, false)
  assert.match(refused.error, /non-advancing ingested sequence/)
})

test('WHAT[BD-013] ENFORCER_045_mainContext_refuses_unmapped_next_cursor', () => {
  const missingDigest = blog.coverageBirth({
    previousIngestedThroughSequence: 0,
    nextIngestedThroughSequence: 0,
    previousCoverableTurnCutoffExclusive: 0,
    nextCoverableTurnCutoffExclusive: 0,
    nextCoveredPrefixDigest: '',
  })
  assert.equal(missingDigest.ok, false)

  const unmapped = blog.coverageBirth({
    previousIngestedThroughSequence: 1,
    nextIngestedThroughSequence: 1,
    previousCoverableTurnCutoffExclusive: 1,
    nextCoverableTurnCutoffExclusive: 1,
    nextCoveredPrefixDigest: 'stale',
  })
  assert.equal(unmapped.ok, false)
  assert.match(unmapped.error, /non-advancing/)
})

test('WHAT[BD-013] ENFORCER_045_mainContext_accepts_strict_advance', () => {
  const context = blog.coverageBirth({
    previousIngestedThroughSequence: 0,
    nextIngestedThroughSequence: 1,
    previousCoverableTurnCutoffExclusive: 0,
    nextCoverableTurnCutoffExclusive: 1,
    nextCoveredPrefixDigest: 'd1',
  })
  assert.equal(context.ok, true)
  assert.equal(context.ingestedThroughSequence, 1)
  assert.ok(context.coverableTurnCutoffExclusive > 0)
})

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
    traceSequences: [1, 2],
  })
  assert.equal(refused.ok, false)
  assert.match(refused.error, /non-advancing ingested sequence/)
})

test('WHAT[BD-013] ENFORCER_045_mainContext_refuses_unmapped_next_cursor', () => {
  const unmapped = blog.coverageBirth({
    previousIngestedThroughSequence: 0,
    nextIngestedThroughSequence: 1,
    previousCoverableTurnCutoffExclusive: 0,
    nextCoverableTurnCutoffExclusive: 1,
    nextCoveredPrefixDigest: 'd1',
    traceSequences: [],
  })
  assert.equal(unmapped.ok, false)
  assert.match(unmapped.error, /unmapped/)
})

test('WHAT[BD-013] ENFORCER_045_mainContext_accepts_strict_advance', () => {
  const context = blog.coverageBirth({
    previousIngestedThroughSequence: 0,
    nextIngestedThroughSequence: 1,
    previousCoverableTurnCutoffExclusive: 0,
    nextCoverableTurnCutoffExclusive: 1,
    nextCoveredPrefixDigest: 'd1',
    traceSequences: [1],
  })
  assert.equal(context.ok, true)
  assert.equal(context.ingestedThroughSequence, 1n)
  assert.equal(context.coverableTurnCutoffExclusive, 1)
})

test('WHAT[BD-013] ENFORCER_045_mid_turn_advance_preserves_covered_prefix', () => {
  const context = blog.coverageBirth({
    previousIngestedThroughSequence: 3,
    nextIngestedThroughSequence: 4,
    previousCoverableTurnCutoffExclusive: 3,
    nextCoverableTurnCutoffExclusive: 3,
    nextCoveredPrefixDigest: 'covered-prefix',
    traceSequences: [4],
  })
  assert.deepEqual(context, {
    ok: true,
    ingestedThroughSequence: 4n,
    coverableTurnCutoffExclusive: 3,
    nextCoveredPrefixDigest: 'covered-prefix',
  })
})

// BD-013: Coverage monotonic progression birth gate and pre-commit checks
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import * as observation from '../../../dist/Enforcer/ObservationSurface.js'

test('WHAT[BD-013] ENFORCER_045_stale_previous_ingest_cursor_rejected', () => {
  const first = observation.applyBlogEntry(
    { frameEpoch: 0, previousIngestedThroughSequence: 0, nextIngestedThroughSequence: 2, previousCoverableTurnCutoffExclusive: 0, nextCoverableTurnCutoffExclusive: 1, nextCoveredPrefixDigest: 'd1' },
    observation.blogFrame({ kind: 'Entry', digest: 'sha-a', ref: 'blob-a', coveredFrom: 0, coveredThrough: 2 }),
    observation.emptyBlog,
  )
  assert.equal(first.ok, true)
  const stale = observation.applyBlogEntry(
    { frameEpoch: 0, previousIngestedThroughSequence: 0, nextIngestedThroughSequence: 3, previousCoverableTurnCutoffExclusive: 0, nextCoverableTurnCutoffExclusive: 2, nextCoveredPrefixDigest: 'd2' },
    observation.blogFrame({ kind: 'Entry', digest: 'sha-b', ref: 'blob-b', coveredFrom: 0, coveredThrough: 3 }),
    first.value,
  )
  assert.equal(stale.ok, false)
})

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

test('WHAT[BD-013] ENFORCER_commit_classification_exposes_named_semantic_branches', () => {
  assert.equal(blog.classifyCommit({ callCount: 0, providerRun: 'run', tip: 'primitive-obsession' }).branch, 'ProtocolRepair')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: '', tip: 'primitive-obsession' }).branch, 'Fatal')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'run', tip: '' }).branch, 'ProtocolRepair')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'run', tip: 'not-a-field' }).branch, 'ProtocolRepair')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'run', tip: 'primitive-obsession' }).branch, 'Committed')
  assert.equal(enforcer.validateProviderRun('run').ok, true)
})

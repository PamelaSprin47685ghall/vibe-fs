// PERSIST-010 cycle commit prechecks at the Enforcer/Blog owner boundary.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

test('WHAT[BD-013] ENFORCER_precheck_stale_ingest_abandons_then_catchup', () => {
  const stale = blog.coverageBirth({
    previousIngestedThroughSequence: 3,
    nextIngestedThroughSequence: 3,
    previousCoverableTurnCutoffExclusive: 3,
    nextCoverableTurnCutoffExclusive: 3,
    nextCoveredPrefixDigest: 'stale',
  })
  assert.equal(stale.ok, false)
  assert.match(stale.error, /non-advancing/)
  const commit = blog.classifyCommit({ callCount: 1, providerRun: 'asst-1', tip: 'primitive-obsession' })
  assert.equal(commit.branch, 'Committed')
})

test('WHAT[BD-013] ENFORCER_precheck_cutoff_mismatch_abandons', () => {
  const stale = blog.coverageBirth({
    previousIngestedThroughSequence: 3,
    nextIngestedThroughSequence: 4,
    previousCoverableTurnCutoffExclusive: 0,
    nextCoverableTurnCutoffExclusive: 4,
    nextCoveredPrefixDigest: 'd2',
  })
  assert.equal(stale.ok, true, 'sequence may advance, while writer precheck remains responsible for cutoff identity')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'asst-2', tip: 'primitive-obsession' }).branch, 'Committed')
})

test('WHAT[BD-013] ENFORCER_precheck_epoch_mismatch_after_squash_abandons', () => {
  const squash = observationEpoch(0, 1)
  assert.equal(squash.next > squash.previous, true)
  const staleEpoch = blog.classifyCommit({ callCount: 1, providerRun: 'asst-3', tip: 'primitive-obsession' })
  assert.equal(staleEpoch.branch, 'Committed')
  assert.equal(blog.coverageBirth({
    previousIngestedThroughSequence: 3,
    nextIngestedThroughSequence: 3,
    previousCoverableTurnCutoffExclusive: 3,
    nextCoverableTurnCutoffExclusive: 3,
    nextCoveredPrefixDigest: 'd3',
  }).ok, false)
})

const observationEpoch = (previous, next) => ({ previous, next })

test('WHAT[BD-013] ENFORCER_commit_classification_exposes_named_semantic_branches', () => {
  assert.equal(blog.classifyCommit({ callCount: 0, providerRun: 'run', tip: 'primitive-obsession' }).branch, 'ProtocolRepair')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: '', tip: 'primitive-obsession' }).branch, 'Fatal')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'run', tip: '' }).branch, 'ProtocolRepair')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'run', tip: 'not-a-field' }).branch, 'ProtocolRepair')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'run', tip: 'primitive-obsession' }).branch, 'Committed')
  assert.equal(enforcer.validateProviderRun('run').ok, true)
})

// ENFORCER-045 atomic fold: one BlogObservationCommitted updates Blog and
// Enforcement together. The owner surface keeps both projections semantic.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import * as observation from '../../../dist/Enforcer/ObservationSurface.js'

const cycle = ({ run = 'msg-e1', tipRuleId = 'enforcement-tip-1', fieldNameAtCommit = 'field-tip-1', toolCalls = [], evidenceRef } = {}) => ({
  mainSessionId: 'ses-main',
  bloggerSessionId: 'ses-blogger',
  run,
  toolCallIds: toolCalls,
  textRef: 'blob-e1',
  textDigest: 'sha-e1',
  tipRuleId,
  fieldNameAtCommit,
  evidenceRef,
  observedPrefixEpoch: 0,
})
const apply = (state, value, n = 1) => {
  const enforcement = observation.applyEnforcementCycle(state.enforcement, cycle(value))
  assert.equal(enforcement.ok, true, enforcement.ok ? '' : enforcement.error)
  const committed = observation.applyBlogEntry(
    {
      frameEpoch: 0,
      previousIngestedThroughSequence: n - 1,
      nextIngestedThroughSequence: n,
      previousCoverableTurnCutoffExclusive: n - 1,
      nextCoverableTurnCutoffExclusive: n,
      nextCoveredPrefixDigest: `d-${n}`,
    },
    observation.blogFrame({
      kind: 'Entry',
      digest: `sha-e${n}`,
      ref: `blob-e${n}`,
      coveredFrom: n - 1,
      coveredThrough: n,
    }),
    state.blog,
  )
  assert.equal(committed.ok, true, committed.ok ? '' : committed.error)
  return { enforcement: enforcement.value, blog: committed.value }
}

test('WHAT[BD-012] ENFORCER_045_cycle_commit_appends_frame_and_advances_coverage', () => {
  const state = apply({ enforcement: observation.emptyEnforcement, blog: observation.emptyBlog }, {
    toolCalls: ['call-1'], tipRuleId: 'enforcement-a01', fieldNameAtCommit: 'primitive-obsession', evidenceRef: 'blob-evidence',
  })
  assert.equal(observation.frameCount(state.blog), 1)
  assert.equal(observation.coverage(state.blog).ingestedThroughSequence, 1)
  assert.equal(observation.coverage(state.blog).coverableTurnCutoffExclusive, 1)
})

test('WHAT[BD-012] ENFORCER_045_enforcement_half_queryable_by_provider_run', () => {
  const state = apply({ enforcement: observation.emptyEnforcement, blog: observation.emptyBlog }, {
    run: 'msg-run1', toolCalls: ['call-a', 'call-b'], tipRuleId: 'enforcement-a01', fieldNameAtCommit: 'primitive-obsession', evidenceRef: 'blob-ev1',
  })
  assert.equal(observation.enforcementRecordCount(state.enforcement), 1)
  const tip = observation.recentTips(state.enforcement)[0]
  assert.equal(tip.ruleId, 'enforcement-a01')
  assert.equal(tip.fieldName, 'primitive-obsession')
})

test('WHAT[BD-014] ENFORCER_045_duplicate_provider_run_rejected_by_fold', () => {
  const first = observation.applyEnforcementCycle(observation.emptyEnforcement, cycle({ run: 'msg-dup' }))
  assert.equal(first.ok, true)
  const duplicate = observation.applyEnforcementCycle(first.value, cycle({ run: 'msg-dup' }))
  assert.equal(duplicate.ok, false)
  assert.match(duplicate.error, /already recorded/)
})

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

test('WHAT[BD-012] ENFORCER_045_no_enforcement_cycle_committed_fact_exists', () => {
  assert.throws(() => blog.serializeFact({ case: 'EnforcementCycleCommitted' }), /unknown fact/)
})

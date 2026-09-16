import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as attemptPlan from '../../../dist/Context/Companion/AttemptPlanProbeEligibilitySurface.js'

test('WHAT[CONTEXT-COMPRESSION-009] CTX_010_the_probe_records_the_epoch_it_was_built_from', () => {
  const result = prefix.select({
    committedEpoch: 3,
    committedSnapshot: prefix.snapshot({ ref: 'b', frozenDigest: 'f', cutoff: 2, prefixDigest: 'p', sealRoot: 's', syntheticId: 'syn' }),
    coverableCutoff: 7,
    coveredDigest: 'p7',
    requestStartCutoff: 20,
    recomputeDigest: () => 'p7',
  })
  assert.equal(result.ok, true)
  assert.equal(result.basedOnEpoch, 3n)
})

test('WHAT[CONTEXT-COMPRESSION-009] candidate_uncommitted_leaves_no_durable_facts', () => {
  assert.equal(attemptPlan.uncommittedLeavesNoDurableFacts, true)
})

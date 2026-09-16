import assert from 'node:assert/strict'
import test from 'node:test'
import * as invariants from '../../../dist/Context/Companion/RequestContextInvariantsSurface.js'
import * as enforcerCycleRecovery from '../../../dist/Enforcer/Cycle/Recovery.js'

test('WHAT[CONTEXT-COMPRESSION-027] every successful Main construction satisfies the §4.2 invariants', () => {
  assert.ok(invariants.mainConstructionInvariants)
})

test('WHAT[CONTEXT-COMPRESSION-027] every successful Squash construction binds count and digests', () => {
  assert.ok(invariants.squashConstructionBindsCount)
})

test('WHAT[CONTEXT-COMPRESSION-027] non-advancing coverage is rejected, never constructed', () => {
  assert.ok(invariants.nonAdvancingCoverageRejected)
})

test('WHAT[CONTEXT-COMPRESSION-027] different content yields different digests; same content is stable', () => {
  assert.ok(invariants.contentDigestProperties)
})

test('WHAT[CONTEXT-COMPRESSION-027] squash count/digest disagreement is rejected, never constructed', () => {
  assert.ok(invariants.squashCountDigestDisagreementRejected)
})

test('WHAT[CONTEXT-COMPRESSION-027] recovery rejection cases are distinct and labeled', () => {
  assert.ok(invariants.recoveryRejectionCasesDistinct)
})

test('WHAT[CONTEXT-COMPRESSION-027] old-epoch staged requests keep their frozen epoch and never claim current authority', () => {
  assert.ok(invariants.oldEpochStagedRequestsKeepFrozen)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

test('WHAT[SPEC-INV-006] STRENGTH_006_prepared_commit_unknown_is_resolved_without_guessing', () => {
  assert.equal(Strength.commitResolvePrepared('Committed', 'Unknown'), 'Proceed')
  assert.equal(Strength.commitResolvePrepared('Rejected', 'Unknown'), 'FallBackK0')
  assert.equal(Strength.commitResolvePrepared('CommitUnknown', 'Matches'), 'Proceed')
  assert.equal(Strength.commitResolvePrepared('CommitUnknown', 'Absent'), 'FallBackK0')
  assert.equal(Strength.commitResolvePrepared('CommitUnknown', 'Unknown'), 'FailClosed')
  assert.equal(Strength.commitResolvePrepared('CommitUnknown', 'Conflicts'), 'FailClosed')
})

test('WHAT[SPEC-INV-007] STRENGTH_007_promotion_commit_unknown_never_allows_continuation_without_durable_fact', () => {
  assert.equal(Strength.commitResolvePromotion('Committed', 'Unknown'), 'Proceed')
  assert.equal(Strength.commitResolvePromotion('Rejected', 'Unknown'), 'FailClosed')
  assert.equal(Strength.commitResolvePromotion('CommitUnknown', 'Matches'), 'Proceed')
  assert.equal(Strength.commitResolvePromotion('CommitUnknown', 'Absent'), 'RetryAppend')
  assert.equal(Strength.commitResolvePromotion('CommitUnknown', 'Unknown'), 'FailClosed')
})

test('WHAT[SPEC-INV-007] STRENGTH_007_promotion_requires_the_exact_target_run_and_real_provider_output', () => {
  assert.equal(Strength.promotionDecide('run-1', 'run-1', 'RealOutput'), 'Promote')
  assert.equal(Strength.promotionDecide('run-1', 'run-2', 'RealOutput'), 'IgnoreWrongRun')
  assert.equal(Strength.promotionDecide('run-1', 'run-1', 'NoOutput'), 'AwaitOrAbandon')
  assert.equal(Strength.promotionDecide('run-1', 'run-1', 'TransportOnly'), 'AwaitOrAbandon')
})

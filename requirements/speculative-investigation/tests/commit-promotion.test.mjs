import assert from 'node:assert/strict'
import test from 'node:test'

import * as Commit from '../../../dist/Strength/Replica/Commit.js'
import * as Promotion from '../../../dist/Strength/Replica/Promotion.js'
import * as Id from '../../../dist/Foundation/Identity.js'

const caseOf = (value) => value.cases()[value.tag]
const run = (value) => Id.ProviderRunIdentityModule_create(value)

test('WHAT[SPEC-INV-006] STRENGTH_006_prepared_commit_unknown_is_resolved_without_guessing', () => {
  assert.equal(caseOf(Commit.StrengthCommit_resolvePrepared(Commit.StrengthAppendOutcome.Committed, Commit.StrengthDurableEvidence.Unknown)), 'Proceed')
  assert.equal(caseOf(Commit.StrengthCommit_resolvePrepared(Commit.StrengthAppendOutcome.Rejected, Commit.StrengthDurableEvidence.Unknown)), 'FallBackK0')
  assert.equal(caseOf(Commit.StrengthCommit_resolvePrepared(Commit.StrengthAppendOutcome.CommitUnknown, Commit.StrengthDurableEvidence.Matches)), 'Proceed')
  assert.equal(caseOf(Commit.StrengthCommit_resolvePrepared(Commit.StrengthAppendOutcome.CommitUnknown, Commit.StrengthDurableEvidence.Absent)), 'FallBackK0')
  assert.equal(caseOf(Commit.StrengthCommit_resolvePrepared(Commit.StrengthAppendOutcome.CommitUnknown, Commit.StrengthDurableEvidence.Unknown)), 'FailClosed')
  assert.equal(caseOf(Commit.StrengthCommit_resolvePrepared(Commit.StrengthAppendOutcome.CommitUnknown, Commit.StrengthDurableEvidence.Conflicts)), 'FailClosed')
})

test('WHAT[SPEC-INV-007] STRENGTH_007_promotion_commit_unknown_never_allows_continuation_without_durable_fact', () => {
  assert.equal(caseOf(Commit.StrengthCommit_resolvePromotion(Commit.StrengthAppendOutcome.Committed, Commit.StrengthDurableEvidence.Unknown)), 'Proceed')
  assert.equal(caseOf(Commit.StrengthCommit_resolvePromotion(Commit.StrengthAppendOutcome.Rejected, Commit.StrengthDurableEvidence.Unknown)), 'FailClosed')
  assert.equal(caseOf(Commit.StrengthCommit_resolvePromotion(Commit.StrengthAppendOutcome.CommitUnknown, Commit.StrengthDurableEvidence.Matches)), 'Proceed')
  assert.equal(caseOf(Commit.StrengthCommit_resolvePromotion(Commit.StrengthAppendOutcome.CommitUnknown, Commit.StrengthDurableEvidence.Absent)), 'RetryAppend')
  assert.equal(caseOf(Commit.StrengthCommit_resolvePromotion(Commit.StrengthAppendOutcome.CommitUnknown, Commit.StrengthDurableEvidence.Unknown)), 'FailClosed')
})

test('WHAT[SPEC-INV-007] STRENGTH_007_promotion_requires_the_exact_target_run_and_real_provider_output', () => {
  assert.equal(caseOf(Promotion.StrengthPromotion_decide(run('run-1'), run('run-1'), Promotion.StrengthProviderOutputEvidence.RealOutput)), 'Promote')
  assert.equal(caseOf(Promotion.StrengthPromotion_decide(run('run-1'), run('run-2'), Promotion.StrengthProviderOutputEvidence.RealOutput)), 'IgnoreWrongRun')
  assert.equal(caseOf(Promotion.StrengthPromotion_decide(run('run-1'), run('run-1'), Promotion.StrengthProviderOutputEvidence.NoOutput)), 'AwaitOrAbandon')
  assert.equal(caseOf(Promotion.StrengthPromotion_decide(run('run-1'), run('run-1'), Promotion.StrengthProviderOutputEvidence.TransportOnly)), 'AwaitOrAbandon')
})

// REVIEW-ASSURANCE-007: Finality causality has no shared parked seal state.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(new URL(`../../../${path}`, import.meta.url), 'utf8')

test('WHAT[REVIEW-ASSURANCE-007] SHARED_finality_has_no_pending_provider_input_seal_registry', () => {
  const shared = read('src/Wanxiangshu/OpenCode/Host/SharedState.fs')
  assert.doesNotMatch(shared, /PendingReviewSeals|PendingSeal|IncludedToolResultDigests|SealDigest/)
})

test('WHAT[REVIEW-ASSURANCE-002] SHARED_judgement_rendezvous_is_physical_not_a_business_stage', () => {
  const inbox = read('src/Wanxiangshu/Mission/Review/OpenCode/JudgementInbox.fs')
  assert.match(inbox, /TaskCompletionSource/)
  assert.match(inbox, /AwaitJudgement/)
  assert.doesNotMatch(inbox, /FirstPerfect|SecondPerfect|PerfectPending|PendingChallenge|ConfirmedState|Stage|Phase/)
})

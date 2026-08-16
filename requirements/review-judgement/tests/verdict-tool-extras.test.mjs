// REVIEW-JUDGEMENT-001: Judge context-gate precedence remains fail-closed.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as judge from '../../../dist/Mission/Review/OpenCode/JudgeSurface.js'

const contextResult = (overrides = {}) => judge.validateContext(
  overrides.role ?? 'Reviewer',
  overrides.sessionId ?? 'ses-reviewer',
  overrides.hasOwner ?? true,
  overrides.hasParent ?? true,
  overrides.hasBarrier ?? true,
  overrides.hasTree ?? true,
)

const assertIncomplete = (result) => {
  assert.equal(result.ok, false)
  assert.match(result.message, /review context is incomplete/i)
  assert.doesNotMatch(result.message, /error\s*=/)
}

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_unknown_owner_fails_closed_without_internal_vocabulary', () => {
  assertIncomplete(contextResult({ hasOwner: false }))
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_missing_tree_fails_closed_without_internal_vocabulary', () => {
  assertIncomplete(contextResult({ hasParent: false }))
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_no_open_review_barrier_fails_closed_without_internal_vocabulary', () => {
  assertIncomplete(contextResult({ hasBarrier: false, hasTree: true }))
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_non_reviewer_role_is_refused_before_identity_checks', () => {
  const result = contextResult({ role: 'Coder', hasOwner: false, hasParent: false, hasBarrier: false, hasTree: false })
  assert.equal(result.ok, false)
  assert.match(result.message, /did not come from a Reviewer/i)
  assert.doesNotMatch(result.message, /error\s*=/)
})

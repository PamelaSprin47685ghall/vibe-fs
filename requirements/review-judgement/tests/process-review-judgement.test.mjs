// REVIEW-JUDGEMENT-008 / REVIEW-013 process-review judgement boundary.
import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as provider from '../../../dist/Participant/Provider/LanguageSurface.js'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'
import * as todo from '../../../dist/Mission/Review/ReviewTodoSurface.js'
import * as obligation from '../../../dist/Mission/Obligation/Todo/Surface.js'

const sha256 = (value) => `digest:${value}`
const life = 'life-process-judgement'
const ids = todo.ids(sha256, life, 'call-t1')
const write = ids.todoWriteId
const reviewId = ids.todoReviewId
const verdictWitness = (value) => review.verdictWitness({ ProviderRun: value.run, ToolCallId: value.call, GitTreeHash: value.tree, ReviewerSessionId: value.reviewer })
const attempt = review.attemptIdentity('bar_process', verdictWitness({ run: 'run_1', call: 'call_1', tree: 'tree_process', reviewer: 'ses_process_rev' }))
const guardWrap = (handle) => ({ handle, Witness: review.guardWitness(handle) })
const unwrap = (value) => value.handle ?? value
const empty = guardWrap(review.emptyGuard())
const apply = (attemptValue, verdictValue, current) => {
  const result = review.applyVerdict(attemptValue, verdictValue, unwrap(current))
  return result.ok ? { ok: true, value: guardWrap(result.value) } : result
}
const read = (value) => {
  const result = review.guardView(unwrap(value))
  return { ...result, witness: result.witness.state, seals: result.sealCount }
}
const providerLanguage = { english: 'English', simplifiedChinese: 'SimplifiedChinese' }
const providerResources = { readText: (language, path) => provider.readText(language, path) }

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_the_request_kind_is_typed_process_vs_finality', () => {
  assert.deepEqual(todo.requestKindNames(), ['TodoProcess', 'FinalityTerminal'])
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_ensure_review_stays_outstanding_until_concluded', () => {
  assert.equal(todo.needsEnsureReview(true, false), true)
  assert.equal(todo.needsEnsureReview(false, false), false)
  assert.equal(todo.needsEnsureReview(true, true), false)
  assert.equal(todo.needsEnsureReview(false, true), false)
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_process_preamble_commands_exactly_one_verdict_and_disclaims_terminal_witness', () => {
  const preamble = providerResources.readText(providerLanguage.english, 'lifecycle/magic-todo/process-reviewer-preamble')
  assert.match(preamble, /Reply with exactly one judge tool call: PERFECT or REVISE\./)
  assert.doesNotMatch(preamble, /verdict tool/)
  assert.match(preamble, /Process PERFECT is not a terminal Finality witness\./)
  for (const forbidden of ['challenge', 'Challenge', 'seal', 'Seal', 'dual', '2N', 'barrier', 'Barrier']) {
    assert.equal(new RegExp(`\\b${forbidden.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\b`).test(preamble), false, `process preamble must not mention '${forbidden}'`)
  }
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_process_assignment_is_request_range_bounded_without_confirmation_vocabulary', () => {
  const preamble = providerResources.readText(providerLanguage.english, 'lifecycle/magic-todo/process-reviewer-preamble')
  const message = todo.renderAssignmentUserMessage(preamble, {
    TodoReviewId: reviewId,
    TodoWriteId: write,
    ManagerLifeId: life,
    OpeningRaw: 'original task authority text',
    ManagerCheckpointLwr: 'frontier-bounded work record for this checkpoint',
    EffectivePlanComplete: false,
    OldTodo: [],
    ProposedTodo: [],
  })
  const parsed = parseToml(message)
  assert.match(message, /^# original task authority text$/m)
  assert.equal(parsed.manager_checkpoint_lwr, 'frontier-bounded work record for this checkpoint')
  assert.equal(parsed.effective_plan_complete, false)
  assert.equal(parsed.prior_current_obligations, '[]')
  assert.equal(parsed.accepted_obligation_account_under_review, '[]')
  assert.equal(parsed.opening_raw, undefined)
  for (const forbidden of ['challenge', 'Challenge', 'seal', 'Seal', 'dual', '2N', 'barrier', 'Barrier', 'Confirmation']) {
    assert.equal(new RegExp(`\\b${forbidden.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\b`).test(message), false, `process assignment must not mention '${forbidden}'`)
  }
  assert.equal(obligation.renderObligationListWire([]), '[]')
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_continuation_process_assignment_does_not_replay_opening_authority', () => {
  const preamble = providerResources.readText(providerLanguage.english, 'lifecycle/magic-todo/process-reviewer-preamble')
  const message = todo.renderAssignmentUserMessage(preamble, {
    TodoReviewId: reviewId,
    TodoWriteId: write,
    ManagerLifeId: life,
    OpeningRaw: '',
    ManagerCheckpointLwr: 'only work since the reviewer last concluded',
    EffectivePlanComplete: true,
    OldTodo: [{ name: 'old', work: 'old work' }],
    ProposedTodo: [{ name: 'next', work: 'next work' }],
  })

  assert.doesNotMatch(message, /OpeningRaw \(task authority\)/)
  assert.doesNotMatch(message, /original task authority text/)
  const parsed = parseToml(message)
  assert.equal(parsed.manager_checkpoint_lwr, 'only work since the reviewer last concluded')
  assert.equal(parsed.effective_plan_complete, true)
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_a_process_verdict_never_becomes_a_confirmed_witness_by_itself', () => {
  const applied = apply(attempt, 'PERFECT', empty)
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  assert.equal(read(applied.value).observedAttempts, 1)
  assert.equal(read(applied.value).witness, 'NoReview')
  assert.equal(review.satisfiesGuard('tree_process', unwrap(applied.value)), false)
  assert.equal(review.isConfirmed(applied.value.Witness), false)

  const revised = apply(attempt, 'REVISE', empty)
  assert.equal(revised.ok, true)
  assert.equal(revised.value.Witness.state, 'RevisionWitness')
  assert.equal(review.isConfirmed(revised.value.Witness), false)
})

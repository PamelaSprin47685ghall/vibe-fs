// REVIEW-JUDGEMENT-001/008: production-bound Judge execution admission.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as judge from '../../../dist/Mission/Review/OpenCode/JudgeSurface.js'

const executionDecision = (overrides = {}) => judge.decideExecution(
  overrides.role ?? 'Reviewer',
  overrides.sessionId ?? 'ses-reviewer',
  overrides.submitted ?? false,
  overrides.verdict ?? 'PERFECT',
  overrides.toolCallId ?? 'call-review',
  overrides.providerRunId ?? 'run-review',
  overrides.physicalUserMessageId ?? 'physical-review',
)

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_execution_decision_requires_complete_exact_identity', () => {
  assert.deepEqual(executionDecision(), {
    decision: 'Proceed',
    rejection: '',
    sessionId: 'ses-reviewer',
    physicalUserMessageId: 'physical-review',
    providerRunId: 'run-review',
    toolCallId: 'call-review',
    verdict: 'Perfect',
  })
  assert.equal(executionDecision({ verdict: 'REVISE' }).decision, 'Proceed')

  for (const physicalUserMessageId of ['', ' ', '\t\n']) {
    assert.deepEqual(executionDecision({ physicalUserMessageId }), {
      decision: 'Refused',
      rejection: 'CouldNotBind',
    })
  }

  assert.equal(executionDecision({ sessionId: '' }).rejection, 'NoActiveIdentity')
  assert.equal(executionDecision({ toolCallId: '' }).rejection, 'CouldNotBind')
  assert.equal(executionDecision({ providerRunId: '' }).rejection, 'CouldNotBind')
  assert.equal(executionDecision({ role: 'Coder' }).rejection, 'NotFromReviewer')
})

test('WHAT[REVIEW-JUDGEMENT-008] JUDGE_execution_dedupe_is_exact_request_scoped', () => {
  try {
    judge.clearVerdictSubmissions()
    judge.markVerdictSubmitted('ses-reviewer', 'physical-review-1')

    assert.equal(
      executionDecision({
        submitted: judge.hasVerdictSubmitted('ses-reviewer', 'physical-review-1'),
        physicalUserMessageId: 'physical-review-1',
      }).decision,
      'AlreadyJudged',
    )
    assert.equal(
      executionDecision({
        submitted: judge.hasVerdictSubmitted('ses-reviewer', 'physical-review-2'),
        physicalUserMessageId: 'physical-review-2',
      }).decision,
      'Proceed',
    )
  } finally {
    judge.clearVerdictSubmissions()
  }
})

test('WHAT[REVIEW-JUDGEMENT-001] mutation_canary_blank_physical_identity_guard_is_observable', () => {
  const assertBlankRejected = (decide) => {
    assert.equal(decide({ physicalUserMessageId: '   ' }).rejection, 'CouldNotBind')
  }

  assertBlankRejected(executionDecision)
  assert.throws(
    () => assertBlankRejected((input) => executionDecision({ ...input, physicalUserMessageId: 'mutant-bypass' })),
    { name: 'AssertionError' },
  )
})

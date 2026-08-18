import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import * as judge from '../../../dist/Mission/Review/OpenCode/JudgeSurface.js'

const hostContext = ({ sessionId = 'ses-reviewer', toolCallId, providerRunId } = {}) => ({
  sessionID: sessionId,
  agent: 'fast-reviewer',
  ...(toolCallId === undefined ? {} : { callID: toolCallId }),
  ...(providerRunId === undefined ? {} : { messageID: providerRunId }),
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_spec_exposes_the_verdict_input_and_public_tool_identity', () => {
  const contract = judge.contract('English')
  assert.equal(contract.name, 'judge')
  assert.match(contract.description, /PERFECT or REVISE/)
  assert.match(contract.description, /does not mutate source/)
  assert.deepEqual(contract.arguments[0], { name: 'verdict', values: ['PERFECT', 'REVISE'] })
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_invalid_input_is_rejected_as_a_natural_consequence', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')
    const result = await hooks.tool.judge.execute({ verdict: 'APPROVE' }, hostContext())
    assert.match(result, /(?:judgment was not received|你的判断未被收下)/i)
    assert.match(result, /(?:PERFECT or REVISE|PERFECT 或 REVISE)/i)
    assert.doesNotMatch(result, /\berror\s*=/)
  })
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_missing_input_is_rejected_as_a_natural_consequence', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')
    const result = await hooks.tool.judge.execute({}, hostContext())
    assert.match(result, /(?:judgment was not received|你的判断未被收下)/i)
    assert.match(result, /(?:PERFECT or REVISE|PERFECT 或 REVISE)/i)
    assert.doesNotMatch(result, /\berror\s*=/)
  })
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_is_unavailable_to_non_reviewer_sessions', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = await hooks.tool.judge.execute({ verdict: 'REVISE' }, hostContext({ sessionId: 'ses-manager' }))
    assert.match(result, /(?:did not come from a Reviewer|并非来自 Reviewer|调用方权威确立之前|authority is established)/i)
    assert.doesNotMatch(result, /\berror\s*=/)
  })
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_empty_session_is_rejected_before_role_resolution', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')
    const result = await hooks.tool.judge.execute({ verdict: 'REVISE' }, hostContext({ sessionId: '' }))
    assert.match(result, /(?:authority is established|no active identity|没有有效身份|调用方权威确立之前)/i)
    assert.doesNotMatch(result, /\berror\s*=/)
  })
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_reviewer_requires_a_tool_call_id_before_review_submission', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')
    const result = await hooks.tool.judge.execute({ verdict: 'REVISE' }, hostContext({ providerRunId: 'run-1' }))
    assert.match(result, /(?:could not be bound to the current review turn|无法绑定到当前审查轮次)/i)
    assert.doesNotMatch(result, /\berror\s*=/)
  })
})

test('WHAT[REVIEW-JUDGEMENT-001] JUDGE_subsequent_call_returns_already_judged_message', async () => {
  try {
    await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
      await acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')
      judge.markVerdictSubmitted('ses-reviewer')
      const result = await hooks.tool.judge.execute({ verdict: 'PERFECT' }, hostContext({ toolCallId: 'call-1', providerRunId: 'run-1' }))
      assert.match(result, /(?:You have already made a judgment, please conclude the conversation|你已经做出过判断了，现在请你结束对话)/i)
      assert.doesNotMatch(result, /(?:judgment was not received|你的判断未被收下)/i)
      assert.ok(runtime.abortedIds.includes('ses-reviewer'), 'reviewer session must be interrupted/aborted on subsequent judge call')
    })
  } finally {
    judge.clearVerdictSessions()
  }
})

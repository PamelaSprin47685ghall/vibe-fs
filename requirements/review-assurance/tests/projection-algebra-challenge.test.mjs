// REVIEW-ASSURANCE-002: skeptical challenge is the first judge call's typed tool result, never a provider-projection intent.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(new URL(`../../../${path}`, import.meta.url), 'utf8')

test('WHAT[REVIEW-ASSURANCE-002] PROJ_008_review_challenge_is_absent_from_provider_projection_DSL', () => {
  for (const path of [
    'src/Wanxiangshu/Participant/Provider/Projection/Intent.fs',
    'src/Wanxiangshu/Participant/Provider/Projection/Planner.fs',
    'src/Wanxiangshu/Participant/Provider/Projection/Renderer.fs',
  ]) {
    assert.doesNotMatch(read(path), /AppendReviewChallenge|ChallengeIntent|ConflictingReviewChallenge/)
  }
})

test('WHAT[REVIEW-ASSURANCE-002] PROJ_008_review_challenge_is_the_typed_judge_reply_not_a_second_prompt', () => {
  const workflow = read('src/Wanxiangshu/Mission/Review/Barrier/Reverify.fs')
  const judgeTool = read('src/Wanxiangshu/Mission/Review/OpenCode/JudgeTool.fs')
  const host = read('src/Wanxiangshu/Mission/Finality/OpenCode/HostPort.fs')

  assert.match(workflow, /firstDelivery\.Challenge\(\)/)
  assert.match(judgeTool, /let challenge \(\) =/)
  assert.match(judgeTool, /finish \(challenged context\)/)
  assert.doesNotMatch([workflow, judgeTool, host].join('\n'), /ReviewJudgementReply|ReviewConfirmation|sendContinuationResult|SendChallenge/)
})

test('WHAT[REVIEW-ASSURANCE-002] Finality reviewer terminal wait is future-only so a reused reviewer cannot replay the previous request terminal', () => {
  const terminalOwner = read('src/Wanxiangshu/Mission/Review/OpenCode/TerminalAwait.fs')
  const finalityHost = read('src/Wanxiangshu/Mission/Finality/OpenCode/HostPort.fs')
  const changeHost = read('src/Wanxiangshu/Change/Host/Host.fs')

  assert.match(terminalOwner, /sessions\.SubscribeFutureTerminal\(/)
  assert.doesNotMatch(terminalOwner, /sessions\.SubscribeTerminal\(/)
  assert.match(finalityHost, /ReviewerTerminalAwait\.awaitFuture scope\.Journal scope\.Sessions occasion reviewerTimeoutMs/)
  assert.match(changeHost, /ReviewerTerminalAwait\.awaitFuture deps\.Journal deps\.Sessions occasion Distillation\.AwaitAgentTimeoutMs/)
  assert.doesNotMatch([finalityHost, changeHost].join('\n'), /Subscribe(?:Future)?Terminal\(/)
})

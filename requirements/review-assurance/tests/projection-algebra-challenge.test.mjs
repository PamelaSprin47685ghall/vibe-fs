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
  const host = read('src/Wanxiangshu/Mission/Finality/OpenCode/HostPort.fs')
  const start = host.indexOf('let awaitTerminal reviewerSessionId')
  const end = host.indexOf('let sendMissingJudgementNudge', start)
  assert.ok(start >= 0 && end > start, 'FinalityHostPort must keep a named reviewer terminal boundary')

  const awaitTerminal = host.slice(start, end)
  assert.match(awaitTerminal, /scope\.Sessions\.SubscribeFutureTerminal\(/)
  assert.doesNotMatch(awaitTerminal, /scope\.Sessions\.SubscribeTerminal\(/)
})

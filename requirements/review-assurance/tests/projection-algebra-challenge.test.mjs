// Split from tests/unit/context/projection-algebra.test.mjs (cutover Wave 2a); owner: review-assurance.
//
// PROJ-008 Step5 production byte contracts: AppendReviewChallenge must emit the
// REVIEW-003 ChallengeIntent.Prompt bytes (`# <text>\n`) for seal/nudge parity —
// the production `challenge.prompt` — and a custom Prompt is emitted
// verbatim.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'

const challenge = review.challengeObject('English')
const REVIEW_CHALLENGE_PROMPT = challenge.prompt

const semanticView = (raw) => projection.decodeMessages(raw)
const wireOf = (raw) => projection.decodeMessages(raw).messages

const stage3Snapshot = (raw, extras = {}) =>
  projection.projectionSnapshot(semanticView(raw), {
    committedPrefix: extras.committed,
    blogFrames: extras.blogFrames ?? [],
    transportMessages: extras.transportMessages ?? [],
    hostReanchor: extras.hostReanchor,
  })

test('WHAT[REVIEW-ASSURANCE-002] PROJ_008_step5_AppendReviewChallenge_production_bytes_are_Prompt', () => {
  assert.equal(challenge.prompt, REVIEW_CHALLENGE_PROMPT)
  assert.equal(REVIEW_CHALLENGE_PROMPT, `# ${challenge.text}\n`)

  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'task' }] }]
  const snapshot = stage3Snapshot(raw)
  const intent = projection.appendReviewChallenge({ textVersion: challenge.textVersion, prompt: challenge.prompt })
  const view = projection.renderMessages(snapshot, wireOf(raw), [intent])
  const last = view[view.length - 1]
  assert.equal(last?.role, 'user')
  assert.equal(
    last?.parts[0]?.text,
    REVIEW_CHALLENGE_PROMPT,
    'AppendReviewChallenge must emit ChallengeIntent.Prompt bytes for seal/nudge parity',
  )
})

test('WHAT[REVIEW-ASSURANCE-002] PROJ_008_step5_AppendReviewChallenge_emits_intent_Prompt', () => {
  const custom = '# localized-challenge\n'
  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'task' }] }]
  const snapshot = stage3Snapshot(raw)
  const intent = projection.appendReviewChallenge({ textVersion: 1, prompt: custom })
  const view = projection.renderMessages(snapshot, wireOf(raw), [intent])
  const last = view[view.length - 1]
  assert.equal(last?.parts[0]?.text, custom)
})

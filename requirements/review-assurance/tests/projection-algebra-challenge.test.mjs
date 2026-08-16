// Split from tests/unit/context/projection-algebra.test.mjs (cutover Wave 2a); owner: review-assurance.
//
// PROJ-008 Step5 production byte contracts: AppendReviewChallenge must emit the
// REVIEW-003 ChallengeIntent.Prompt bytes (`# <text>\n`) for seal/nudge parity —
// the production `reviewChallenge.prompt` — and a custom Prompt is emitted
// verbatim.

import assert from 'node:assert/strict'
import test from 'node:test'
import { projectionAlgebra, projectionIntent, projectionSnapshot, providerProjection, reviewChallenge, toList } from '../../verification-system/tests/support/domain.mjs'

const REVIEW_CHALLENGE_PROMPT = reviewChallenge.prompt

const semanticView = (raw) => providerProjection.toSemantic(providerProjection.decodeMessageView(toList(raw)))
const wireOf = (raw) => providerProjection.decodeMessageView(toList(raw)).Messages

const stage3Snapshot = (raw, extras = {}) =>
  projectionSnapshot.of({
    currentProjection: semanticView(raw),
    committedPrefix: extras.committed,
    blogFrames: extras.blogFrames ?? [],
    transportMessages: extras.transportMessages ?? [],
    hostReanchor: extras.hostReanchor,
  })

test('WHAT[REVIEW-ASSURANCE-002] PROJ_008_step5_AppendReviewChallenge_production_bytes_are_Prompt', () => {
  assert.equal(reviewChallenge.prompt, REVIEW_CHALLENGE_PROMPT)
  assert.equal(REVIEW_CHALLENGE_PROMPT, `# ${reviewChallenge.text}\n`)

  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'task' }] }]
  const snapshot = stage3Snapshot(raw)
  const intent = projectionIntent.appendReviewChallenge({ TextVersion: reviewChallenge.textVersion })
  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
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
  const intent = projectionIntent.appendReviewChallenge({ TextVersion: 1, Prompt: custom })
  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  const last = view[view.length - 1]
  assert.equal(last?.parts[0]?.text, custom)
})

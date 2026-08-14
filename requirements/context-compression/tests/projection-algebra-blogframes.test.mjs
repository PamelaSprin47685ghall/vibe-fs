// Split from tests/unit/context/projection-algebra.test.mjs (cutover Wave 2a); owner: context-compression.
//
// PROJ-008 step 3b: algebra InsertBlogFrames ≡ CompanionProjectionBuilder —
// the InsertBlogFrames intent renders exactly like the production Companion
// builder (workingRecord historic frames, squash instruction, digest parity).

import assert from 'node:assert/strict'
import test from 'node:test'
import { companionProjection as companionProj, companionPrompt, projectionAlgebra, projectionIntent, projectionSnapshot, providerProjection, toList } from '../../verification-system/tests/support/domain.mjs'

const semanticView = (raw) => providerProjection.toSemantic(providerProjection.decodeMessageView(toList(raw)))

const stage3Snapshot = (raw, extras = {}) =>
  projectionSnapshot.of({
    currentProjection: semanticView(raw),
    committedPrefix: extras.committed,
    blogFrames: extras.blogFrames ?? [],
    transportMessages: extras.transportMessages ?? [],
    hostReanchor: extras.hostReanchor,
  })

const planNames = (intents) => {
  const result = projectionAlgebra.plan(intents)
  assert.equal(result.ok, true, `expected Ok plan, got ${JSON.stringify(result)}`)
  return result.intents
}

test('PROJ_008_step3b_InsertBlogFrames_digest_equiv_to_CompanionProjectionBuilder', () => {
  const spy = (input) => `«${input}»`
  const dataToml = '[[new_work_to_record]]\nuser = "work"'
  const frames = [
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f0', body: 'frame body 0' }),
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f1', body: 'frame body 1' }),
  ]
  const previousTips = [{ field: 'progress', cycleId: 'cycle-1' }]
  const delta = { messageId: 'msg_delta', toml: dataToml }

  const intent = projectionIntent.insertBlogFrames({
    RequestKind: 'normal',
    SquashFrameCount: 0,
    BloggerSessionId: 'ses_y',
    FrameEpoch: 0,
    PhysicalDelta: delta,
    PreviousTips: previousTips,
  })
  const snapshot = stage3Snapshot([], { blogFrames: frames })
  assert.deepEqual(planNames([intent]), ['InsertBlogFrames'])

  const algebraView = projectionAlgebra.renderMessagesWithIntents(snapshot, [], [intent])
  const builderPlan = companionProj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: companionProj.normal,
    frames: frames.map((f) => ({ digest: f.Digest, body: f.Body })),
    delta,
    previousTips,
  })

  assert.deepEqual(
    algebraView.map((m) => m.role),
    builderPlan.roles,
  )
  assert.deepEqual(
    algebraView.map((m) => m.parts[0]?.text),
    builderPlan.texts,
  )
})

test('PROJ_008_step3b_InsertBlogFrames_squash_digest_equiv_to_Builder', () => {
  const spy = (input) => `«${input}»`
  const frames = [
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f0', body: 'frame body 0' }),
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f1', body: 'frame body 1' }),
    projectionSnapshot.blogFrame({ kind: 'Squash', digest: 'sha-f2', body: 'frame body 2' }),
  ]
  const intent = projectionIntent.insertBlogFrames({
    RequestKind: 'squash',
    SquashFrameCount: 2,
    BloggerSessionId: 'ses_y',
    FrameEpoch: 1,
    PhysicalDelta: undefined,
    PreviousTips: [],
  })
  const snapshot = stage3Snapshot([], { blogFrames: frames })
  const algebraView = projectionAlgebra.renderMessagesWithIntents(snapshot, [], [intent])
  const builderPlan = companionProj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: companionProj.squash(2),
    frames: frames.map((f) => ({ digest: f.Digest, body: f.Body })),
  })

  assert.deepEqual(
    algebraView.map((m) => [m.role, m.parts[0]?.text]),
    builderPlan.messages.map((m) => [m.role, m.text]),
  )
  assert.equal(algebraView.at(-1)?.parts[0]?.text, companionPrompt.squashInstruction)
})

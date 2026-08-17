// Split from tests/unit/context/projection-algebra.test.mjs (cutover Wave 2a); owner: context-compression.
//
// PROJ-008 step 3b: algebra InsertBlogFrames ≡ CompanionProjectionBuilder —
// the InsertBlogFrames intent renders exactly like the production Companion
// builder (workingRecord historic frames, squash instruction, digest parity).

import assert from 'node:assert/strict'
import test from 'node:test'
import * as algebra from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as companionProj from '../../../dist/Context/Companion/ProjectionSurface.js'

const projectionSnapshot = algebra

const stage3Snapshot = (blogFrames = []) =>
  algebra.projectionSnapshot(
    { providerId: null, modelId: null, variant: null, tools: [], system: [], messages: [] },
    { blogFrames, transportMessages: [], hostReanchor: null },
  )

const planNames = (intents) => {
  const result = algebra.plan(intents)
  assert.equal(result.ok, true, `expected Ok plan, got ${JSON.stringify(result)}`)
  return result.intents
}

test('WHAT[CONTEXT-COMPRESSION-012] PROJ_008_step3b_InsertBlogFrames_digest_equiv_to_CompanionProjectionBuilder', () => {
  const spy = (input) => `«${input}»`
  const dataToml = '[[new_work_to_record]]\nuser = "work"'
  const frames = [
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f0', body: 'frame body 0' }),
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f1', body: 'frame body 1' }),
  ]
  const previousTips = [{ field: 'progress', cycleId: 'cycle-1' }]
  const delta = { messageId: 'msg_delta', toml: dataToml }

  const intent = algebra.insertBlogFrames({
    requestKind: 'normal',
    squashFrameCount: 0,
    bloggerSessionId: 'ses_y',
    frameEpoch: 0,
    physicalDelta: delta,
    previousTips,
    normalInstructionLines: companionProj.normalInstructionLines,
    squashInstructionLines: companionProj.squashInstructionLines,
  })
  const snapshot = stage3Snapshot(frames)
  assert.deepEqual(planNames([intent]), ['InsertBlogFrames'])

  const algebraView = algebra.renderMessages(snapshot, [], [intent])
  const builderPlan = companionProj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: 'normal',
    frames: frames.map((f) => ({ digest: f.digest, body: f.body })),
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

test('WHAT[CONTEXT-COMPRESSION-012] PROJ_008_step3b_InsertBlogFrames_squash_digest_equiv_to_Builder', () => {
  const spy = (input) => `«${input}»`
  const frames = [
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f0', body: 'frame body 0' }),
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f1', body: 'frame body 1' }),
    projectionSnapshot.blogFrame({ kind: 'Squash', digest: 'sha-f2', body: 'frame body 2' }),
  ]
  const intent = algebra.insertBlogFrames({
    requestKind: 'squash',
    squashFrameCount: 2,
    bloggerSessionId: 'ses_y',
    frameEpoch: 1,
    physicalDelta: null,
    previousTips: [],
    normalInstructionLines: companionProj.normalInstructionLines,
    squashInstructionLines: companionProj.squashInstructionLines,
  })
  const snapshot = stage3Snapshot(frames)
  const algebraView = algebra.renderMessages(snapshot, [], [intent])
  const builderPlan = companionProj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: companionProj.squash(2),
    frames: frames.map((f) => ({ digest: f.digest, body: f.body })),
  })

  assert.deepEqual(
    algebraView.map((m) => [m.role, m.parts[0]?.text]),
    builderPlan.messages.map((m) => [m.role, m.text]),
  )
  assert.equal(algebraView.at(-1)?.parts[0]?.text, companionProj.squashInstruction)
})

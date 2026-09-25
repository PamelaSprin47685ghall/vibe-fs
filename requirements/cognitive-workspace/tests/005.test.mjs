import assert from 'node:assert/strict'
import test from 'node:test'
import * as fold from '../../../dist/Participant/Cognition/FoldSurface.js'
import * as runtime from '../../../dist/Participant/Cognition/RuntimeSurface.js'

// cognitive-workspace-005: one owner's commits fold onto one another inside a
// single serial domain, and one owner committing never blocks or disturbs
// another owner. The fold the fake journal uses is the production one, so these
// assertions pin the runtime, not a copy of its rules.

const makeJournal = () => {
  const blobs = new Map()
  const projections = new Map()
  let blobSeq = 0
  return {
    blobs,
    projections,
    writeBlob: async (content) => {
      const snapshotRef = `blob-${++blobSeq}`
      blobs.set(snapshotRef, content)
      return { ok: true, snapshotRef, snapshotDigest: `digest-${snapshotRef}` }
    },
    appendCommit: async (commit) => {
      const current = projections.get(commit.ownerKey) ?? null
      const folded = fold.CognitiveFactFold_fold(current, {
        case: 'AssumePhaseCommitted',
        fields: commit,
      })
      if (!folded.ok) return { ok: false, error: folded.error }
      projections.set(commit.ownerKey, folded.value[0].projection)
      return { ok: true }
    },
    readProjection: (ownerKey) => projections.get(ownerKey) ?? null,
    readBlob: async (snapshotRef) =>
      blobs.has(snapshotRef)
        ? { ok: true, value: blobs.get(snapshotRef) }
        : { ok: false, error: `blob ${snapshotRef} is missing` },
  }
}

const ofSession = (sessionId, incumbencyId) => ({ sessionId, incumbencyId })
const commit = (rt, owner, call) => runtime.CognitiveRuntime_commit(rt, owner, call)
const currentCanvas = (rt, owner) => runtime.CognitiveRuntime_currentCanvas(rt, owner)

test('WHAT[cognitive-workspace-005] concurrent commits for one owner fold onto one another in one serial domain', async () => {
  const journal = makeJournal()
  const rt = runtime.CognitiveRuntime_create(journal)

  const dispatched = await Promise.all([
    commit(rt, ofSession('ses-1'), { toolCallId: 'call-1', inputDigest: 'digest-1', merge: { x: 1 } }),
    commit(rt, ofSession('ses-1'), { toolCallId: 'call-2', inputDigest: 'digest-2', merge: { y: 2 } }),
  ])

  assert.deepEqual(
    dispatched.map((outcome) => outcome.ok),
    [true, true]
  )
  assert.deepEqual(
    dispatched.map((outcome) => outcome.ordinal).sort(),
    [1, 2]
  )

  // The second commit read the first commit's canvas, so its write is stacked on top
  // of it instead of overwriting it from an empty start.
  const view = await currentCanvas(rt, ofSession('ses-1'))
  assert.equal(view.ok, true, view.error ?? '')
  assert.deepEqual(JSON.parse(view.canvasJson), { x: 1, y: 2 })
})

test('WHAT[cognitive-workspace-005] one owner committing never blocks or disturbs another owner', async () => {
  const journal = makeJournal()
  const rt = runtime.CognitiveRuntime_create(journal)

  const dispatched = await Promise.all([
    commit(rt, ofSession('ses-1'), { toolCallId: 'call-1', inputDigest: 'digest-1', merge: { x: 1 } }),
    commit(rt, ofSession('ses-2'), { toolCallId: 'call-2', inputDigest: 'digest-2', merge: { y: 2 } }),
  ])

  assert.deepEqual(
    dispatched.map((outcome) => outcome.ok),
    [true, true]
  )
  // Each owner advanced from its own committed ordinal: no shared counter, no queue.
  assert.deepEqual(
    dispatched.map((outcome) => outcome.ordinal),
    [1, 1]
  )

  const first = await currentCanvas(rt, ofSession('ses-1'))
  const second = await currentCanvas(rt, ofSession('ses-2'))
  assert.deepEqual(JSON.parse(first.canvasJson), { x: 1 })
  assert.deepEqual(JSON.parse(second.canvasJson), { y: 2 })
})

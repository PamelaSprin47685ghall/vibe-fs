import assert from 'node:assert/strict'
import test from 'node:test'
import * as fold from '../../../dist/Participant/Cognition/FoldSurface.js'
import * as runtime from '../../../dist/Participant/Cognition/RuntimeSurface.js'

// cognitive-workspace-001/005/006: the canvas's owner is the physical session, a
// Life turnover inside one session keeps the canvas and the ordinal, the canvas
// survives a restart by recovering from committed facts, a failed recovery is
// a refusal rather than an empty canvas, and one owner's commits fold onto one
// another inside a single serial domain. The fold the fake journal uses is the
// production one, so these assertions pin the runtime, not a copy of its rules.

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

test('WHAT[cognitive-workspace-001] the canvas owner is the physical session, not the incumbency', () => {
  const underOneLife = runtime.CognitiveOwner_key(ofSession('ses-1', 'life-a'))
  const underNextLife = runtime.CognitiveOwner_key(ofSession('ses-1', 'life-b'))
  assert.equal(underOneLife, underNextLife)
  assert.equal(underOneLife, 'ses-1')
  assert.notEqual(runtime.CognitiveOwner_key(ofSession('ses-2', 'life-a')), underOneLife)
})

// A suicide or a retirement turns the session's Life over without touching the
// canvas: the next Life reads what the previous Life committed and keeps counting
// from the owner's committed ordinal, and the retired Life left nothing cleared.
test('WHAT[cognitive-workspace-001] a Life turnover inside one session keeps the canvas and continues the ordinal', async () => {
  const journal = makeJournal()
  const rt = runtime.CognitiveRuntime_create(journal)

  const first = await commit(rt, ofSession('ses-1', 'life-a'), {
    toolCallId: 'call-1',
    inputDigest: 'digest-1',
    merge: { carried: 'from the first life' },
  })
  assert.equal(first.ok, true, first.error ?? '')
  assert.equal(first.ordinal, 1)

  const carried = await currentCanvas(rt, ofSession('ses-1', 'life-b'))
  assert.equal(carried.ok, true, carried.error ?? '')
  assert.deepEqual(JSON.parse(carried.canvasJson), { carried: 'from the first life' })

  const second = await commit(rt, ofSession('ses-1', 'life-b'), {
    toolCallId: 'call-2',
    inputDigest: 'digest-2',
    merge: { continued: 'under the second life' },
  })
  assert.equal(second.ok, true, second.error ?? '')
  // The next Life commits on the same canvas, so the ordinal continues from the
  // owner's committed ordinal instead of restarting from one.
  assert.equal(second.ordinal, 2)

  const merged = await currentCanvas(rt, ofSession('ses-1', 'life-b'))
  assert.equal(merged.ok, true, merged.error ?? '')
  assert.deepEqual(JSON.parse(merged.canvasJson), {
    carried: 'from the first life',
    continued: 'under the second life',
  })

  // Retirement is not a clearing either: the canvas the old Life committed is still
  // the session's canvas after the new Life has committed on top of it.
  const afterTurnover = await currentCanvas(rt, ofSession('ses-1', 'life-a'))
  assert.equal(afterTurnover.ok, true, afterTurnover.error ?? '')
  assert.deepEqual(JSON.parse(afterTurnover.canvasJson), {
    carried: 'from the first life',
    continued: 'under the second life',
  })
})

test('WHAT[cognitive-workspace-001] one session’s commit does not touch another session’s canvas or ordinal', async () => {
  const journal = makeJournal()
  const rt = runtime.CognitiveRuntime_create(journal)

  const first = await commit(rt, ofSession('ses-1'), {
    toolCallId: 'call-1',
    inputDigest: 'digest-1',
    merge: { a: 1 },
  })
  assert.equal(first.ok, true, first.error ?? '')
  assert.equal(first.ordinal, 1)

  const second = await commit(rt, ofSession('ses-2'), {
    toolCallId: 'call-2',
    inputDigest: 'digest-2',
    merge: { b: 2 },
  })
  assert.equal(second.ok, true, second.error ?? '')
  // The ordinal belongs to the owner's committed ordinal, not to a process counter.
  assert.equal(second.ordinal, 1)

  const view = await currentCanvas(rt, ofSession('ses-1'))
  assert.equal(view.ok, true, view.error ?? '')
  assert.deepEqual(JSON.parse(view.canvasJson), { a: 1 })
})

test('WHAT[cognitive-workspace-001] a rebuilt runtime recovers the committed canvas and continues the ordinal', async () => {
  const journal = makeJournal()
  const before = runtime.CognitiveRuntime_create(journal)

  const first = await commit(before, ofSession('ses-1'), {
    toolCallId: 'call-1',
    inputDigest: 'digest-1',
    merge: { a: 1 },
    todos: [{ content: 'carry the canvas across the restart', status: 'in_progress' }],
  })
  assert.equal(first.ok, true, first.error ?? '')
  assert.equal(first.ordinal, 1)

  // A second runtime over the same journal is a restart: the process memory is gone,
  // the committed facts are not.
  const after = runtime.CognitiveRuntime_create(journal)

  const recovered = await currentCanvas(after, ofSession('ses-1'))
  assert.equal(recovered.ok, true, recovered.error ?? '')
  assert.deepEqual(JSON.parse(recovered.canvasJson), { a: 1 })
  // The declaration travels in the same envelope as the canvas.
  assert.deepEqual(recovered.todos, [
    { content: 'carry the canvas across the restart', status: 'in_progress', priority: 'medium' },
  ])

  const second = await commit(after, ofSession('ses-1'), {
    toolCallId: 'call-2',
    inputDigest: 'digest-2',
    merge: { b: 2 },
  })
  assert.equal(second.ok, true, second.error ?? '')
  assert.equal(second.ordinal, 2)

  const merged = await currentCanvas(after, ofSession('ses-1'))
  assert.deepEqual(JSON.parse(merged.canvasJson), { a: 1, b: 2 })
})

test('WHAT[cognitive-workspace-001] a projection whose snapshot blob is missing is refused, not answered empty', async () => {
  const journal = makeJournal()
  const rt = runtime.CognitiveRuntime_create(journal)

  const first = await commit(rt, ofSession('ses-1'), {
    toolCallId: 'call-1',
    inputDigest: 'digest-1',
    merge: { a: 1 },
  })
  assert.equal(first.ok, true, first.error ?? '')

  // The durable payload is gone while the projection still claims it: the owner must
  // refuse rather than silently restart from an empty canvas.
  journal.blobs.clear()
  const reboot = runtime.CognitiveRuntime_create(journal)

  const view = await currentCanvas(reboot, ofSession('ses-1'))
  assert.equal(view.ok, false)
  assert.match(view.error, /cannot recover the committed canvas/)
  assert.doesNotMatch(view.error, /\{/)

  // The refusal is a refusal to answer, not a commit: nothing was written.
  assert.equal(journal.projections.get('ses-1').ordinal, 1)
})

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

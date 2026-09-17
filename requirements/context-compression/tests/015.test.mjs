import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");

const entryWithEnforcement = ({
  epoch = 0,
  from,
  to,
  cutoffFrom,
  cutoffTo,
  digest = `d-${cutoffTo}`,
  n = 1,
}) => ({
  epoch,
  previous: from,
  next: to,
  previousCutoff: cutoffFrom,
  nextCutoff: cutoffTo,
  digest,
  frame: blog.frame({ kind: 'Entry', digest: `sha-e${n}`, ref: `blob-e${n}`, coveredFrom: from, coveredThrough: to }),
})
const foldOk = (requests) => {
  let state = blog.empty
  for (const request of requests) {
    const result = blog.applyEntry(request, state)
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    state = result.value
  }
  return { Blog: state }
}
const foldErr = (requests) => {
  let state = blog.empty
  for (const request of requests) {
    const result = blog.applyEntry(request, state)
    if (!result.ok) return result.error
    state = result.value
  }
  assert.fail('expected fold rejection')
}

test('WHAT[CONTEXT-COMPRESSION-015] ENFORCER_045_coverage_strictly_advances_across_commits', () => {
  const s = foldOk([
    entryWithEnforcement({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1, run: 'msg_r1' }),
    entryWithEnforcement({ from: 1, to: 3, cutoffFrom: 1, cutoffTo: 2, n: 2, run: 'msg_r2' }),
  ])

  assert.equal(blog.frameCount(s.Blog), 2)
  assert.equal(blog.coverage(s.Blog).ingestedThroughSequence, 3)
  assert.equal(blog.coverage(s.Blog).cutoff, 2)
})
test('WHAT[CONTEXT-COMPRESSION-015] ENFORCER_045_zero_advance_rejected', () => {
  const error = foldErr([
    entryWithEnforcement({ from: 1, to: 1, cutoffFrom: 0, cutoffTo: 0, n: 1, run: 'msg_zero' }),
  ])
  assert.ok(error, 'zero advance must be rejected (CTX-011)')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");

const entryFrame = (n) => blog.frame({
  kind: 'Entry',
  digest: `sha-entry-${n}`,
  ref: `blob-entry-${n}`,
  coveredFrom: n - 1,
  coveredThrough: n,
})
const squashFrame = (n) => blog.frame({
  kind: 'Squash',
  digest: `sha-entry-${n}`,
  ref: `blob-squash-${n}`,
  coveredFrom: 0,
  coveredThrough: 2,
})
const commitEntry = (state, { epoch = 0, from, to, cutoffFrom, cutoffTo, digest = `digest-${cutoffTo}`, n = 1 }) =>
  blog.applyEntry(
    {
      epoch,
      previous: from,
      next: to,
      previousCutoff: cutoffFrom,
      nextCutoff: cutoffTo,
      digest,
      frame: entryFrame(n),
    },
    state,
  )
function threeEntries() {
  let state = blog.empty

  for (let i = 1; i <= 3; i += 1) {
    const result = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i })
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    state = result.value
  }

  return state
}

test('WHAT[CONTEXT-COMPRESSION-015] PERSIST_010_empty_projection_covers_nothing', () => {
  assert.equal(blog.frameCount(blog.empty), 0)
  assert.equal(blog.hasCoverage(blog.empty), false)
  assert.deepEqual(blog.coverage(blog.empty), {
    ingestedThroughSequence: 0,
    cutoff: 0,
    digest: '',
    coverableFrames: 0,
  })
})
test('WHAT[CONTEXT-COMPRESSION-015] COMPANION_008_entry_appends_frame_and_advances_coverage_together', () => {
  const result = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, digest: 'd1' })

  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(blog.frameCount(result.value), 1)
  assert.deepEqual(blog.frameKinds(result.value), ['Entry'])
  assert.deepEqual(blog.coverage(result.value), {
    ingestedThroughSequence: 1,
    cutoff: 1,
    digest: 'd1',
    coverableFrames: 1,
  })

  // The cutoff advanced, so the frame it produced is coverable: a probe may build
  // FrozenRecordPrefix from it.
  assert.deepEqual(blog.coverableFrameKinds(result.value), ['Entry'])

  const [stamped] = blog.frames(result.value)
  assert.equal(stamped.coveredFrom, 0)
  assert.equal(stamped.coveredThrough, 1)
})
test('WHAT[CONTEXT-COMPRESSION-015] CTX_011_entry_that_consumed_nothing_is_refused', () => {
  // An entry whose ingest sequence did not move would let the same delta be
  // blogged forever: the next offer would compute the identical chunk.
  const same = commitEntry(blog.empty, { from: 0, to: 0, cutoffFrom: 0, cutoffTo: 0 })
  assert.deepEqual(same, { ok: false, error: 'IngestCursorNotAdvanced' })

  // Backwards is the same refusal, not a separate one — both mean "did not advance".
  const first = commitEntry(blog.empty, { from: 0, to: 2, cutoffFrom: 0, cutoffTo: 2 }).value
  const back = commitEntry(first, { from: 2, to: 1, cutoffFrom: 2, cutoffTo: 2 })
  assert.deepEqual(back, { ok: false, error: 'IngestCursorNotAdvanced' })
})
test('WHAT[CONTEXT-COMPRESSION-015] PERSIST_010_entry_whose_previous_cursor_disagrees_is_refused', () => {
  // The writer's view of where the Companion was must match the projection's.
  // A mismatch means two writers, or a line replayed out of order.
  const first = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1 }).value
  const stale = commitEntry(first, { from: 0, to: 2, cutoffFrom: 1, cutoffTo: 2 })

  assert.deepEqual(stale, { ok: false, error: 'IngestCursorMismatch' })
})
test('WHAT[CONTEXT-COMPRESSION-015] CTX_011_coverage_may_not_retreat', () => {
  const first = commitEntry(blog.empty, { from: 0, to: 2, cutoffFrom: 0, cutoffTo: 2 }).value

  // Claiming an earlier previous-cutoff than the projection holds.
  const wrongPrevious = commitEntry(first, { from: 2, to: 3, cutoffFrom: 1, cutoffTo: 3 })
  assert.deepEqual(wrongPrevious, { ok: false, error: 'CoverageRetreated' })

  // Moving the cutoff backwards outright.
  const backwards = commitEntry(first, { from: 2, to: 3, cutoffFrom: 2, cutoffTo: 1 })
  assert.deepEqual(backwards, { ok: false, error: 'CoverageRetreated' })
})
test('WHAT[CONTEXT-COMPRESSION-015] PERSIST_010_entry_written_against_a_replaced_frame_epoch_is_refused', () => {
  // A squash replaced the frame sequence. An entry still carrying the old epoch
  // describes frames that no longer exist.
  const first = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1 }).value
  const squashed = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 1, frame: squashFrame(1) }, first).value

  const stale = commitEntry(squashed, { epoch: 0, from: 1, to: 2, cutoffFrom: 1, cutoffTo: 2 })
  assert.deepEqual(stale, { ok: false, error: 'StaleFrameEpoch' })

  const current = commitEntry(squashed, { epoch: 1, from: 1, to: 2, cutoffFrom: 1, cutoffTo: 2 })
  assert.equal(current.ok, true, current.ok ? '' : current.error)
})
}

// tests/unit/Context/blog-projection.test.mjs — PERSIST-010 frame folds.
//
// COMPANION-008 makes frame append and coverage advance ONE domain commit. The
// projection is where that is enforced, so these tests pin the refusals as much
// as the successes: a fold that absorbs an impossible line cannot fail closed,
// and PERSIST-004 requires a corrupt journal to stop startup rather than be
// quietly repaired.
//
// COMPANION-003: the RecordCoverage advance is an XTrace cursor SEQUENCE
// (strictly monotonic int64), not a turn/part pair — the two coverage claims
// (record vs prefix) prove different things and neither derives from the other.
// The cutoff remains the complete-turn boundary; a mid-turn chunk advances the
// sequence and leaves the cutoff put.
//
// Every rejection is asserted by NAME. The rejection union carries payloads for
// diagnostics, but a test that matched on the payload would still pass if the
// case changed underneath it.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Context/Companion/Blogger/FrameSurface.js'

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

/** Commit one entry whose record coverage advances from `from` to `to`. */
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

// ── the empty state is a claim, not a placeholder ───────────────────────────

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

// ── entry: append and coverage are one commit ───────────────────────────────

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

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_sequence_advances_within_one_turn', () => {
  // A large message spans several 200 KiB chunks, so a chunk boundary can fall
  // inside a turn. Those chunks advance the record sequence and must be accepted
  // while the turn cutoff stays put.
  const chunk1 = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 0, digest: '' })
  assert.equal(chunk1.ok, true, chunk1.ok ? '' : chunk1.error)
  assert.deepEqual(blog.coverage(chunk1.value), {
    ingestedThroughSequence: 1,
    cutoff: 0,
    digest: '',
    coverableFrames: 0,
  })
  assert.equal(blog.hasCoverage(chunk1.value), false, 'a half-consumed turn is not coverage a probe may use')

  // The frame exists but is NOT coverable. This is the gap the count closes: the
  // frame describes material the cutoff does not yet claim, so a probe building
  // FrozenRecordPrefix from it would summarise a turn that is also still present raw.
  assert.equal(blog.frameCount(chunk1.value), 1)
  assert.deepEqual(blog.coverableFrameKinds(chunk1.value), [])

  const chunk2 = commitEntry(chunk1.value, { from: 1, to: 2, cutoffFrom: 0, cutoffTo: 0, digest: '', n: 2 })
  assert.equal(chunk2.ok, true, chunk2.ok ? '' : chunk2.error)
  assert.equal(blog.frameCount(chunk2.value), 2)
  assert.deepEqual(blog.coverableFrameKinds(chunk2.value), [], 'still nothing coverable mid-turn')

  // Only the chunk that crosses the turn end advances the cutoff — and it makes
  // every frame so far coverable at once.
  const final = commitEntry(chunk2.value, { from: 2, to: 3, cutoffFrom: 0, cutoffTo: 1, digest: 'd1', n: 3 })
  assert.equal(final.ok, true, final.ok ? '' : final.error)
  assert.deepEqual(blog.coverage(final.value), {
    ingestedThroughSequence: 3,
    cutoff: 1,
    digest: 'd1',
    coverableFrames: 3,
  })
  assert.equal(blog.hasCoverage(final.value), true)
  assert.deepEqual(blog.coverableFrameKinds(final.value), ['Entry', 'Entry', 'Entry'])
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

// ── squash: changes representation, never coverage ──────────────────────────

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_squash_replaces_the_oldest_frames_and_leaves_the_covered_range_alone', () => {
  let state = blog.empty
  for (let i = 1; i <= 4; i += 1) {
    state = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i }).value
  }

  const before = blog.coverage(state)
  assert.equal(blog.frameCount(state), 4)
  assert.equal(before.coverableFrames, 4)

  const result = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 2, frame: squashFrame(1) }, state)
  assert.equal(result.ok, true, result.ok ? '' : result.error)

  // Oldest two collapsed into one Squash frame, newest two untouched and in order.
  assert.deepEqual(blog.frameKinds(result.value), ['Squash', 'Entry', 'Entry'])
  assert.equal(blog.frameCount(result.value), 3)

  const after = blog.coverage(result.value)

  // A squash changes how B is REPRESENTED, not which X turns it covers. So the
  // cutoff, its digest and the record sequence are all untouched.
  assert.deepEqual(
    { ingestedThroughSequence: after.ingestedThroughSequence, cutoff: after.cutoff, digest: after.digest },
    { ingestedThroughSequence: before.ingestedThroughSequence, cutoff: before.cutoff, digest: before.digest },
  )

  // `coverableFrames` DOES move, and must: it is a frame index, and two frames below
  // it became one. Leaving it at 4 would point past the end of a 3-frame list;
  // subtracting 2 would drop the newest covered frame out of the probe's reach.
  assert.equal(after.coverableFrames, 3)
  assert.deepEqual(blog.coverableFrameKinds(result.value), ['Squash', 'Entry', 'Entry'])

  const [merged] = blog.frames(result.value)
  assert.equal(merged.coveredFrom, 0)
  assert.equal(merged.coveredThrough, 2, 'squash unions the replaced frames\' coverage interval')
})

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_a_squash_that_consumes_the_whole_covered_range_leaves_one_coverable_frame', () => {
  // The boundary case the arithmetic has to get right. Squashing every covered frame
  // into one means the covered range is now that single frame — not zero, which would
  // silently disable probes, and not the old count, which would overrun the list.
  let state = blog.empty
  for (let i = 1; i <= 3; i += 1) {
    state = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i }).value
  }

  const collapsed = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 3, frame: squashFrame(1) }, state)
  assert.equal(collapsed.ok, true, collapsed.ok ? '' : collapsed.error)

  assert.deepEqual(blog.frameKinds(collapsed.value), ['Squash'])
  assert.equal(blog.coverage(collapsed.value).coverableFrames, 1)
  assert.equal(blog.coverage(collapsed.value).cutoff, 3, 'the covered X range is unchanged')
  assert.deepEqual(blog.coverableFrameKinds(collapsed.value), ['Squash'])
})

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_a_squash_cannot_make_an_uncovered_frame_coverable', () => {
  // Mid-turn chunks only: nothing is coverable. A squash rewrites those frames but
  // cannot create coverage the cutoff never claimed.
  const chunk1 = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 0, digest: '' }).value
  const chunk2 = commitEntry(chunk1, { from: 1, to: 2, cutoffFrom: 0, cutoffTo: 0, digest: '', n: 2 }).value

  assert.equal(blog.coverage(chunk2).coverableFrames, 0)

  const squashed = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 2, frame: squashFrame(1) }, chunk2)
  assert.equal(squashed.ok, true, squashed.ok ? '' : squashed.error)

  assert.equal(blog.coverage(squashed.value).coverableFrames, 0)
  assert.deepEqual(blog.coverableFrameKinds(squashed.value), [])
  assert.equal(blog.hasCoverage(squashed.value), false)
})

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_squash_width_is_ceil_half_and_does_not_skip_a_single_frame', () => {
  const widthAfter = (count) => {
    let state = blog.empty
    for (let i = 1; i <= count; i += 1) {
      state = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i }).value
    }
    return blog.squashWidth(state)
  }

  assert.equal(blog.squashWidth(blog.empty), 0, 'nothing to squash')
  // m = 1 is NOT skipped: one frame can still be large and redundant enough that
  // a rewrite shortens it materially.
  assert.deepEqual([1, 2, 3, 4, 5, 6].map(widthAfter), [1, 1, 2, 2, 3, 3])
})

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_squash_frames_are_interchangeable_with_entries_so_cascade_works', () => {
  let state = threeEntries()
  // [Entry, Entry, Entry] → squash 2 → [Squash, Entry]
  const first = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 2, frame: squashFrame(1) }, state).value
  assert.deepEqual(blog.frameKinds(first), ['Squash', 'Entry'])

  // A later squash may consume the previous Squash frame alongside an Entry.
  const second = blog.applySquash({ previousEpoch: 1, nextEpoch: 2, count: 2, frame: squashFrame(2) }, first)
  assert.equal(second.ok, true, second.ok ? '' : second.error)
  assert.deepEqual(blog.frameKinds(second.value), ['Squash'])
  assert.equal(Number(blog.frameEpochOf(second.value)), 2)
})

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_squash_count_outside_available_range_is_refused', () => {
  let state = blog.empty
  for (let i = 1; i <= 2; i += 1) {
    state = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i }).value
  }

  for (const count of [0, -1, 3, 99]) {
    assert.deepEqual(
      blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count, frame: squashFrame(1) }, state),
      { ok: false, error: 'CoveredFrameCountOutOfRange' },
      `count ${count} must be refused against 2 available frames`,
    )
  }

  // Collapsing everything into one is a legitimate cascade step, not an error.
  const all = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 2, frame: squashFrame(1) }, state)
  assert.equal(all.ok, true, all.ok ? '' : all.error)
  assert.deepEqual(blog.frameKinds(all.value), ['Squash'])
})

test('WHAT[CONTEXT-COMPRESSION-011] PERSIST_010_squash_epoch_must_be_the_successor', () => {
  const state = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1 }).value

  for (const nextEpoch of [0, 2, 7]) {
    assert.deepEqual(
      blog.applySquash({ previousEpoch: 0, nextEpoch, count: 1, frame: squashFrame(1) }, state),
      { ok: false, error: 'NonSequentialFrameEpoch' },
      `nextEpoch ${nextEpoch} must be refused after epoch 0`,
    )
  }
})

test('WHAT[CONTEXT-COMPRESSION-011] PERSIST_010_squash_written_against_a_stale_epoch_is_refused', () => {
  const state = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1 }).value
  const once = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 1, frame: squashFrame(1) }, state).value

  // A replayed squash carries the epoch it expected, which the projection left.
  const replay = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 1, frame: squashFrame(1) }, once)
  assert.deepEqual(replay, { ok: false, error: 'StaleFrameEpoch' })
})

// ── reanchor: PrefixCoverage voided, RecordCoverage + frames survive ─────────

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_reanchor_zeroes_prefix_coverage_and_keeps_record_coverage', () => {
  let state = threeEntries()
  const framesBefore = blog.frameKinds(state)
  const ingestBefore = blog.coverage(state).ingestedThroughSequence
  assert.equal(blog.hasCoverage(state), true)
  assert.equal(ingestBefore, 3)

  const reanchored = blog.applyReanchor(state)

  // B records work that really happened. What compaction voided is the mapping
  // from B to X turn indices, not the work log — so frames survive untouched.
  assert.deepEqual(blog.frameKinds(reanchored), framesBefore)
  assert.equal(blog.frameCount(reanchored), blog.frameCount(state))

  // PrefixCoverage returns to the origin: Host numbering those positions referred
  // to no longer exists. RecordCoverage (IngestedThrough) is an XTrace cursor and
  // stays put — clearing it would re-feed already-compressed X into Y (COMPANION-008).
  assert.deepEqual(blog.coverage(reanchored), {
    ingestedThroughSequence: ingestBefore,
    cutoff: 0,
    digest: '',
    coverableFrames: 0,
  })
  assert.equal(blog.hasCoverage(reanchored), false)
  assert.deepEqual(blog.coverableFrameKinds(reanchored), [], 'no probe may be built until prefix coverage rebuilds')
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_reanchor_does_not_advance_the_frame_epoch', () => {
  // No frame changed, so a squash already written against the current epoch is
  // still valid after a reanchor. Advancing here would reject it.
  const state = threeEntries()
  const before = blog.frameEpochOf(state)
  const reanchored = blog.applyReanchor(state)

  assert.equal(blog.frameEpochOf(reanchored), before)

  const squash = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 1, frame: squashFrame(1) }, reanchored)
  assert.equal(squash.ok, true, squash.ok ? '' : squash.error)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_prefix_coverage_rebuilds_after_a_reanchor_without_rewinding_ingest', () => {
  // Probe capability recovers on its own once a new complete-turn boundary is
  // crossed in the NEW Host numbering. RecordCoverage continues from where Y
  // already was — the next entry must advance the XTrace sequence, not restart.
  const reanchored = blog.applyReanchor(threeEntries())
  assert.equal(blog.coverage(reanchored).ingestedThroughSequence, 3)

  const rebuilt = commitEntry(reanchored, { from: 3, to: 4, cutoffFrom: 0, cutoffTo: 1, digest: 'new-d1' })
  assert.equal(rebuilt.ok, true, rebuilt.ok ? '' : rebuilt.error)
  assert.equal(blog.hasCoverage(rebuilt.value), true)

  // The pre-reanchor frames are still there, now joined by the new entry.
  assert.deepEqual(blog.frameKinds(rebuilt.value), ['Entry', 'Entry', 'Entry', 'Entry'])

  // And all four become coverable at once, including the three written under the
  // voided numbering. That is deliberate, not an oversight: those frames describe
  // work that really happened, and the cutoff is a claim about X's CURRENT prefix,
  // not about which turns B's text discusses. A FrozenRecordPrefix richer than the
  // cutoff is extra context; one poorer than it would be information loss.
  assert.deepEqual(blog.coverage(rebuilt.value), {
    ingestedThroughSequence: 4,
    cutoff: 1,
    digest: 'new-d1',
    coverableFrames: 4,
  })
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_reanchor_is_idempotent_on_the_frame_projection', () => {
  // The prefix projection makes a replay stale via its epoch check; here the
  // operation itself must be safe to apply twice, because the two projections
  // move under one fact.
  const once = blog.applyReanchor(threeEntries())
  const twice = blog.applyReanchor(once)

  assert.deepEqual(blog.coverage(twice), blog.coverage(once))
  assert.deepEqual(blog.frameKinds(twice), blog.frameKinds(once))
})

// ── COMPANION-006: a squash rewrites the first half of the frames permanently ─

test('WHAT[CONTEXT-COMPRESSION-014] COMPANION_006_squash_rewrites_first_half_of_frames_permanently', () => {
  let state = blog.empty
  for (let i = 1; i <= 4; i += 1) {
    const result = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i })
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    state = result.value
  }

  const squashed = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 2, frame: squashFrame(1) }, state).value
  assert.deepEqual(blog.frameKinds(squashed), ['Squash', 'Entry', 'Entry'])

  // The rewritten first half persists: a later entry does not restore the old frames.
  const next = commitEntry(squashed, { epoch: 1, from: 4, to: 5, cutoffFrom: 4, cutoffTo: 5, n: 5 }).value
  assert.deepEqual(blog.frameKinds(next), ['Squash', 'Entry', 'Entry', 'Entry'])
  assert.equal(blog.coverage(next).cutoff, 5)
})

// ── helper that builds a realistic state ───────────────────────────────────

/** Three entries, coverage at turn 3, no seed — COMPANION-003 has none. */
function threeEntries() {
  let state = blog.empty

  for (let i = 1; i <= 3; i += 1) {
    const result = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i })
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    state = result.value
  }

  return state
}

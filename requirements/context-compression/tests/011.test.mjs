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

test('WHAT[context-compression-011] CTX_012_squash_does_not_advance_coverage', () => {
  const squash = {
    previousEpoch: 0,
    nextEpoch: 1,
    count: 1,
    frame: blog.frame({
      kind: 'Squash',
      digest: 'sha-e1',
      ref: 'blob-s1',
      coveredFrom: 0,
      coveredThrough: 2,
    }),
  }

  let state = blog.empty
  state = foldOk([entryWithEnforcement({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 })]).Blog
  const squashResult = blog.applySquash(squash, state)
  assert.equal(squashResult.ok, true, squashResult.ok ? '' : squashResult.error)
  state = squashResult.value

  assert.equal(blog.coverage(state).ingestedThroughSequence, 1, 'coverage unchanged by squash')
  assert.equal(blog.coverage(state).cutoff, 1)
  assert.equal(blog.frameCount(state), 1, 'squash replaced frame, not appended')
  assert.deepEqual(blog.frameKinds(state), ['Squash'])
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

test('WHAT[context-compression-011] CTX_012_squash_replaces_the_oldest_frames_and_leaves_the_covered_range_alone', () => {
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
test('WHAT[context-compression-011] CTX_012_a_squash_that_consumes_the_whole_covered_range_leaves_one_coverable_frame', () => {
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
test('WHAT[context-compression-011] CTX_012_squash_width_is_ceil_half_and_does_not_skip_a_single_frame', () => {
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
test('WHAT[context-compression-011] CTX_012_single_frame_squash_may_replace_text_with_a_new_digest', () => {
  const state = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }).value
  const rewritten = blog.frame({
    kind: 'Squash',
    digest: 'sha-squash-rewritten',
    ref: 'blob-squash-rewritten',
    coveredFrom: 0,
    coveredThrough: 1,
  })

  const squashed = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 1, frame: rewritten }, state)
  assert.equal(squashed.ok, true, squashed.ok ? '' : squashed.error)
  assert.equal(blog.frames(squashed.value)[0].digest, 'sha-squash-rewritten')
})
test('WHAT[context-compression-011] CTX_012_squash_frames_are_interchangeable_with_entries_so_cascade_works', () => {
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
test('WHAT[context-compression-011] CTX_012_squash_count_outside_available_range_is_refused', () => {
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
test('WHAT[context-compression-011] PERSIST_010_squash_epoch_must_be_the_successor', () => {
  const state = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1 }).value

  for (const nextEpoch of [0, 2, 7]) {
    assert.deepEqual(
      blog.applySquash({ previousEpoch: 0, nextEpoch, count: 1, frame: squashFrame(1) }, state),
      { ok: false, error: 'NonSequentialFrameEpoch' },
      `nextEpoch ${nextEpoch} must be refused after epoch 0`,
    )
  }
})
test('WHAT[context-compression-011] PERSIST_010_squash_written_against_a_stale_epoch_is_refused', () => {
  const state = commitEntry(blog.empty, { from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1 }).value
  const once = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 1, frame: squashFrame(1) }, state).value

  // A replayed squash carries the epoch it expected, which the projection left.
  const replay = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 1, frame: squashFrame(1) }, once)
  assert.deepEqual(replay, { ok: false, error: 'StaleFrameEpoch' })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const toml = await import("../../../dist/Context/Companion/Blogger/TomlSurface.js");
const owner = await import("../../../dist/Context/Companion/ProjectionSurface.js");

const ident = owner
const prompt = owner
const proj = owner
const spy = (input) => `«${input}»`
const frames = (count) =>
  Array.from({ length: count }, (_, n) => ({ digest: `sha-f${n}`, body: `frame body ${n}` }))
const dataItems = [{ role: 'user', kind: 'text', text: 'work', truncated: false }]
const dataToml = '[[new_work_to_record]]\nuser = "work"\n'
const combinedDelta = prompt.newWork(dataItems)
const isHistoricFrame = (text) => text.startsWith('[[do_not_exec]]') && text.includes('historic_frame')
const isCombinedNormalDelta = (text) =>
  text.startsWith('# Write the dense work-log continuation now') && text.includes('[[new_work_to_record]]')
const isPreviousTip = (text) => text.includes('previous_enforcer_tip')

test('WHAT[context-compression-011] COMPANION_013_seal_root_is_derived_from_exactly_the_candidate_identity', () => {
  const seal = ident.sealRoot(spy, {
    session: 'ses_x',
    epoch: 3,
    cutoff: 7,
    prefixDigest: 'prefix-7',
    frozenDigest: 'frozen-7',
  })

  assert.equal(seal, '«ses_x|3|7|prefix-7|frozen-7»')
})
test('WHAT[context-compression-011] COMPANION_013_seal_root_changes_when_any_identity_field_changes', () => {
  const base = { session: 'ses_x', epoch: 1, cutoff: 4, prefixDigest: 'p', frozenDigest: 'f' }
  const seal = (over) => ident.sealRoot(spy, { ...base, ...over })

  const variants = [
    seal({}),
    seal({ session: 'ses_y' }),
    seal({ epoch: 2 }),
    seal({ cutoff: 5 }),
    seal({ prefixDigest: 'p2' }),
    seal({ frozenDigest: 'f2' }),
  ]

  assert.equal(new Set(variants).size, variants.length, 'every field must affect the seal')
})
test('WHAT[context-compression-011] COMPANION_013_seal_root_is_stable_across_calls', () => {
  const args = { session: 'ses_x', epoch: 2, cutoff: 9, prefixDigest: 'p', frozenDigest: 'f' }
  assert.equal(ident.sealRoot(spy, args), ident.sealRoot(spy, args))
})
test('WHAT[context-compression-011] COMPANION_013_companion_memory_id_is_a_function_of_the_seal_alone', () => {
  assert.equal(ident.companionMemoryMessageId(spy, 'SEAL'), '«SEAL|companion-memory»')
  assert.equal(
    ident.companionMemoryMessageId(spy, 'SEAL'),
    ident.companionMemoryMessageId(spy, 'SEAL'),
  )
  assert.notEqual(ident.companionMemoryMessageId(spy, 'SEAL'), ident.companionMemoryMessageId(spy, 'OTHER'))
})
test('WHAT[context-compression-011] COMPANION_013_frame_id_needs_both_the_ordinal_and_the_frame_epoch', () => {
  const id = (over) =>
    ident.frameMessageId(spy, { blogger: 'ses_y', epoch: 0, ordinal: 0, digest: 'sha-a', ...over })

  assert.notEqual(id({}), id({ ordinal: 1 }))
  assert.notEqual(id({}), id({ epoch: 1 }))
  assert.equal(id({}), '«ses_y|0|0|sha-a|blog-frame»')
})
test('WHAT[context-compression-011] COMPANION_013_instruction_id_distinguishes_normal_from_squash', () => {
  const normal = ident.instructionMessageId(spy, { blogger: 'ses_y', epoch: 0, kind: 'normal' })
  const squash = ident.instructionMessageId(spy, { blogger: 'ses_y', epoch: 0, kind: 'squash' })

  assert.notEqual(normal, squash)
  assert.equal(normal, '«ses_y|0|normal|instruction»')
})
test('WHAT[context-compression-011] COMPANION_013_frame_ids_are_positional_within_the_current_sequence', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 2,
    kind: proj.normal,
    frames: frames(2),
    delta: { messageId: 'msg_d', items: dataItems },
  })

  assert.deepEqual(plan.messages.slice(0, 2).map((m) => m.id), [
    '«ses_y|2|0|sha-f0|blog-frame»',
    '«ses_y|2|1|sha-f1|blog-frame»',
  ])
  // Normal no longer emits a synthetic instruction message id.
  assert.equal(plan.messages.at(-1).id, 'msg_d')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

const selection = prefix
const agreeing = (digest) => () => digest
const committedAt = (cutoff, { digest = `prefix-${cutoff}`, frozen = `frozen-${cutoff}`, seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: frozen,
    cutoff,
    prefixDigest: digest,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

test('WHAT[context-compression-011] CTX_012_the_probe_carries_the_seal_the_promotion_will_reuse', () => {
  // COMPANION-013: the seal is derived from the candidate's identity plus the epoch it
  // was built from. That is what lets CTX-012 promote the snapshot verbatim — the seal
  // the successful request used is already the one the committed epoch needs, so
  // promotion adds no second cold boundary.
  const result = selection.select({
    session: 'ses_x',
    committedEpoch: 3,
    committedSnapshot: undefined,
    coverableCutoff: 7,
    coveredDigest: 'p7',
    requestStartCutoff: 20,
    frozenDigest: 'f7',
    recomputeDigest: agreeing('p7'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)

  // The visible sha256 stand-in shows exactly which fields went in.
  assert.equal(result.sealRoot, '«ses_x|3|7|p7|f7»')

  // Both derived ids hash the ALREADY-HASHED seal, so the stand-in nests. That is the
  // real shape, not an artefact of the test double: COMPANION-013 derives them from the
  // seal rather than from its inputs, so two candidates that produce the same seal
  // necessarily produce the same message id — which is what keeps the provider from
  // seeing a changed prefix.
  assert.equal(result.syntheticId, '««ses_x|3|7|p7|f7»|companion-memory»')
  assert.equal(result.probeId, '««ses_x|3|7|p7|f7»|probe»')

  // And they are distinct from each other: one addresses a message, the other
  // identifies the attempt, and CTX-012's fold matches on the ProbeId alone.
  assert.notEqual(result.syntheticId, result.probeId)
})
test('WHAT[context-compression-011] CTX_012_the_built_candidate_is_exactly_what_the_projection_will_promote', () => {
  // End to end across the two modules: the snapshot the selector produced is accepted
  // by the fold unchanged. If either side reconstructed a field, this would be where
  // the drift showed.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 5,
    coveredDigest: 'p5',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('p5'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)

  const promoted = prefix.applyRebase({ previousEpoch: 0, nextEpoch: 1, candidate: result.candidate }, prefix.empty)

  assert.equal(promoted.ok, true, promoted.ok ? '' : promoted.error)
  assert.deepEqual(promoted.value.snapshot, result.candidate, 'promoted byte-for-byte, not rebuilt')
  assert.equal(promoted.value.snapshot.sealRoot, result.sealRoot)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const XWireSurface = await import("../../../dist/Context/Prefix/XWireSurface.js");

test('WHAT[context-compression-011] XWIRE_tool_call_provider_success_promotes_and_clears_before_host_turn_finishes', () => {
  const result = XWireSurface.reconcile({
    hasPlan: true,
    outcome: 'tool-calls',
    hasProbe: true,
    currentEpoch: 0,
    probeEpoch: 0,
  })
  assert.equal(result.promoted, true)
  assert.equal(result.cleared, true)
  assert.equal(result.keptPlan, false)
})
}

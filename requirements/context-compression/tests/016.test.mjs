import test from 'node:test'

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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");
const delta = await import("../../../dist/Context/Companion/Blogger/DeltaSurface.js");
const prompt = await import("../../../dist/Context/Companion/ProjectionSurface.js");
const toml = await import("../../../dist/Context/Companion/Blogger/TomlSurface.js");

const syn = { byteCount: delta.byteCount }
const textItem = (text, role = 'user') => ({
  Role: role,
  Part: { Kind: 'text', Text: text, Tool: '', Args: '', MediaType: '' },
  Truncated: false,
})
const origin = delta.cursor(0, 0)
const drainAll = (limit, messages, guard = 50) => {
  const chunks = []
  let cursor = origin
  let cutoff = 0

  for (let i = 0; i < guard; i += 1) {
    const chunk = delta.nextChunk({ limit, cursor, previousCutoff: cutoff, messages })
    if (chunk === undefined) return chunks

    chunks.push(chunk)
    cursor = delta.cursor(chunk.nextCursor.turn, chunk.nextCursor.part)
    cutoff = chunk.nextCutoff
  }

  assert.fail(`chunking did not terminate within ${guard} chunks — a cursor is not advancing`)
}

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_a_fully_consumed_transcript_yields_no_chunk', () => {
  const messages = delta.messages([{ role: 'user', parts: [delta.text('one')] }])

  assert.equal(delta.nextChunk({ limit: 1024, cursor: delta.cursor(1, 0), messages }), undefined)
  assert.equal(delta.nextChunk({ limit: 1024, cursor: origin, messages: delta.messages([]) }), undefined)
})
test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_the_cursor_resumes_exactly_where_the_previous_chunk_stopped', () => {
  // Each turn here renders to roughly 1 KiB, so a small limit forces several
  // chunks. What matters is that draining loses nothing and repeats nothing.
  const body = 'x'.repeat(900)
  const messages = delta.messages(
    [0, 1, 2, 3].map((n) => ({ role: 'user', parts: [delta.text(`${n}:${body}`)] })),
  )

  const chunks = drainAll(1200, messages)

  assert.equal(chunks.length, 4, 'one turn per chunk at this limit')
  assert.deepEqual(
    chunks.map((c) => c.nextCursor),
    [
      { turn: 1, part: 0 },
      { turn: 2, part: 0 },
      { turn: 3, part: 0 },
      { turn: 4, part: 0 },
    ],
  )
  assert.deepEqual(chunks.map((c) => c.nextCutoff), [1, 2, 3, 4])

  // Every turn appears exactly once across all chunks.
  const seen = chunks.flatMap((c) => c.toml.match(/^(\d):/gm) ?? [])
  assert.deepEqual(seen, [])
  for (const n of [0, 1, 2, 3]) {
    const occurrences = chunks.filter((c) => c.toml.includes(`${n}:${body}`)).length
    assert.equal(occurrences, 1, `turn ${n} must appear in exactly one chunk`)
  }
})
test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_a_multi_part_turn_splits_at_part_boundaries_and_holds_the_cutoff', () => {
  const body = 'y'.repeat(900)
  const messages = delta.messages([
    {
      role: 'assistant',
      parts: [delta.text(`p0:${body}`), delta.text(`p1:${body}`), delta.text(`p2:${body}`)],
    },
    { role: 'user', parts: [delta.text('after')] },
  ])

  const chunks = drainAll(1200, messages)

  // The first two chunks stop inside turn 0: the ingest cursor moves by part, and
  // the cutoff stays at 0 because the turn is not finished.
  assert.deepEqual(chunks[0].nextCursor, { turn: 0, part: 1 })
  assert.equal(chunks[0].nextCutoff, 0, 'a half-consumed turn is not coverage')
  assert.deepEqual(chunks[1].nextCursor, { turn: 0, part: 2 })
  assert.equal(chunks[1].nextCutoff, 0)

  // The third chunk consumes turn 0's last part AND all of turn 1, because both
  // still fit — level one packs across message boundaries whenever it can. So the
  // cutoff jumps straight to 2. What the rule guarantees is that the cutoff never
  // moves past a turn whose last part is still unconsumed, not that it advances
  // one turn per chunk.
  assert.equal(chunks.length, 3)
  assert.deepEqual(chunks[2].nextCursor, { turn: 2, part: 0 })
  assert.equal(chunks[2].nextCutoff, 2)
})
test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_a_chunk_ending_on_a_non_final_part_never_advances_the_cutoff', () => {
  // The rule in isolation, with no following turn to pack in: three oversized parts
  // in one turn, so every chunk but the last stops mid-turn.
  const body = 'w'.repeat(1000)
  const messages = delta.messages([
    {
      role: 'assistant',
      parts: [delta.text(`p0:${body}`), delta.text(`p1:${body}`), delta.text(`p2:${body}`)],
    },
  ])

  const chunks = drainAll(1150, messages)

  assert.equal(chunks.length, 3)
  assert.deepEqual(chunks.map((c) => c.nextCutoff), [0, 0, 1], 'only the final part advances the cutoff')
  assert.deepEqual(
    chunks.map((c) => c.nextCursor),
    [
      { turn: 0, part: 1 },
      { turn: 0, part: 2 },
      { turn: 1, part: 0 },
    ],
  )
})
test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_the_cutoff_never_decreases_across_chunks', () => {
  const body = 'z'.repeat(700)
  const messages = delta.messages([
    { role: 'user', parts: [delta.text('short')] },
    { role: 'assistant', parts: [delta.text(`a:${body}`), delta.text(`b:${body}`)] },
    { role: 'user', parts: [delta.text('short again')] },
  ])

  let previous = 0
  for (const chunk of drainAll(1000, messages)) {
    assert.equal(chunk.nextCutoff >= previous, true, `cutoff went ${previous} → ${chunk.nextCutoff}`)
    previous = chunk.nextCutoff
  }
  assert.equal(previous, 3, 'everything is eventually covered')
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

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_coverage_inside_the_live_tail_means_no_candidate', () => {
  // `requestStartCutoff = 0` is the first request of a session: there are no turns
  // before the message being answered, so any candidate would have to swallow it.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 5,
    coveredDigest: 'd5',
    requestStartCutoff: 0,
    recomputeDigest: agreeing('d5'),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'CoverageNotAheadOfRequest')
})
test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_the_candidate_never_swallows_the_message_being_answered', () => {
  // Companion is ahead of the request boundary — it consumed turns this request has
  // not sent yet. The candidate must clamp to the request's own start, or the probe
  // would replace the user message the model is supposed to answer.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 9,
    coveredDigest: 'd-clamped',
    requestStartCutoff: 4,
    recomputeDigest: agreeing('d-clamped'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.equal(result.cutoff, 4, 'clamped to the request start, not the Companion coverage')
})
test('WHAT[CONTEXT-COMPRESSION-016] COMPANION_011_the_proof_hashes_exactly_the_clamped_cutoff', () => {
  // Step 1 clamps before step 5 hashes. Hashing the Companion's unclamped cutoff would
  // prove a prefix the candidate does not actually use — the check would pass while
  // describing a different range.
  const asked = []

  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 9,
    coveredDigest: 'd-at-4',
    requestStartCutoff: 4,
    recomputeDigest: (cutoff) => {
      asked.push(cutoff)
      return cutoff === 4 ? 'd-at-4' : 'wrong-range'
    },
  })

  assert.deepEqual(asked, [4], 'the proof must hash the clamped cutoff exactly once')
  assert.equal(result.ok, true, result.ok ? '' : result.message)
})

test('WHAT[CONTEXT-COMPRESSION-016] journal_to_blob_materialization_and_probe_pipeline', async () => {
  const xwire = await import('../../../dist/Context/Prefix/XWireSurface.js')
  const { createHash } = await import('node:crypto')
  const sha256Hex = (text) => createHash('sha256').update(text, 'utf8').digest('hex')

  const blobStore = new Map()
  const frame1 = 'Completed discovery on module Alpha\nEstablished baseline.'
  const frame2 = 'Implemented fix in module Beta\nAdded test case.'
  const ref1 = 'blobs/frame-1'
  const ref2 = 'blobs/frame-2'
  const digest1 = sha256Hex(frame1)
  const digest2 = sha256Hex(frame2)
  blobStore.set(ref1, frame1)
  blobStore.set(ref2, frame2)

  const readRefs = []
  const writtenBlobs = new Map()
  const port = {
    readBlob: async (ref) => {
      readRefs.push(ref)
      if (!blobStore.has(ref)) return { ok: false, error: `missing blob ${ref}` }
      return { ok: true, value: blobStore.get(ref) }
    },
    writeBlob: async (content) => {
      const digest = sha256Hex(content)
      const ref = 'blobs/frozen-prefix-1'
      writtenBlobs.set(ref, { content, digest })
      return { ok: true, value: { blobRef: ref, blobDigest: digest } }
    },
  }

  const frames = [
    { kind: 'Entry', ref: ref1, digest: digest1, coveredFrom: 0, coveredThrough: 1 },
    { kind: 'Entry', ref: ref2, digest: digest2, coveredFrom: 1, coveredThrough: 2 },
  ]
  const opening = {
    assignmentText: '# Common Law\nOriginal user assignment charter',
    modelName: 'test-model',
    timestampIso: '2026-09-17T00:00:00Z',
  }

  const res = await xwire.candidateFromJournal({
    port,
    sessionId: 'test-compress-ses',
    opening,
    frames,
    coverableCutoff: 2,
    coveredDigest: '26431b78769f0ddc2d42da5c6f920285fca27f5755353eda546a25aebefb9072',
    requestCutoff: 2,
  })

  assert.equal(res.ok, true, res.error)
  assert.equal(readRefs.length, 2, 'ReadBlob must read both frame blobs')
  assert.ok(readRefs.includes(ref1) && readRefs.includes(ref2), 'ReadBlob must read both frame blobs')
  assert.equal(writtenBlobs.size, 1, 'Exactly one frozen record prefix blob must be written')

  const written = writtenBlobs.get('blobs/frozen-prefix-1')
  assert.ok(written.content.includes(frame1), 'materialized text must contain frame 1 body')
  assert.ok(written.content.includes(frame2), 'materialized text must contain frame 2 body')
  assert.ok(
    !written.content.includes('Original user assignment charter'),
    'same-session frozen prefix must NOT contain Opening charter text'
  )
  assert.equal(written.digest, sha256Hex(written.content), 'BlobDigest must equal sha256 of materialized text')

  assert.ok(res.probe, 'PrefixProbe must be returned')
  assert.equal(res.probe.candidate.ref, 'blobs/frozen-prefix-1', 'PrefixProbe must carry written blob ref')
  assert.equal(res.probe.candidate.frozenDigest, written.digest, 'PrefixProbe must carry written blob digest')
  assert.equal(res.probe.candidate.cutoff, 2, 'PrefixProbe must carry correct cutoff')
})

test('WHAT[CONTEXT-COMPRESSION-016] journal_materialization_fails_closed_on_corrupted_blob_ref', async () => {
  const xwire = await import('../../../dist/Context/Prefix/XWireSurface.js')

  const writtenBlobs = new Map()
  const port = {
    readBlob: async (ref) => {
      return { ok: false, error: `corrupted blob reference: ${ref}` }
    },
    writeBlob: async (content) => {
      writtenBlobs.set('garbage', content)
      return { ok: true, value: { blobRef: 'garbage-ref', blobDigest: 'garbage-digest' } }
    },
  }

  const frames = [
    { kind: 'Entry', ref: 'blobs/corrupted-frame', digest: 'bad-digest', coveredFrom: 0, coveredThrough: 1 },
  ]

  const res = await xwire.candidateFromJournal({
    port,
    sessionId: 'test-corrupt-ses',
    frames,
    coverableCutoff: 1,
    coveredDigest: 'cov-bad',
    requestCutoff: 1,
  })

  assert.equal(res.ok, false, 'must fail-closed on corrupt blob read')
  assert.match(res.error, /corrupt/i, 'error reason must propagate')
  assert.equal(writtenBlobs.size, 0, 'zero garbage writes must occur when frame read fails')
})
}

// tests/unit/Context/blogger-delta.test.mjs — CTX-003 / CTX-011 / CTX-013 chunker.
//
// Three levels, in order: whole messages, then part boundaries, then a hard cut.
// The first is the common case; the second engages only when one message alone
// exceeds the limit; the third only when one part does.
//
// Two rules here are load-bearing and silent if broken:
//
//   The cutoff advances ONLY across a turn consumed to its last part
//   (COMPANION-011). A chunk stopping mid-turn moves the ingest cursor and nothing
//   else, so a probe can never be built from half a turn.
//
//   A hard truncation DISCARDS the tail (CTX-013). Carrying it forward would make a
//   part that is always over the limit resend forever.

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { bloggerDelta as delta, bloggerToml as toml, companionPrompt as prompt, syntheticToml as syn } from '../../../tests/unit/support/domain.mjs'

const origin = delta.cursor(0, 0)

/** Drain the whole message list, returning every chunk in order. */
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

// ── the contract constant ──────────────────────────────────────────────────

test('CTX_003_delta_limit_is_200_KiB', () => {
  // An input contract, not an estimate: never compared to a model window, never
  // scaled by provider. Exported as a plain value so this test can read it.
  assert.equal(delta.limitBytes, 200 * 1024)
})

// ── nothing to consume ─────────────────────────────────────────────────────

test('CTX_011_a_fully_consumed_transcript_yields_no_chunk', () => {
  const messages = delta.messages([{ role: 'user', parts: [delta.text('one')] }])

  assert.equal(delta.nextChunk({ limit: 1024, cursor: delta.cursor(1, 0), messages }), undefined)
  assert.equal(delta.nextChunk({ limit: 1024, cursor: origin, messages: delta.messages([]) }), undefined)
})

// ── level one: whole messages ──────────────────────────────────────────────

test('CTX_013_a_small_transcript_becomes_one_chunk', () => {
  const messages = delta.messages([
    { role: 'user', parts: [delta.text('请修复 fallback 的竞态。')] },
    { role: 'assistant', parts: [delta.toolCall('edit', '{"a":1}')] },
    { role: 'tool', parts: [delta.toolResult('applied')] },
  ])

  const chunks = drainAll(64 * 1024, messages)

  assert.equal(chunks.length, 1)
  assert.deepEqual(chunks[0].kinds, ['TextPart', 'ToolCallPart', 'ToolResultPart'])
  assert.deepEqual(chunks[0].nextCursor, { turn: 3, part: 0 })
  assert.equal(chunks[0].nextCutoff, 3, 'all three turns are complete')
})

test('CTX_011_the_cursor_resumes_exactly_where_the_previous_chunk_stopped', () => {
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

test('CTX_003_no_chunk_exceeds_the_limit', () => {
  const messages = delta.messages(
    [0, 1, 2, 3, 4, 5].map((n) => ({
      role: n % 2 === 0 ? 'user' : 'assistant',
      parts: [delta.text(`turn ${n}: ` + '中'.repeat(200))],
    })),
  )

  // CJK: 200 characters is 600 bytes, so a character-based limit would let each
  // chunk run three times over.
  const limit = 1500
  for (const chunk of drainAll(limit, messages)) {
    assert.equal(chunk.bytes <= limit, true, `chunk of ${chunk.bytes} bytes exceeds ${limit}`)
    assert.equal(chunk.bytes, syn.byteCount(chunk.toml), 'reported bytes must be the rendered bytes')
  }
})

test('CTX_013_normal_chunk_is_data_only_and_counts_no_instruction_header', () => {
  // COMPANION-004: normal deltas are data-only. The behaviour rules live in the
  // system prompt alone, so a normal chunk carries no instruction header and pays
  // nothing for one.
  const body = 'observed work ' + 'x'.repeat(500)
  const messages = delta.messages([{ role: 'user', parts: [delta.text(body)] }])
  const item = toml.item({ role: 'user', part: toml.text(body) })
  const dataOnlyBytes = syn.byteCount(toml.render([item]))
  const limit = dataOnlyBytes + 100

  const chunk = delta.nextChunk({ limit, cursor: origin, messages })
  assert.equal(chunk.bytes <= limit, true, 'the final sent bytes must fit the limit')
  assert.equal(chunk.bytes, syn.byteCount(chunk.toml))
  assert.equal(chunk.toml.startsWith('# '), false, 'a normal delta carries no instruction header')
  assert.equal(chunk.toml.includes('\n\n'), false, 'data body has no decorative blank lines')

  const parsed = parseToml(chunk.toml)
  assert.equal(parsed.new_work_to_record.length, 1)
  assert.equal(typeof parsed.new_work_to_record[0].user, 'string')
  assert.equal(parsed.new_work_to_record[0].truncated, undefined, 'the whole part fits, so no truncation flag')
  assert.equal('messages' in parsed, false, 'the payload is Blogger TOML data, not a JSON envelope')
})

// ── level two: part boundaries within one message ──────────────────────────

test('CTX_011_a_multi_part_turn_splits_at_part_boundaries_and_holds_the_cutoff', () => {
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

test('CTX_011_a_chunk_ending_on_a_non_final_part_never_advances_the_cutoff', () => {
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

test('CTX_011_the_cutoff_never_decreases_across_chunks', () => {
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

// ── level three: hard truncation ───────────────────────────────────────────

test('CTX_013_a_single_oversized_part_is_hard_truncated_and_marked', () => {
  const huge = 'q'.repeat(20000)
  const messages = delta.messages([{ role: 'user', parts: [delta.text(huge)] }])

  const limit = 2000
  const chunk = delta.nextChunk({ limit, cursor: origin, messages })

  assert.equal(chunk.itemCount, 1)
  assert.deepEqual(chunk.truncatedFlags, [true], 'the item must declare it was cut')
  assert.equal(chunk.bytes <= limit, true, `truncated chunk of ${chunk.bytes} bytes exceeds ${limit}`)
  assert.equal(chunk.toml.includes(toml.truncationMarker), true, 'the fixed marker must be present')
})

test('CTX_013_truncation_discards_the_tail_rather_than_resending_it', () => {
  // The rule that prevents an infinite loop: an always-oversized part must be
  // passed over entirely, not carried into the next chunk.
  const huge = 'q'.repeat(20000)
  const messages = delta.messages([
    { role: 'user', parts: [delta.text(huge)] },
    { role: 'assistant', parts: [delta.text('next turn')] },
  ])

  const chunks = drainAll(2000, messages)

  assert.equal(chunks.length, 2, 'the oversized part is consumed in one pass')
  assert.deepEqual(chunks[0].nextCursor, { turn: 1, part: 0 }, 'the cursor passes the WHOLE original part')
  assert.equal(chunks[0].nextCutoff, 1, 'the turn is finished, even though its content was cut')

  assert.deepEqual(chunks[1].truncatedFlags, [false])
  assert.equal(chunks[1].toml.includes('next turn'), true)
})

test('CTX_013_truncated_output_is_still_valid_TOML_and_ends_at_a_character_boundary', () => {
  // Cutting rendered UTF-8 bytes directly would split a multi-byte sequence. The
  // marker is appended after the cut, so the document must still parse.
  const cjk = '中'.repeat(8000)
  const messages = delta.messages([{ role: 'user', parts: [delta.text(cjk)] }])

  const limit = 3000
  const chunk = delta.nextChunk({ limit, cursor: origin, messages })

  assert.equal(chunk.bytes <= limit, true)
  assert.equal(chunk.toml.includes('\uFFFD'), false, 'no replacement character from a split sequence')
  assert.equal(chunk.toml.includes(toml.truncationMarker), true)

  // The claim in this test's name, actually exercised. Byte-level checks cannot see a
  // document that is malformed, and the truncation path is where malformation is most
  // likely: the cut lands at an arbitrary offset inside a `'''` body and the marker plus
  // the closing delimiter are appended after it. ARCH-010 put that delimiter on its own
  // line, which moved the arithmetic by one byte — precisely the kind of change a
  // `bytes <= limit` assertion passes through silently.
  const parsed = parseToml(chunk.toml)
  assert.equal(parsed.new_work_to_record.length, 1)
  assert.equal(parsed.new_work_to_record[0].truncated, true)
  assert.equal(
    parsed.new_work_to_record[0].user.includes(toml.truncationMarker),
    true,
    'the marker survives parsing as data',
  )

  // Every retained CJK character is whole: the count of them is an integer number
  // of 3-byte sequences, which a byte-level cut could not guarantee.
  const retained = (chunk.toml.match(/中/g) ?? []).length
  assert.equal(retained > 0, true, 'some content survived')
  assert.equal(syn.byteCount('中'.repeat(retained)), retained * 3)
})

test('CTX_013_hard_truncation_of_an_escaped_multiline_body_still_fits', () => {
  // A body containing both a newline and `'''` has no legal multi-line form, so
  // renderString falls back to a basic string. That expansion is the non-linearity
  // the search must measure; a character/byte ratio would undershoot the budget.
  const huge = ("keep ''' inside\n").repeat(3000)
  const messages = delta.messages([{ role: 'user', parts: [delta.text(huge)] }])

  const limit = 4000
  const chunk = delta.nextChunk({ limit, cursor: origin, messages })

  assert.equal(chunk.bytes <= limit, true, `truncated chunk of ${chunk.bytes} bytes exceeds ${limit}`)
  assert.equal(syn.byteCount(chunk.toml), chunk.bytes)
  assert.equal(chunk.toml.includes(toml.truncationMarker), true)

  const parsed = parseToml(chunk.toml)
  assert.equal(parsed.new_work_to_record.length, 1)
  assert.equal(parsed.new_work_to_record[0].truncated, true)
})

test('CTX_013_an_omission_marker_is_never_truncated', () => {
  // It has no body to cut. A limit it cannot meet means the limit is below the
  // fixed item scaffolding — a configuration error, not something to repair by
  // emitting an invalid item.
  const messages = delta.messages([{ role: 'user', parts: [delta.media('image/png', 'sha-img')] }])

  const chunk = delta.nextChunk({ limit: 10, cursor: origin, messages })

  assert.deepEqual(chunk.kinds, ['ImageOmitted'])
  assert.deepEqual(chunk.truncatedFlags, [false])
  assert.deepEqual(chunk.nextCursor, { turn: 1, part: 0 }, 'the cursor still advances past it')
})

// ── media: the Companion has no vision ─────────────────────────────────────

test('CTX_013_images_become_markers_carrying_no_content', () => {
  const messages = delta.messages([
    {
      role: 'user',
      parts: [delta.text('look at this'), delta.media('image/png', 'sha256-of-the-image')],
    },
  ])

  const chunk = delta.nextChunk({ limit: 8192, cursor: origin, messages })

  assert.deepEqual(chunk.kinds, ['TextPart', 'ImageOmitted'])
  assert.equal(chunk.toml.includes('[[new_work_to_record]]'), true)
  assert.equal(chunk.toml.includes('media_omitted = "image/png"'), true)

  // The digest exists in the semantic projection for CTX-011's cutoff proof. It
  // must NOT cross into the delta: there it would be a fact about the image the
  // Companion could carry into B.
  assert.equal(chunk.toml.includes('sha256-of-the-image'), false)
  assert.doesNotMatch(chunk.toml, /base64|data:|contentDigest/)
})

test('CTX_013_non_image_media_uses_the_media_marker', () => {
  const messages = delta.messages([
    { role: 'user', parts: [delta.media('application/pdf', 'sha-pdf')] },
    { role: 'user', parts: [delta.media(undefined, 'sha-unknown')] },
  ])

  const chunk = delta.nextChunk({ limit: 8192, cursor: origin, messages })

  assert.deepEqual(chunk.kinds, ['MediaOmitted', 'MediaOmitted'])
  assert.equal(chunk.toml.includes('media_omitted = "application/pdf"'), true)
  assert.equal(chunk.toml.includes('media_omitted = "untyped"'), true)
})

test('CTX_013_an_image_only_turn_is_consumed_and_advances_coverage', () => {
  // The turn is real and must not stall the cursor just because its content was
  // omitted — otherwise a screenshot would freeze the Companion permanently.
  const messages = delta.messages([
    { role: 'user', parts: [delta.media('image/png', 'sha-only')] },
    { role: 'assistant', parts: [delta.text('I see a screenshot.')] },
  ])

  const chunks = drainAll(8192, messages)

  assert.equal(chunks.length, 1)
  assert.deepEqual(chunks[0].kinds, ['ImageOmitted', 'TextPart'])
  assert.equal(chunks[0].nextCutoff, 2, 'the image-only turn counts as covered')
})

// ── determinism ────────────────────────────────────────────────────────────

test('CTX_013_the_same_input_produces_the_same_chunks', () => {
  const build = () =>
    delta.messages([
      { role: 'user', parts: [delta.text('修复竞态'), delta.media('image/png', 'sha-a')] },
      { role: 'assistant', parts: [delta.toolCall('edit', '{"b":2,"a":1}')] },
      { role: 'tool', parts: [delta.toolResult('ok')] },
    ])

  const first = drainAll(4096, build()).map((c) => c.toml)
  const second = drainAll(4096, build()).map((c) => c.toml)

  assert.deepEqual(first, second)
})

test('CTX_013_canonical_args_pass_through_without_re_sorting', () => {
  // `args` is already canonical: it is the value the Host codec put into the wire
  // projection. Re-sorting here would be a second canonicaliser that could
  // disagree with the one the seal digest used.
  //
  // The assertion is on the ESCAPED form, because a single-line body renders as a
  // basic string. What it proves is key ORDER: `zebra` still precedes `alpha`.
  const messages = delta.messages([
    { role: 'assistant', parts: [delta.toolCall('edit', '{"zebra":1,"alpha":2}')] },
  ])

  const chunk = delta.nextChunk({ limit: 4096, cursor: origin, messages })

  assert.equal(chunk.toml.includes('arguments = "{\\"zebra\\":1,\\"alpha\\":2}"'), true, 'order preserved as supplied')

  // A multi-line body takes the literal form, where the bytes appear verbatim —
  // the same guarantee without the escaping.
  const multiline = delta.messages([
    { role: 'assistant', parts: [delta.toolCall('edit', '{\n  "zebra": 1,\n  "alpha": 2\n}')] },
  ])

  const literal = delta.nextChunk({ limit: 4096, cursor: origin, messages: multiline })
  assert.equal(literal.toml.includes('"zebra": 1'), true)
  assert.equal(literal.toml.indexOf('zebra') < literal.toml.indexOf('alpha'), true)
})

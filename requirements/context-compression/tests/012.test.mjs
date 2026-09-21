import test from 'node:test'

// Provider language is adopted at import time; these fixtures assert English
// prose, so pin the preference before any production surface loads.
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

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

test('WHAT[context-compression-012] CTX_013_a_small_transcript_becomes_one_chunk', () => {
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
test('WHAT[context-compression-012] CTX_013_normal_chunk_is_data_only_and_counts_no_instruction_header', () => {
  // COMPANION-004: normal deltas are data-only. The behaviour rules live in the
  // system prompt alone, so a normal chunk carries no instruction header and pays
  // nothing for one.
  const body = 'observed work ' + 'x'.repeat(500)
  const messages = delta.messages([{ role: 'user', parts: [delta.text(body)] }])
  const item = textItem(body)
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
test('WHAT[context-compression-012] CTX_013_a_single_oversized_part_is_hard_truncated_and_marked', () => {
  const huge = 'q'.repeat(20000)
  const messages = delta.messages([{ role: 'user', parts: [delta.text(huge)] }])

  const limit = 2000
  const chunk = delta.nextChunk({ limit, cursor: origin, messages })

  assert.equal(chunk.itemCount, 1)
  assert.deepEqual(chunk.truncatedFlags, [true], 'the item must declare it was cut')
  assert.equal(chunk.bytes <= limit, true, `truncated chunk of ${chunk.bytes} bytes exceeds ${limit}`)
  assert.equal(chunk.toml.includes(toml.TruncationMarker), true, 'the fixed marker must be present')
})
test('WHAT[context-compression-012] CTX_013_truncation_discards_the_tail_rather_than_resending_it', () => {
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
test('WHAT[context-compression-012] CTX_013_truncated_output_is_still_valid_TOML_and_ends_at_a_character_boundary', () => {
  // Cutting rendered UTF-8 bytes directly would split a multi-byte sequence. The
  // marker is appended after the cut, so the document must still parse.
  const cjk = '中'.repeat(8000)
  const messages = delta.messages([{ role: 'user', parts: [delta.text(cjk)] }])

  const limit = 3000
  const chunk = delta.nextChunk({ limit, cursor: origin, messages })

  assert.equal(chunk.bytes <= limit, true)
  assert.equal(chunk.toml.includes('\uFFFD'), false, 'no replacement character from a split sequence')
  assert.equal(chunk.toml.includes(toml.TruncationMarker), true)

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
    parsed.new_work_to_record[0].user.includes(toml.TruncationMarker),
    true,
    'the marker survives parsing as data',
  )

  // Every retained CJK character is whole: the count of them is an integer number
  // of 3-byte sequences, which a byte-level cut could not guarantee.
  const retained = (chunk.toml.match(/中/g) ?? []).length
  assert.equal(retained > 0, true, 'some content survived')
  assert.equal(syn.byteCount('中'.repeat(retained)), retained * 3)
})
test('WHAT[context-compression-012] CTX_013_hard_truncation_of_an_escaped_multiline_body_still_fits', () => {
  // A body containing both a newline and `'''` has no legal multi-line form, so
  // renderString falls back to a basic string. That expansion is the non-linearity
  // the search must measure; a character/byte ratio would undershoot the budget.
  const huge = ("keep ''' inside\n").repeat(3000)
  const messages = delta.messages([{ role: 'user', parts: [delta.text(huge)] }])

  const limit = 4000
  const chunk = delta.nextChunk({ limit, cursor: origin, messages })

  assert.equal(chunk.bytes <= limit, true, `truncated chunk of ${chunk.bytes} bytes exceeds ${limit}`)
  assert.equal(syn.byteCount(chunk.toml), chunk.bytes)
  assert.equal(chunk.toml.includes(toml.TruncationMarker), true)

  const parsed = parseToml(chunk.toml)
  assert.equal(parsed.new_work_to_record.length, 1)
  assert.equal(parsed.new_work_to_record[0].truncated, true)
})
test('WHAT[context-compression-012] CTX_013_an_omission_marker_is_never_truncated', () => {
  // It has no body to cut. A limit it cannot meet means the limit is below the
  // fixed item scaffolding — a configuration error, not something to repair by
  // emitting an invalid item.
  const messages = delta.messages([{ role: 'user', parts: [delta.media('image/png', 'sha-img')] }])

  const chunk = delta.nextChunk({ limit: 10, cursor: origin, messages })

  assert.deepEqual(chunk.kinds, ['ImageOmitted'])
  assert.deepEqual(chunk.truncatedFlags, [false])
  assert.deepEqual(chunk.nextCursor, { turn: 1, part: 0 }, 'the cursor still advances past it')
})
test('WHAT[context-compression-012] CTX_013_images_become_markers_carrying_no_content', () => {
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
test('WHAT[context-compression-012] CTX_013_non_image_media_uses_the_media_marker', () => {
  const messages = delta.messages([
    { role: 'user', parts: [delta.media('application/pdf', 'sha-pdf')] },
    { role: 'user', parts: [delta.media(undefined, 'sha-unknown')] },
  ])

  const chunk = delta.nextChunk({ limit: 8192, cursor: origin, messages })

  assert.deepEqual(chunk.kinds, ['MediaOmitted', 'MediaOmitted'])
  assert.equal(chunk.toml.includes('media_omitted = "application/pdf"'), true)
  assert.equal(chunk.toml.includes('media_omitted = "untyped"'), true)
})
test('WHAT[context-compression-012] CTX_013_an_image_only_turn_is_consumed_and_advances_coverage', () => {
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
test('WHAT[context-compression-012] CTX_013_the_same_input_produces_the_same_chunks', () => {
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
test('WHAT[context-compression-012] CTX_013_canonical_args_pass_through_without_re_sorting', () => {
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

test('WHAT[context-compression-012] COMPANION_004_request_instructions_require_exactly_one_blog_call', () => {
  assert.match(prompt.normalInstruction, /# Write the dense work-log continuation now/)
  assert.match(prompt.normalInstruction, /exactly once/)
  assert.match(prompt.squashInstruction, /# Rewrite the preceding assistant work-log frames now/)
  assert.match(prompt.squashInstruction, /exactly once/)
  assert.equal(prompt.system, undefined, 'System is owned by PromptResources Blogger Role Law, not CompanionPrompt')
})
test('WHAT[context-compression-012] ENFORCER_030_squash_and_normal_require_tip_not_omit_scores', () => {
  assert.match(prompt.squashInstruction, /required tip|catalog field/)
  assert.match(prompt.squashInstruction, /do not output ordinary assistant prose/i)
  assert.doesNotMatch(prompt.squashInstruction, /omit all scores/)
  assert.match(prompt.normalInstruction, /required tip|catalog field/)
  assert.doesNotMatch(prompt.normalInstruction, /omit.*scores/i)
})
test('WHAT[context-compression-012] COMPANION_010_memory_block_is_one_instruction_plane', () => {
  const block = prompt.memoryBlock('B CONTENT')

  assert.match(block, /prior responsibility/)
  assert.match(block, /^# .*prior responsibility/m)
  assert.match(block, /^# B CONTENT$/m)
  assert.doesNotMatch(block, /<work-log>|not a new user instruction/)
})
test('WHAT[context-compression-012] COMPANION_005_message_wrappers_are_toml_not_markdown_titles', () => {
  assert.equal(prompt.workingRecord('frame body 0'), toml.renderHistoricFrame('frame body 0'))
  assert.equal(prompt.workingRecord('frame body 0').includes('[[do_not_exec]]'), true)
  assert.equal(prompt.workingRecord('frame body 0').includes('historic_frame'), true)
  assert.equal(prompt.workingRecord('frame body 0').includes('# Working Record'), false)
  assert.equal(prompt.newWork(dataItems).includes('# New Work To Record'), false)
})
test('WHAT[context-compression-012] COMPANION_005_new_work_is_instruction_header_then_data_body', () => {
  const rendered = prompt.newWork(dataItems)
  assert.equal(rendered.startsWith('# Write the dense work-log continuation now'), true)
  assert.equal(rendered.includes('\n\n[[new_work_to_record]]'), true)
  assert.equal(rendered.endsWith(dataToml + '\n') || rendered.endsWith(dataToml), true)
  // Data body has no extra instruction after tables.
  const dataStart = rendered.indexOf('[[new_work_to_record]]')
  assert.equal(rendered.slice(dataStart).includes('# Write'), false)
})
test('WHAT[context-compression-012] COMPANION_005_normal_with_frames_is_assistant_do_not_exec_then_combined_delta', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(3),
    delta: { messageId: 'msg_delta', items: dataItems },
  })

  assert.equal(plan.system, undefined, 'projection plan no longer carries System')
  assert.equal(plan.texts.filter(isHistoricFrame).length, 3)
  assert.equal(plan.texts.filter(isCombinedNormalDelta).length, 1)
  assert.equal(plan.texts.at(-1), combinedDelta)
  assert.deepEqual(plan.roles, ['assistant', 'assistant', 'assistant', 'user'])
  assert.deepEqual(plan.physicalFlags, [false, false, false, true])

  for (let n = 0; n < 3; n++) {
    assert.equal(plan.texts[n], toml.renderHistoricFrame(`frame body ${n}`))
  }
  assert.equal(plan.texts[3], combinedDelta)
  // No separate trailing instruction message.
  assert.equal(plan.texts.filter((t) => t === prompt.normalInstruction).length, 0)
})
test('WHAT[context-compression-012] COMPANION_005_normal_without_frames_is_one_combined_delta', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: [],
    delta: { messageId: 'msg_first', items: dataItems },
  })

  assert.equal(plan.texts.filter(isHistoricFrame).length, 0)
  assert.deepEqual(plan.texts, [combinedDelta])
  assert.deepEqual(plan.physicalFlags, [true])
  assert.equal(plan.isFirstTurnShape, true)
  assert.equal(plan.system, undefined)
})
test('WHAT[context-compression-012] COMPANION_005_combined_delta_is_always_the_last_user_message', () => {
  const withFrames = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(2),
    delta: { messageId: 'msg_d', items: dataItems },
  })
  const withoutFrames = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: [],
    delta: { messageId: 'msg_d', items: dataItems },
  })

  assert.equal(withFrames.texts.at(-1), combinedDelta)
  assert.equal(withoutFrames.texts.at(-1), combinedDelta)
  assert.equal(withFrames.messages.at(-1).physical, true)
  assert.equal(withoutFrames.messages.at(-1).physical, true)
})
test('WHAT[context-compression-012] COMPANION_005_each_frame_is_exactly_one_do_not_exec_document', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(4),
    delta: { messageId: 'msg_d', items: dataItems },
  })

  const frameTexts = plan.texts.slice(0, 4)
  for (const text of frameTexts) {
    assert.equal((text.match(/\[\[do_not_exec\]\]/g) || []).length, 1)
    assert.equal(text.startsWith('[[do_not_exec]]'), true)
    assert.equal(text.includes('# Working Record'), false)
  }
  assert.equal(plan.texts[4], combinedDelta)
})
test('WHAT[context-compression-012] COMPANION_005_the_delta_carries_the_id_the_Host_persisted', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(1),
    delta: { messageId: 'msg_real', items: dataItems },
  })

  const physical = plan.messages.filter((m) => m.physical)
  assert.equal(physical.length, 1)
  assert.equal(physical[0].id, 'msg_real')
  assert.equal(physical[0].text, combinedDelta)
})
test('WHAT[context-compression-012] COMPANION_009_the_same_epoch_and_frames_produce_byte_identical_messages', () => {
  const args = {
    blogger: 'ses_y',
    epoch: 4,
    kind: proj.normal,
    frames: frames(2),
    delta: { messageId: 'msg_d', items: dataItems },
  }

  assert.deepEqual(proj.build(spy, args).messages, proj.build(spy, args).messages)
})
test('WHAT[context-compression-012] ENFORCER_071_normal_interleaves_tips_with_frames_then_delta', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(2),
    delta: { messageId: 'msg_delta', items: dataItems },
    previousTips: [
      { field: 'primitive-obsession', cycleId: 'msg_c1' },
      { field: 'ignored-tdd', cycleId: 'msg_c2' },
    ],
  })

  assert.deepEqual(
    plan.texts.map((t) => {
      if (isPreviousTip(t)) return 'tip'
      if (isHistoricFrame(t)) return 'frame'
      if (isCombinedNormalDelta(t)) return 'delta'
      return 'other'
    }),
    ['tip', 'frame', 'tip', 'frame', 'delta'],
  )
  assert.match(plan.texts[0], /tip = "primitive-obsession"/)
  assert.equal(plan.texts[1], toml.renderHistoricFrame('frame body 0'))
  assert.match(plan.texts[2], /tip = "ignored-tdd"/)
  assert.equal(plan.texts[3], toml.renderHistoricFrame('frame body 1'))
  assert.equal(plan.messages.at(-1).physical, true)
})
test('WHAT[context-compression-012] ENFORCER_071_unpaired_tips_or_frames_append_after_zip', () => {
  const extraTip = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(1),
    delta: { messageId: 'msg_d', items: dataItems },
    previousTips: [
      { field: 'primitive-obsession', cycleId: 'c1' },
      { field: 'ignored-tdd', cycleId: 'c2' },
    ],
  })
  assert.deepEqual(
    extraTip.texts.map((t) => (isPreviousTip(t) ? 'tip' : isHistoricFrame(t) ? 'frame' : 'delta')),
    ['tip', 'frame', 'tip', 'delta'],
  )

  const extraFrame = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(2),
    delta: { messageId: 'msg_d', items: dataItems },
    previousTips: [{ field: 'primitive-obsession', cycleId: 'c1' }],
  })
  assert.deepEqual(
    extraFrame.texts.map((t) => (isPreviousTip(t) ? 'tip' : isHistoricFrame(t) ? 'frame' : 'delta')),
    ['tip', 'frame', 'frame', 'delta'],
  )
})
test('WHAT[context-compression-012] COMPANION_007_canonical_digest_uses_semantic_projection_not_toml', () => {
  const seal = ident.sealRoot(spy, {
    session: 'ses_y',
    epoch: 2,
    cutoff: 5,
    prefixDigest: 'prefix-5',
    frozenDigest: 'frozen-5',
  })

  assert.equal(seal, '«ses_y|2|5|prefix-5|frozen-5»')
  assert.doesNotMatch(seal, /toml|\[\[item\]\]/i)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const algebra = await import("../../../dist/Participant/Provider/Projection/Surface.js");
const companionProj = await import("../../../dist/Context/Companion/ProjectionSurface.js");

const emptyProjection = {
  providerId: null,
  modelId: null,
  variant: null,
  tools: [],
  system: [],
  messages: [],
}
const snapshot = () => algebra.projectionSnapshot(emptyProjection)
const planNames = (intents) => {
  const result = algebra.plan(intents)
  assert.equal(result.ok, true, `expected Ok plan, got ${JSON.stringify(result)}`)
  return result.intents
}
const assertOwnerRowsMatchBuilder = (intent, builderPlan) => {
  assert.deepEqual(
    intent.rows.map((row) => [row.message.role, row.message.parts[0]?.text]),
    builderPlan.messages.map((message) => [message.role, message.text]),
  )
  assert.deepEqual(
    intent.rows.map((row) => row.hostMessageId),
    builderPlan.messages.map((message) => message.id),
  )
  assert.deepEqual(
    intent.rows.map((row) => row.hostIsPhysical),
    builderPlan.physicalFlags,
  )
}

test('WHAT[context-compression-012] PROJ_008_Companion_owner_normal_rows_render_through_generic_projection', () => {
  const spy = (input) => `«${input}»`
  const frames = [
    { digest: 'sha-f0', body: 'frame body 0' },
    { digest: 'sha-f1', body: 'frame body 1' },
  ]
  const previousTips = [{ field: 'progress', cycleId: 'cycle-1' }]
  const delta = { messageId: 'msg_delta', toml: '[[new_work_to_record]]\nuser = "work"' }
  const input = { blogger: 'ses_y', epoch: 0, kind: 'normal', frames, delta, previousTips }

  const intent = companionProj.projectionIntent(spy, input)
  const builderPlan = companionProj.build(spy, input)

  assert.equal(intent.kind, 'ReplaceMessageBase')
  assert.deepEqual(planNames([intent]), ['ReplaceMessageBase'])
  assertOwnerRowsMatchBuilder(intent, builderPlan)

  const rendered = algebra.renderMessages(snapshot(), [], [intent])
  const renderedWithHost = algebra.renderMessagesWithHostIds(snapshot(), [], [intent])
  assert.deepEqual(
    rendered.map((message) => [message.role, message.parts[0]?.text]),
    builderPlan.messages.map((message) => [message.role, message.text]),
  )
  assert.deepEqual(
    renderedWithHost.hostMessageIds,
    builderPlan.messages.map((message) => message.id),
  )
  assert.deepEqual(renderedWithHost.hostIsPhysical, builderPlan.physicalFlags)
})
test('WHAT[context-compression-012] PROJ_008_frame_only_owner_inserts_before_message_index_one_and_empty_is_no_op', () => {
  const spy = (input) => `«${input}»`
  const input = {
    blogger: 'ses_y',
    epoch: 2,
    kind: 'normal',
    frames: [{ digest: 'sha-f0', body: 'frame body 0' }],
  }

  const intent = companionProj.projectionIntent(spy, input)
  const builderPlan = companionProj.build(spy, input)

  assert.equal(intent.kind, 'InsertMessageRows')
  assert.deepEqual(intent.anchor, { kind: 'BeforeMessageIndex', index: 1 })
  assertOwnerRowsMatchBuilder(intent, builderPlan)
  assert.equal(companionProj.projectionIntent(spy, { ...input, frames: [] }), null)
})
}

// tests/unit/Context/companion-projection.test.mjs — COMPANION-004/005/010/013.
//
// Provider-visible request shape:
// assistant [[do_not_exec]] historic_frame(s) + one combined normal user delta
// (instruction comment header first, then [[new_work_to_record]] data).

import assert from 'node:assert/strict'
import test from 'node:test'
import * as toml from '../../../dist/Context/Companion/Blogger/TomlSurface.js'
import * as owner from '../../../dist/Context/Companion/ProjectionSurface.js'
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

// ── prompt text is fixed and carries no numbers ─────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_004_request_instructions_require_exactly_one_blog_call', () => {
  assert.match(prompt.normalInstruction, /# Write the dense work-log continuation now/)
  assert.match(prompt.normalInstruction, /exactly once/)
  assert.match(prompt.squashInstruction, /# Rewrite the preceding assistant work-log frames now/)
  assert.match(prompt.squashInstruction, /exactly once/)
  assert.equal(prompt.system, undefined, 'System is owned by PromptResources Blogger Role Law, not CompanionPrompt')
})

test('WHAT[CONTEXT-COMPRESSION-001] CTX_001_no_prompt_carries_a_token_count_or_output_budget', () => {
  const all = [
    prompt.normalInstruction,
    prompt.squashInstruction,
    prompt.memoryPreamble,
    prompt.workingRecord('body'),
    prompt.newWork(dataItems),
  ]

  for (const text of all) {
    assert.doesNotMatch(text, /token|budget|limit|KiB|bytes/i, 'no capacity vocabulary')
    assert.doesNotMatch(text, /%s|%d|\{0\}/, 'no format placeholders')
  }
})

test('WHAT[CONTEXT-COMPRESSION-012] ENFORCER_030_squash_and_normal_require_tip_not_omit_scores', () => {
  assert.match(prompt.squashInstruction, /required tip|catalog field/)
  assert.match(prompt.squashInstruction, /do not output ordinary assistant prose/i)
  assert.doesNotMatch(prompt.squashInstruction, /omit all scores/)
  assert.match(prompt.normalInstruction, /required tip|catalog field/)
  assert.doesNotMatch(prompt.normalInstruction, /omit.*scores/i)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_010_memory_block_is_one_instruction_plane', () => {
  const block = prompt.memoryBlock('B CONTENT')

  assert.match(block, /prior responsibility/)
  assert.match(block, /^# .*prior responsibility/m)
  assert.match(block, /^# B CONTENT$/m)
  assert.doesNotMatch(block, /<work-log>|not a new user instruction/)
})

test('WHAT[CONTEXT-COMPRESSION-017] COMPANION_010_same_session_lwr_returns_responsibility_without_delegation_fields', () => {
  const lwr = 'Opening\nhuman-root task\n\nChronicle\nself history'
  const block = prompt.memoryBlock(lwr)

  for (const line of ['Opening', 'human-root task', 'Chronicle', 'self history']) {
    assert.match(block, new RegExp(`^# ${line}$`, 'm'))
  }
  assert.doesNotMatch(block, /(?:^|\n)commissioner_record\s*=/)
  assert.doesNotMatch(block, /(?:^|\n)attached_work_record\s*=/)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_message_wrappers_are_toml_not_markdown_titles', () => {
  assert.equal(prompt.workingRecord('frame body 0'), toml.renderHistoricFrame('frame body 0'))
  assert.equal(prompt.workingRecord('frame body 0').includes('[[do_not_exec]]'), true)
  assert.equal(prompt.workingRecord('frame body 0').includes('historic_frame'), true)
  assert.equal(prompt.workingRecord('frame body 0').includes('# Working Record'), false)
  assert.equal(prompt.newWork(dataItems).includes('# New Work To Record'), false)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_new_work_is_instruction_header_then_data_body', () => {
  const rendered = prompt.newWork(dataItems)
  assert.equal(rendered.startsWith('# Write the dense work-log continuation now'), true)
  assert.equal(rendered.includes('\n\n[[new_work_to_record]]'), true)
  assert.equal(rendered.endsWith(dataToml + '\n') || rendered.endsWith(dataToml), true)
  // Data body has no extra instruction after tables.
  const dataStart = rendered.indexOf('[[new_work_to_record]]')
  assert.equal(rendered.slice(dataStart).includes('# Write'), false)
})

// ── synthetic identities (COMPANION-013) ───────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_seal_root_is_derived_from_exactly_the_candidate_identity', () => {
  const seal = ident.sealRoot(spy, {
    session: 'ses_x',
    epoch: 3,
    cutoff: 7,
    prefixDigest: 'prefix-7',
    frozenDigest: 'frozen-7',
  })

  assert.equal(seal, '«ses_x|3|7|prefix-7|frozen-7»')
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_seal_root_changes_when_any_identity_field_changes', () => {
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

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_seal_root_is_stable_across_calls', () => {
  const args = { session: 'ses_x', epoch: 2, cutoff: 9, prefixDigest: 'p', frozenDigest: 'f' }
  assert.equal(ident.sealRoot(spy, args), ident.sealRoot(spy, args))
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_companion_memory_id_is_a_function_of_the_seal_alone', () => {
  assert.equal(ident.companionMemoryMessageId(spy, 'SEAL'), '«SEAL|companion-memory»')
  assert.equal(
    ident.companionMemoryMessageId(spy, 'SEAL'),
    ident.companionMemoryMessageId(spy, 'SEAL'),
  )
  assert.notEqual(ident.companionMemoryMessageId(spy, 'SEAL'), ident.companionMemoryMessageId(spy, 'OTHER'))
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_frame_id_needs_both_the_ordinal_and_the_frame_epoch', () => {
  const id = (over) =>
    ident.frameMessageId(spy, { blogger: 'ses_y', epoch: 0, ordinal: 0, digest: 'sha-a', ...over })

  assert.notEqual(id({}), id({ ordinal: 1 }))
  assert.notEqual(id({}), id({ epoch: 1 }))
  assert.equal(id({}), '«ses_y|0|0|sha-a|blog-frame»')
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_instruction_id_distinguishes_normal_from_squash', () => {
  const normal = ident.instructionMessageId(spy, { blogger: 'ses_y', epoch: 0, kind: 'normal' })
  const squash = ident.instructionMessageId(spy, { blogger: 'ses_y', epoch: 0, kind: 'squash' })

  assert.notEqual(normal, squash)
  assert.equal(normal, '«ses_y|0|normal|instruction»')
})

// ── the normal projection (COMPANION-005) ──────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_normal_with_frames_is_assistant_do_not_exec_then_combined_delta', () => {
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

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_normal_without_frames_is_one_combined_delta', () => {
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

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_combined_delta_is_always_the_last_user_message', () => {
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

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_each_frame_is_exactly_one_do_not_exec_document', () => {
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

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_the_delta_carries_the_id_the_Host_persisted', () => {
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

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_frame_ids_are_positional_within_the_current_sequence', () => {
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

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_009_the_same_epoch_and_frames_produce_byte_identical_messages', () => {
  const args = {
    blogger: 'ses_y',
    epoch: 4,
    kind: proj.normal,
    frames: frames(2),
    delta: { messageId: 'msg_d', items: dataItems },
  }

  assert.deepEqual(proj.build(spy, args).messages, proj.build(spy, args).messages)
})

// ── paired tip + frame observation units (rulebook §2 / ENFORCER-071) ──────

const isPreviousTip = (text) => text.includes('previous_enforcer_tip')

test('WHAT[CONTEXT-COMPRESSION-012] ENFORCER_071_normal_interleaves_tips_with_frames_then_delta', () => {
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

test('WHAT[CONTEXT-COMPRESSION-012] ENFORCER_071_unpaired_tips_or_frames_append_after_zip', () => {
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

// ── the squash projection (CTX-012) ────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_projects_only_oldest_historic_frames_then_instruction', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(2),
    frames: frames(4),
    delta: undefined,
  })

  assert.equal(plan.texts.filter(isHistoricFrame).length, 2)
  assert.equal(plan.texts.some((t) => t.includes('[[new_work_to_record]]')), false)
  assert.deepEqual(plan.texts, [
    toml.renderHistoricFrame('frame body 0'),
    toml.renderHistoricFrame('frame body 1'),
    prompt.squashInstruction,
  ])
  assert.deepEqual(plan.physicalFlags, [false, false, false])
  assert.deepEqual(plan.roles, ['assistant', 'assistant', 'user'])
  assert.equal(plan.system, undefined)
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_pairs_tips_with_covered_frames_then_instruction', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(2),
    frames: frames(4),
    delta: undefined,
    previousTips: [
      { field: 'primitive-obsession', cycleId: 'c1' },
      { field: 'ignored-tdd', cycleId: 'c2' },
    ],
  })

  assert.deepEqual(
    plan.texts.map((t) => {
      if (isPreviousTip(t)) return 'tip'
      if (isHistoricFrame(t)) return 'frame'
      if (t === prompt.squashInstruction) return 'instruction'
      return 'other'
    }),
    ['tip', 'frame', 'tip', 'frame', 'instruction'],
  )
  // Only oldest k=2 frames; later bodies must not appear.
  assert.equal(
    plan.texts.some((t) => t.includes('frame body 2') || t.includes('frame body 3')),
    false,
  )
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_a_squash_ignores_a_delta_even_if_one_is_supplied', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(1),
    frames: frames(3),
    delta: { messageId: 'msg_should_not_appear', items: dataItems },
  })

  assert.deepEqual(plan.texts, [toml.renderHistoricFrame('frame body 0'), prompt.squashInstruction])
  assert.equal(plan.physicalFlags.includes(true), false)
  assert.equal(
    plan.messages.some((m) => m.text.includes('UNCONSUMED')),
    false,
    'the delta must not reach a squash request',
  )
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_a_squash_never_shows_the_later_frames', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.squash(2),
    frames: frames(5),
    delta: undefined,
  })

  for (const n of [2, 3, 4]) {
    assert.equal(
      plan.texts.some((t) => t.includes(`frame body ${n}`)),
      false,
      `frame ${n} is outside the squash range and must not be projected`,
    )
  }
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_and_normal_requests_use_different_last_message_ids', () => {
  const shared = { blogger: 'ses_y', epoch: 0, frames: frames(1) }

  const normal = proj.build(spy, { ...shared, kind: proj.normal, delta: { messageId: 'm', items: dataItems } })
  const squash = proj.build(spy, { ...shared, kind: proj.squash(1), delta: undefined })

  assert.equal(normal.messages.at(-1).id, 'm')
  assert.equal(squash.messages.at(-1).id, '«ses_y|0|squash|instruction»')
  assert.notEqual(normal.messages.at(-1).id, squash.messages.at(-1).id)
})

// ── COMPANION-007: the canonical candidate digest is a function of the semantic projection, not the TOML text

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_007_canonical_digest_uses_semantic_projection_not_toml', () => {
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

test('WHAT[CONTEXT-COMPRESSION-018] COMPANION_018_first_turn_shape_is_false_when_historic_frames_or_tips_present', () => {
  const withFrame = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(1),
    delta: { messageId: 'msg_d', items: dataItems },
  })
  assert.equal(withFrame.isFirstTurnShape, false)

  const withTip = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: [],
    delta: { messageId: 'msg_d', items: dataItems },
    previousTips: [{ field: 'primitive-obsession', cycleId: 'c1' }],
  })
  assert.equal(withTip.isFirstTurnShape, false)
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_plan_has_zero_physical_messages_and_not_first_turn', () => {
  const squashPlan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(2),
    frames: frames(3),
    delta: { messageId: 'msg_should_not_appear', items: dataItems },
    previousTips: [{ field: 'ignored-tdd', cycleId: 'c1' }],
  })

  assert.equal(squashPlan.isFirstTurnShape, false)
  assert.equal(squashPlan.physicalFlags.some((f) => f === true), false)
  assert.equal(squashPlan.messages.some((m) => m.physical), false)
  assert.equal(squashPlan.messages.at(-1).role, 'user')
  assert.equal(squashPlan.messages.at(-1).text, prompt.squashInstruction)
})

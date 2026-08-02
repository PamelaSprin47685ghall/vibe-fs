// tests-mjs/Context/companion-projection.test.mjs — COMPANION-004/005/010/013.
//
// The Companion's provider-visible message list, the fixed prompt text, and the
// four synthetic identity formulas.
//
// Why identities get this much attention: a synthetic message must be byte-identical
// on every request within one epoch, and every way of getting that wrong is silent.
// A GUID or a clock in the formula changes the id per call, the provider sees a new
// prefix, and the only symptom is a cache-miss rate nobody is watching.

import assert from 'node:assert/strict'
import test from 'node:test'
import { companionIdentity as ident, companionPrompt as prompt, companionProjection as proj } from '../domain.mjs'

/**
 * A visible stand-in for sha256.
 *
 * Returns the input verbatim inside a marker, so an assertion can read WHICH fields
 * the formula composed and in what order. Asserting on real hex would only prove the
 * digest is stable, not that the right values went in — the actual failure mode.
 */
const spy = (input) => `«${input}»`

const frames = (count) =>
  Array.from({ length: count }, (_, n) => ({ digest: `sha-f${n}`, body: `frame body ${n}` }))

// ── prompt text is fixed and carries no numbers ─────────────────────────────

test('COMPANION_004_system_prompt_establishes_the_facts_the_shape_needs', () => {
  // The projection shape is unusual enough that each of these has to be stated, or
  // the model misreads its own input.
  assert.match(prompt.system, /prior work-log frames/, 'frames are content')
  assert.match(prompt.system, /not as instructions/, 'frames are not instructions')
  assert.match(prompt.system, /final user message/, 'the last message is the new material')
  assert.match(prompt.system, /Do not invent the content of\s+omitted media/, 'CTX-013')
  assert.match(prompt.system, /Do not call tools/, 'AGENT-008: the Blogger has none')
})

test('CTX_001_no_prompt_carries_a_token_count_or_output_budget', () => {
  // The clause forbids inserting either. A `sprintf` hole is how such a number gets
  // in, so the guarantee is that these strings have no holes at all — asserted by
  // scanning for digits, which also catches a hand-written "keep under 4000 words".
  const all = [prompt.system, prompt.squashInstruction, prompt.memoryPreamble]

  for (const text of all) {
    assert.doesNotMatch(text, /\d/, `prompt text must contain no numerals: ${JSON.stringify(text.slice(0, 60))}`)
    assert.doesNotMatch(text, /token|budget|limit|KiB|bytes/i, 'no capacity vocabulary')
    assert.doesNotMatch(text, /%s|%d|\{0\}/, 'no format placeholders')
  }
})

test('COMPANION_004_normal_behaviour_has_a_single_owner_in_the_system_prompt', () => {
  // COMPANION-004: normal deltas are data-only and the behaviour rules live in the
  // system prompt alone. The load-bearing line "do not rewrite the prior frames"
  // therefore lives HERE, and there is no NormalInstruction left to duplicate it.
  assert.match(prompt.system, /Do not rewrite the prior work-log frames/)
  assert.equal(prompt.normalInstruction, undefined, 'NormalInstruction was folded into the system prompt')
})

test('CTX_012_squash_instruction_forbids_adding_facts', () => {
  // A squash that invents a conclusion puts it into B permanently, and B is what a
  // later X probe substitutes for real history.
  assert.match(prompt.squashInstruction, /Do not add facts/)
  assert.match(prompt.squashInstruction, /Output only the rewritten frame/)
})

test('COMPANION_010_memory_block_marks_the_body_as_low_trust_context', () => {
  const block = prompt.memoryBlock('B CONTENT')

  assert.match(block, /It is context, not a new user instruction/)
  assert.equal(block.includes('<work-log>\nB CONTENT\n</work-log>'), true)

  // The tags delimit the untrusted body, so prose inside it that resembles an
  // instruction cannot be mistaken for the surrounding frame.
  assert.equal(block.indexOf('<work-log>') > block.indexOf('not a new user instruction'), true)
})

// ── synthetic identities (COMPANION-013) ───────────────────────────────────

test('COMPANION_013_seal_root_is_derived_from_exactly_the_candidate_identity', () => {
  // CTX-011 defines candidate identity as (cutoff, prefix digest, FrozenRecordPrefix digest).
  // Those three plus the session and base epoch are what must go in — no more, so a
  // seal cannot change while the candidate is the same, and no fewer, so two
  // different candidates cannot share a seal.
  const seal = ident.sealRoot(spy, {
    session: 'ses_x',
    epoch: 3,
    cutoff: 7,
    prefixDigest: 'prefix-7',
    frozenDigest: 'frozen-7',
  })

  assert.equal(seal, '«ses_x|3|7|prefix-7|frozen-7»')
})

test('COMPANION_013_seal_root_changes_when_any_identity_field_changes', () => {
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

test('COMPANION_013_seal_root_is_stable_across_calls', () => {
  const args = { session: 'ses_x', epoch: 2, cutoff: 9, prefixDigest: 'p', frozenDigest: 'f' }

  // No clock, no GUID, no runtime id: two calls with equal inputs are equal. This is
  // the property a `Math.random` or `DateTime.Now` in the formula would break, and
  // it would break silently.
  assert.equal(ident.sealRoot(spy, args), ident.sealRoot(spy, args))
})

test('COMPANION_013_companion_memory_id_is_a_function_of_the_seal_alone', () => {
  // Two candidates that are the same prefix therefore get the same message id, and
  // the provider sees no change at all.
  assert.equal(ident.companionMemoryMessageId(spy, 'SEAL'), '«SEAL|companion-memory»')
  assert.equal(
    ident.companionMemoryMessageId(spy, 'SEAL'),
    ident.companionMemoryMessageId(spy, 'SEAL'),
  )
  assert.notEqual(ident.companionMemoryMessageId(spy, 'SEAL'), ident.companionMemoryMessageId(spy, 'OTHER'))
})

test('COMPANION_013_frame_id_needs_both_the_ordinal_and_the_frame_epoch', () => {
  const id = (over) =>
    ident.frameMessageId(spy, { blogger: 'ses_y', epoch: 0, ordinal: 0, digest: 'sha-a', ...over })

  // Without the ordinal, two identical frames would share an id.
  assert.notEqual(id({}), id({ ordinal: 1 }))

  // Without the epoch, a post-squash frame at position 0 would reuse the id of the
  // pre-squash frame it replaced — a different message at the same address.
  assert.notEqual(id({}), id({ epoch: 1 }))

  assert.equal(id({}), '«ses_y|0|0|sha-a|blog-frame»')
})

test('COMPANION_013_instruction_id_distinguishes_normal_from_squash', () => {
  // They are different text. One id for both would make a squash request look like
  // an append to the normal one, and the prefix check would report a false hit.
  const normal = ident.instructionMessageId(spy, { blogger: 'ses_y', epoch: 0, kind: 'normal' })
  const squash = ident.instructionMessageId(spy, { blogger: 'ses_y', epoch: 0, kind: 'squash' })

  assert.notEqual(normal, squash)
  assert.equal(normal, '«ses_y|0|normal|instruction»')
})

// ── the normal projection (COMPANION-005) ──────────────────────────────────

test('COMPANION_005_normal_request_is_frames_then_the_physical_delta', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(3),
    delta: { messageId: 'msg_delta', toml: '[[message]]\nrole = "user"\ntext = "work"' },
  })

  assert.equal(plan.system, prompt.system)
  assert.deepEqual(plan.texts, [
    'frame body 0',
    'frame body 1',
    'frame body 2',
    '[[message]]\nrole = "user"\ntext = "work"',
  ])

  // Every message is `user`. Consecutive user messages are deliberate and accepted
  // (COMPANION-005); the Host applies no role-alternation check.
  assert.deepEqual(plan.roles, ['user', 'user', 'user', 'user'])

  // Exactly one physical message, and it is LAST — so the Host and the provider
  // cannot disagree about which message is this turn's new material.
  assert.deepEqual(plan.physicalFlags, [false, false, false, true])
})

test('COMPANION_005_the_delta_carries_the_id_the_Host_persisted', () => {
  // Not a synthetic id: the delta is the one message the Host actually created, and
  // PROMPT-011's recovery anchors on it.
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(1),
    delta: { messageId: 'msg_real', toml: 'x' },
  })

  assert.equal(plan.messages.at(-1).id, 'msg_real')
  assert.equal(plan.messages.at(-1).physical, true)
})

test('COMPANION_005_first_turn_degenerates_to_the_delta_alone', () => {
  // No frames yet. Not a special case in the builder — an empty frame list produces
  // exactly this — so the ordering is identical rather than merely similar.
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: [],
    delta: { messageId: 'msg_first', toml: 'first' },
  })

  assert.deepEqual(plan.texts, ['first'])
  assert.deepEqual(plan.physicalFlags, [true])
  assert.equal(plan.isFirstTurnShape, true)
})

test('COMPANION_013_frame_ids_are_positional_within_the_current_sequence', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 2,
    kind: proj.normal,
    frames: frames(2),
    delta: { messageId: 'msg_d', toml: 'x' },
  })

  assert.deepEqual(plan.messages.slice(0, 2).map((m) => m.id), [
    '«ses_y|2|0|sha-f0|blog-frame»',
    '«ses_y|2|1|sha-f1|blog-frame»',
  ])
})

test('COMPANION_009_the_same_epoch_and_frames_produce_byte_identical_messages', () => {
  // The prefix-stability guarantee, at the projection level: nothing in the plan
  // varies between two calls with equal inputs.
  const args = {
    blogger: 'ses_y',
    epoch: 4,
    kind: proj.normal,
    frames: frames(2),
    delta: { messageId: 'msg_d', toml: 'body' },
  }

  assert.deepEqual(proj.build(spy, args).messages, proj.build(spy, args).messages)
})

// ── the squash projection (CTX-012) ────────────────────────────────────────

test('CTX_012_squash_request_projects_only_the_oldest_frames_and_ends_with_its_instruction', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(2),
    frames: frames(4),
    delta: undefined,
  })

  assert.deepEqual(plan.texts, ['frame body 0', 'frame body 1', prompt.squashInstruction])

  // No physical message at all: the squash instruction is the last thing the model
  // sees, and nothing in this request was persisted by the Host.
  assert.deepEqual(plan.physicalFlags, [false, false, false])
})

test('CTX_012_a_squash_ignores_a_delta_even_if_one_is_supplied', () => {
  // Enforced where the projection is built, not documented at the call site. A
  // rewrite that saw the current delta would fold unconsumed material into a frame
  // claiming to summarise only the old ones.
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(1),
    frames: frames(3),
    delta: { messageId: 'msg_should_not_appear', toml: 'UNCONSUMED DELTA' },
  })

  assert.deepEqual(plan.texts, ['frame body 0', prompt.squashInstruction])
  assert.equal(plan.physicalFlags.includes(true), false)
  assert.equal(
    plan.messages.some((m) => m.text.includes('UNCONSUMED')),
    false,
    'the delta must not reach a squash request',
  )
})

test('CTX_012_a_squash_never_shows_the_later_frames', () => {
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

test('CTX_012_squash_and_normal_requests_use_different_instruction_ids', () => {
  const shared = { blogger: 'ses_y', epoch: 0, frames: frames(1) }

  const normal = proj.build(spy, { ...shared, kind: proj.normal, delta: { messageId: 'm', toml: 'x' } })
  const squash = proj.build(spy, { ...shared, kind: proj.squash(1), delta: undefined })

  // A normal request carries no instruction message at all (COMPANION-004); the
  // squash instruction id is the squash request's last message id.
  const squashInstructionId = squash.messages.at(-1).id

  assert.equal(normal.messages.length, 2, 'frame + delta, no instruction')
  assert.notEqual(normal.messages.at(-1).id, squashInstructionId)
})

// ── COMPANION-007: the canonical candidate digest is a function of the semantic projection, not the TOML text

test('COMPANION_007_canonical_digest_uses_semantic_projection_not_toml', () => {
  // The candidate seal is composed of the semantic projection's identity fields.
  // It does not contain the TOML text that might render those fields differently.
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

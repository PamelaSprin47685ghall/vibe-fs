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
test('WHAT[context-compression-011] COMPANION_013_instruction_id_binds_blogger_epoch_and_kind', () => {
  const normal = ident.instructionMessageId(spy, { blogger: 'ses_y', epoch: 0, kind: 'normal' })
  const nextEpoch = ident.instructionMessageId(spy, { blogger: 'ses_y', epoch: 1, kind: 'normal' })

  assert.notEqual(normal, nextEpoch)
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
    materialCutoff: 7,
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
    materialCutoff: 5,
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

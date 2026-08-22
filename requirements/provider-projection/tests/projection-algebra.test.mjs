// Split from tests/unit/context/projection-algebra.test.mjs (cutover Wave 2a); owner: provider-projection.
//
// PROJ-004 / PROJ-005 / PROJ-006 / PROJ-008 algebra oracle — intent
// order/merge/conflict/permutation, deterministic render, wire/digest byte
// equality. CTX_011_step5_* went to prefix-stability; the feature production
// byte contracts went to interaction-authority (Repair), review-assurance
// (Challenge) and context-compression (BlogFrames ≡ CompanionProjectionBuilder).
//
// Stage 1–2 closed loop (PROJ-008 steps 1–2: plain X + ActivePrefixEpoch +
// attempt-local PrefixProbe) plus step 3a Domain skeleton proofs for the six
// remaining intents: plan/order/conflict algebra and minimal render shapes.
// Production wiring (CompanionTransform / SpikePlugin) is out of scope here.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const activation = (overrides = {}) => ({
  syntheticMessageId: overrides.syntheticMessageId ?? overrides.SyntheticMessageId ?? 'synthetic-1',
  memory: overrides.memory ?? overrides.Memory ?? '<work-log>\\nTHE WORK LOG\\n</work-log>',
  dropLeading: overrides.dropLeading ?? overrides.DropLeading ?? 2,
})

const projectionIntent = {
  keepPhysicalPrefix: Projection.keepPhysicalPrefix,
  activatePrefixEpoch: (value) => Projection.activatePrefixEpoch(value),
  insertBlogFrames: (value = {}) => Projection.insertBlogFrames({
    requestKind: value.requestKind ?? value.RequestKind ?? 'normal',
    squashFrameCount: value.squashFrameCount ?? value.SquashFrameCount ?? 0,
    bloggerSessionId: value.bloggerSessionId ?? value.BloggerSessionId ?? 'ses_blogger',
    frameEpoch: value.frameEpoch ?? value.FrameEpoch ?? 0,
    physicalDelta: value.physicalDelta ?? value.PhysicalDelta ?? null,
    previousTips: value.previousTips ?? value.PreviousTips ?? [],
    normalInstructionLines: value.normalInstructionLines ?? value.NormalInstructionLines ?? [],
    squashInstructionLines: value.squashInstructionLines ?? value.SquashInstructionLines ?? [],
  }),
  insertRepair: (value) => Projection.insertRepair(value.requestKey ?? value.RequestKey ?? value),
  get suppressTransportOnly() { return Projection.suppressTransportOnly },
  get reanchorAfterCompaction() { return Projection.reanchorAfterCompaction },
}

const snapshotFromMessages = (messages, extras = {}) =>
  Projection.projectionSnapshot(
    Projection.semanticProjection(messages),
    {
      committedPrefix: extras.committedPrefix ?? null,
      blogFrames: extras.blogFrames ?? [],
      transportMessages: extras.transportMessages ?? [],
      hostReanchor: extras.hostReanchor ?? null,
    },
  )

const projectionAlgebra = {
  plan: (intents) => Projection.plan(intents),
  renderPrefix: (intents) => Projection.renderPrefix(intents),
  rendered: {
    physical: { name: 'PhysicalPrefix', activation: null },
    synthetic: (value) => ({ name: 'SyntheticPrefix', activation: value }),
  },
  renderMessages: (messages, rendered) => Projection.renderMessages(
    snapshotFromMessages(messages),
    messages,
    rendered.name === 'PhysicalPrefix'
      ? [projectionIntent.keepPhysicalPrefix]
      : [projectionIntent.activatePrefixEpoch(rendered.activation)],
  ),
  renderMessagesWithIntents: (snapshot, messages, intents) => Projection.renderMessages(snapshot, messages, intents),
  renderMessagesWithHostIds: (sha256, snapshot, messages, intents) =>
    Projection.renderMessagesWithHostIds(sha256, snapshot, messages, intents),
}

const providerProjection = {
  decodeMessageView: (raw) => ({ messages: Projection.decodeMessages(raw).messages }),
  applyRenderedPrefix: (raw, rendered) => Projection.applyRenderedPrefix(raw, rendered),
  toSemantic: (wire) => Projection.semanticProjection(wire.messages ?? wire),
  renderWire: (wire) => Projection.renderWire(wire.messages ?? wire),
  renderSemantic: (semantic) => Projection.renderSemantic(semantic),
  semanticallyEqual: (left, right) => Projection.semanticallyEqual(left, right),
}

const projectionSnapshot = {
  blogFrame: ({ kind = 'Entry', digest = 'frame-digest', body = 'frame body' } = {}) => ({ kind, digest, body }),
  hostReanchor: ({ previous = 'epoch-0', next = 'epoch-1', run = 'compact-1' } = {}) => ({
    previousEpochId: previous,
    nextEpochId: next,
    observedCompactionRunId: run,
  }),
  of: ({ currentProjection, committedPrefix = null, blogFrames = [], transportMessages = [], hostReanchor = null }) =>
    Projection.projectionSnapshot(currentProjection, { committedPrefix, blogFrames, transportMessages, hostReanchor }),
}

const projectionConstants = { RepairInstruction: Projection.repairInstruction }
const items = (value) => value

// ── PROJ-006: fail-closed conflicts, order-independent ─────────────────────

test('WHAT[PROVIDER-PROJECTION-006] PROJ_006_prefix_intents_are_mutually_exclusive_at_the_same_anchor', () => {
  const keep = projectionIntent.keepPhysicalPrefix
  const activate = projectionIntent.activatePrefixEpoch(activation())

  // Either registration order is refused: the conflict is structural, never a
  // "first one wins" tie-break.
  const either = projectionAlgebra.plan([keep, activate])
  const reversed = projectionAlgebra.plan([activate, keep])

  assert.equal(either.ok, false)
  assert.equal(either.conflict, 'ConflictingPrefixSelection')
  assert.equal(either.first, 'KeepPhysicalPrefix')
  assert.equal(either.second, 'ActivatePrefixEpoch')

  assert.equal(reversed.ok, false)
  assert.equal(reversed.first, 'ActivatePrefixEpoch')
  assert.equal(reversed.second, 'KeepPhysicalPrefix')
})

test('WHAT[PROVIDER-PROJECTION-006] PROJ_006_two_activations_of_the_same_anchor_are_a_conflict', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.activatePrefixEpoch(activation({ DropLeading: 1 })),
    projectionIntent.activatePrefixEpoch(activation({ DropLeading: 3 })),
  ])

  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingPrefixSelection')
})

test('WHAT[PROVIDER-PROJECTION-006] PROJ_006_a_single_intent_or_none_plans_cleanly', () => {
  const keep = projectionIntent.keepPhysicalPrefix
  const activate = projectionIntent.activatePrefixEpoch(activation())

  assert.deepEqual(projectionAlgebra.plan([]), { ok: true, intents: [] })
  assert.deepEqual(projectionAlgebra.plan([keep]), { ok: true, intents: ['KeepPhysicalPrefix'] })
  assert.deepEqual(projectionAlgebra.plan([activate]), { ok: true, intents: ['ActivatePrefixEpoch'] })
})

// ── PROJ-004: the canonical renderer ───────────────────────────────────────

test('WHAT[PROVIDER-PROJECTION-001] PROJ_004_renderer_maps_intents_to_writeback_instructions', () => {
  // No intent and a plain keep both render to "send the physical prefix".
  assert.deepEqual(projectionAlgebra.renderPrefix([]), { name: 'PhysicalPrefix', activation: null })
  assert.deepEqual(projectionAlgebra.renderPrefix([projectionIntent.keepPhysicalPrefix]), {
    name: 'PhysicalPrefix',
    activation: null,
  })

  const rendered = projectionAlgebra.renderPrefix([projectionIntent.activatePrefixEpoch(activation())])
  assert.equal(rendered.name, 'SyntheticPrefix')
  assert.deepEqual(rendered.activation, activation())
})

test('WHAT[PROVIDER-PROJECTION-004] PROJ_004_physical_prefix_renders_the_messages_unchanged', () => {
  const view = projectionAlgebra.renderMessages(
    providerProjection.decodeMessageView(items([
      { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
      { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'second' }] },
    ])).messages,
    projectionAlgebra.rendered.physical,
  )

  assert.deepEqual(view, [
    { role: 'user', parts: [{ kind: 'text', text: 'first' }] },
    { role: 'user', parts: [{ kind: 'text', text: 'second' }] },
  ])
})

test('WHAT[PROVIDER-PROJECTION-004] PROJ_004_synthetic_prefix_prepends_the_memory_and_drops_the_physical_cutoff', () => {
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] },
  ]

  const view = projectionAlgebra.renderMessages(
    providerProjection.decodeMessageView(items(raw)).messages,
    projectionAlgebra.rendered.synthetic(activation()),
  )

  assert.deepEqual(view, [
    { role: 'user', parts: [{ kind: 'text', text: activation().memory }] },
    { role: 'user', parts: [{ kind: 'text', text: 'third' }] },
  ])
})

test('WHAT[PROVIDER-PROJECTION-004] PROJ_004_a_cutoff_beyond_the_message_view_fails_closed', () => {
  const messages = providerProjection.decodeMessageView(items([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
  ])).messages

  assert.throws(
    () =>
      projectionAlgebra.renderMessages(
        messages,
        projectionAlgebra.rendered.synthetic(activation({ DropLeading: 5 })),
      ),
    /cutoff/,
  )
})

// ── byte equality: the write-back path and the digest view are the same bytes ──

test('WHAT[PROVIDER-PROJECTION-004] PROJ_004_writeback_preserves_the_tail_objects_verbatim', () => {
  const tail = { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] }
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    tail,
  ]

  const written = providerProjection.applyRenderedPrefix(
    items(raw),
    projectionAlgebra.rendered.synthetic(activation()),
  )

  assert.equal(written.length, 2)
  assert.deepEqual(written[1], tail, 'the untouched tail remains semantically unchanged')

  const head = written[0]
  assert.equal(head.info.id, 'synthetic-1')
  assert.equal(head.info.role, 'user')
  assert.equal(head.parts[0].type, 'text')
  assert.equal(head.parts[0].text, activation().memory)
})

test('WHAT[PROVIDER-PROJECTION-003] PROJ_004_the_wire_view_and_the_written_back_bytes_decode_to_the_same_digest_input', () => {
  // The renderer's pure view is what a canonical digest is computed from; the
  // adapter's written-back object list is what the Host actually sends. They must
  // decode to identical wire views — otherwise the seal digests a different
  // prefix than the provider saw.
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] },
  ]
  const rendered = projectionAlgebra.rendered.synthetic(activation())

  const pureView = projectionAlgebra.renderMessages(providerProjection.decodeMessageView(items(raw)).messages, rendered)
  const writtenBack = providerProjection.decodeMessageView(
    items(providerProjection.applyRenderedPrefix(items(raw), rendered)),
  )

  // `PhysicalPrefix` renders its input unchanged, so running the write-back result
  // through it yields the decode of the bytes the Host will actually send.
  const writtenView = projectionAlgebra.renderMessages(writtenBack.messages, projectionAlgebra.rendered.physical)

  assert.deepEqual(pureView, writtenView)
})

test('WHAT[PROVIDER-PROJECTION-003] PROJ_004_the_written_back_bytes_are_the_frozen_prefix_shape', () => {
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'second' }] },
  ]

  const written = providerProjection.applyRenderedPrefix(
    items(raw),
    projectionAlgebra.rendered.synthetic(activation({ Memory: 'MEM', DropLeading: 1 })),
  )
  const bytes = providerProjection.renderWire(providerProjection.decodeMessageView(items(written)))

  // Frozen: the synthetic head is a user text message, the tail is untouched.
  assert.equal(
    bytes,
    '{"provider":null,"model":null,"variant":null,"tools":[],"system":[],' +
      '"messages":[{"role":"user","parts":[{"kind":"text","text":"MEM"}]},' +
      '{"role":"user","parts":[{"kind":"text","text":"second"}]}]}',
  )
})

// ── PROJ-008 stage 2: attempt-local PrefixProbe projection ─────────────────

const semanticView = (raw) => providerProjection.toSemantic(providerProjection.decodeMessageView(items(raw)))

const stage2Snapshot = (raw, committedPrefix = null) =>
  Projection.projectionSnapshot(semanticView(raw), { committedPrefix })

test('WHAT[PROVIDER-PROJECTION-002] PROJ_002_the_snapshot_is_the_attempt_local_input_contract', () => {
  const snapshot = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'second' }] },
  ])

  assert.equal(snapshot.currentProjection.providerId, null)
  assert.equal(snapshot.currentProjection.messages[0].role, 'user')
  assert.equal(snapshot.committedPrefix, null, 'no committed prefix = send physical history')
})

test('WHAT[PROVIDER-PROJECTION-002] PROJ_002_the_committed_prefix_in_the_snapshot_drives_the_prefix_decision', () => {
  const committed = {
    frozenRecordPrefixRef: 'blob-frozen-2',
    frozenRecordPrefixDigest: 'frozen-2',
    cutoffExclusive: 2,
    coveredPrefixDigest: 'prefix-2',
    sealRoot: 'seal-2',
    syntheticMessageId: 'synthetic-2',
  }
  const snapshot = stage2Snapshot([], committed)

  const plan = Projection.prefixForSnapshot(snapshot, '', 'BODY')
  assert.equal(plan.kind, 'ActivatePrefixEpoch')
  assert.equal(plan.activation.dropLeading, 2)
  assert.equal(plan.activation.syntheticMessageId, 'synthetic-2')

  const raw = Projection.prefixForSnapshot(stage2Snapshot([]), '', 'unused')
  assert.equal(raw.kind, 'KeepPhysicalPrefix')
})

// ── PROJ-008 step 3a: six intents — plan, order, conflict, render ──────────
//
// Domain skeleton only. Snapshot gains BlogFrames / TransportMessages /
// HostReanchor (consumer-driven). Planner is groupBy → reduce → sortBy rank;
// Renderer folds ordered intents over base wire messages.

// Domain 单源（PROJ-008 Step4/5）：生产常量来自 ProjectionConstants，不再手写字面量。
const REPAIR_INSTRUCTION =
  projectionConstants.RepairInstruction ??
  '# Protocol repair\n\nCall the blog tool exactly once with non-empty text. Do not answer in prose.'

const wireOf = (raw) => providerProjection.decodeMessageView(items(raw)).messages

const stage3Snapshot = (raw, extras = {}) =>
  projectionSnapshot.of({
    currentProjection: semanticView(raw),
    committedPrefix: extras.committed,
    blogFrames: extras.blogFrames ?? [],
    transportMessages: extras.transportMessages ?? [],
    hostReanchor: extras.hostReanchor,
  })

const planNames = (intents) => {
  const result = projectionAlgebra.plan(intents)
  assert.equal(result.ok, true, `expected Ok plan, got ${JSON.stringify(result)}`)
  return result.intents
}

// ── single-intent smoke (plan Ok + render shape) ───────────────────────────

test('WHAT[PROVIDER-PROJECTION-005] PROJ_008_step3a_InsertBlogFrames_smoke_inserts_assistant_frame_bodies', () => {
  const frames = [
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'd0', body: 'frame-0' }),
    projectionSnapshot.blogFrame({ kind: 'Squash', digest: 'd1', body: 'frame-1' }),
  ]
  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'delta' }] }]
  const snapshot = stage3Snapshot(raw, { blogFrames: frames })
  const intent = projectionIntent.insertBlogFrames({ RequestKind: 'normal' })

  assert.deepEqual(planNames([intent]), ['InsertBlogFrames'])

  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  // Step 3b: frames use CompanionPrompt.workingRecord ([[do_not_exec]] historic_frame).
  assert.equal(view.length >= 2, true, 'frames prepend or insert before delta')
  const assistantBodies = view.filter((m) => m.role === 'assistant').map((m) => m.parts[0]?.text)
  assert.equal(
    assistantBodies.some((t) => t.includes('frame-0')),
    true,
  )
  assert.equal(
    assistantBodies.some((t) => t.includes('frame-1')),
    true,
  )
})

test('WHAT[PROVIDER-PROJECTION-005] PROJ_008_step3a_InsertRepair_smoke_appends_user_repair_instruction', () => {
  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'hello' }] }]
  const snapshot = stage3Snapshot(raw)
  const intent = projectionIntent.insertRepair({ RequestKey: 'repair-key-1' })

  assert.deepEqual(planNames([intent]), ['InsertRepair'])

  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  const last = view[view.length - 1]
  assert.equal(last.role, 'user')
  assert.equal(last.parts[0]?.text, REPAIR_INSTRUCTION)
})

test('WHAT[PROVIDER-PROJECTION-005] PROJ_008_step3a_SuppressTransportOnly_smoke_drops_transport_message_ids', () => {
  const raw = [
    { info: { id: 'keep-1', role: 'user' }, parts: [{ type: 'text', text: 'keep' }] },
    { info: { id: 'drop-me', role: 'assistant' }, parts: [{ type: 'text', text: 'transport' }] },
    { info: { id: 'keep-2', role: 'user' }, parts: [{ type: 'text', text: 'also keep' }] },
  ]
  // The wire message has no id — Suppress removes by the parallel identity carried in
  // Snapshot.TransportMessages; the surface keeps this distinction explicit.
  // The pure wire view does not carry the raw Host identity side-channel for its
  // base messages. It therefore must never guess which row an ID names. Exact
  // suppression is owned by the Host write-back adapter and is covered in
  // provider-projection/tests/projection.test.mjs.
  const snapshot = stage3Snapshot(raw, { transportMessages: ['drop-me'] })
  const intent = projectionIntent.suppressTransportOnly

  assert.deepEqual(planNames([intent]), ['SuppressTransportOnly'])

  // The renderer accepts the base wire view plus the snapshot identity set.
  const emptySnap = stage3Snapshot(raw, { transportMessages: [] })
  const noOp = projectionAlgebra.renderMessagesWithIntents(emptySnap, wireOf(raw), [intent])
  assert.deepEqual(
    noOp.map((m) => m.parts[0]?.text),
    ['keep', 'transport', 'also keep'],
  )

  // Non-empty TransportMessages without an aligned Host identity channel is a
  // safe no-op here; the previous role/count heuristic could delete unrelated
  // assistant semantics merely because one transport ID existed.
  const suppressed = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  assert.deepEqual(
    suppressed.map((m) => m.parts[0]?.text),
    ['keep', 'transport', 'also keep'],
    'the semantic renderer must not guess an ID-to-row mapping it does not possess',
  )
})

test('WHAT[PROVIDER-PROJECTION-005] PROJ_008_step3a_ReanchorAfterCompaction_smoke_is_wire_noop', () => {
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'before' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'after' }] },
  ]
  const snapshot = stage3Snapshot(raw, {
    hostReanchor: projectionSnapshot.hostReanchor(),
  })
  const intent = projectionIntent.reanchorAfterCompaction

  assert.deepEqual(planNames([intent]), ['ReanchorAfterCompaction'])

  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  assert.deepEqual(
    view.map((m) => m.parts[0]?.text),
    ['before', 'after'],
    'reanchor does not rewrite wire bytes',
  )
})

// ── prefix mutual exclusion preserved ──────────────────────────────────────

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_prefix_mutual_exclusion_still_fails_closed', () => {
  const either = projectionAlgebra.plan([
    projectionIntent.keepPhysicalPrefix,
    projectionIntent.activatePrefixEpoch(activation()),
  ])
  assert.equal(either.ok, false)
  assert.equal(either.conflict, 'ConflictingPrefixSelection')
})

// ── canonical order: shuffled input → rank order ───────────────────────────

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_canonical_order_is_rank_sorted_regardless_of_input_order', () => {
  // Rank:
  // 0 Keep/Activate | 1 BlogFrames | 2 Repair | 3 Suppress | 4 Reanchor
  const shuffled = [
    projectionIntent.reanchorAfterCompaction,
    projectionIntent.suppressTransportOnly,
    projectionIntent.insertRepair({ RequestKey: 'k' }),
    projectionIntent.insertBlogFrames({ RequestKind: 'normal' }),
    projectionIntent.keepPhysicalPrefix,
  ]

  assert.deepEqual(planNames(shuffled), [
    'KeepPhysicalPrefix',
    'InsertBlogFrames',
    'InsertRepair',
    'SuppressTransportOnly',
    'ReanchorAfterCompaction',
  ])
})

// ── idempotent merges ──────────────────────────────────────────────────────

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_duplicate_Suppress_Reanchor_merge_to_one', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.suppressTransportOnly,
      projectionIntent.suppressTransportOnly,
    ]),
    ['SuppressTransportOnly'],
  )
  assert.deepEqual(
    planNames([
      projectionIntent.reanchorAfterCompaction,
      projectionIntent.reanchorAfterCompaction,
    ]),
    ['ReanchorAfterCompaction'],
  )
})

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_same_RequestKey_Repair_is_idempotent', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.insertRepair({ RequestKey: 'same' }),
      projectionIntent.insertRepair({ RequestKey: 'same' }),
    ]),
    ['InsertRepair'],
  )
})

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_identical_BlogFrames_intents_merge_to_one', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.insertBlogFrames({ RequestKind: 'normal' }),
      projectionIntent.insertBlogFrames({ RequestKind: 'normal' }),
    ]),
    ['InsertBlogFrames'],
  )
})

// ── conflicts ──────────────────────────────────────────────────────────────

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_conflicting_BlogFrames_payloads_fail_closed', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.insertBlogFrames({ RequestKind: 'normal' }),
    projectionIntent.insertBlogFrames({ RequestKind: 'squash' }),
  ])
  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingBlogFrames')
})

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_conflicting_Repair_keys_fail_closed', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.insertRepair({ RequestKey: 'a' }),
    projectionIntent.insertRepair({ RequestKey: 'b' }),
  ])
  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingRepair')
})

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_Activate_plus_Reanchor_is_ConflictingPrefixLifecycle', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.activatePrefixEpoch(activation()),
    projectionIntent.reanchorAfterCompaction,
  ])
  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingPrefixLifecycle')
})

// ── permutation independence ───────────────────────────────────────────────

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_plan_is_permutation_independent', () => {
  const a = projectionIntent.insertBlogFrames({ RequestKind: 'normal' })
  const b = projectionIntent.insertRepair({ RequestKey: 'k' })
  const c = projectionIntent.suppressTransportOnly
  const d = projectionIntent.keepPhysicalPrefix

  const orders = [
    [a, b, c, d],
    [d, c, b, a],
    [b, d, a, c],
    [c, a, d, b],
  ]
  const expected = [
    'KeepPhysicalPrefix',
    'InsertBlogFrames',
    'InsertRepair',
    'SuppressTransportOnly',
  ]

  for (const order of orders) {
    assert.deepEqual(planNames(order), expected)
  }
})

// ── multi-intent render: Activate then BlogFrames rank order ───────────────

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_Activate_then_BlogFrames_render_in_canonical_order', () => {
  const frames = [projectionSnapshot.blogFrame({ body: 'historic' })]
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] },
  ]
  const snapshot = stage3Snapshot(raw, { blogFrames: frames })
  // Shuffled: BlogFrames before Activate — plan must still Activate first.
  const intents = [
    projectionIntent.insertBlogFrames({ RequestKind: 'normal' }),
    projectionIntent.activatePrefixEpoch(activation()),
  ]
  assert.deepEqual(planNames(intents), ['ActivatePrefixEpoch', 'InsertBlogFrames'])

  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), intents)
  // After Activate: synthetic memory head + tail "third"; BlogFrames insert
  // historic assistant bodies (exact position may be after prefix — assert presence).
  assert.equal(view[0]?.parts[0]?.text, activation().memory)
  assert.equal(
    view.some((m) => m.role === 'assistant' && (m.parts[0]?.text === 'historic' || m.parts[0]?.text?.includes('historic'))),
    true,
  )
})

// ── KeepPhysicalPrefix + later intents coexist ─────────────────────────────

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_Keep_coexists_with_non_prefix_intents', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.insertRepair({ RequestKey: 'k' }),
      projectionIntent.keepPhysicalPrefix,
      projectionIntent.reanchorAfterCompaction,
    ]),
    ['KeepPhysicalPrefix', 'InsertRepair', 'ReanchorAfterCompaction'],
  )
})

// ── empty BlogFrames: InsertBlogFrames is plan-ok, render no-op ────────────

test('WHAT[PROVIDER-PROJECTION-005] PROJ_008_step3a_empty_BlogFrames_is_render_noop', () => {
  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'only' }] }]
  const snapshot = stage3Snapshot(raw, { blogFrames: [] })
  const intent = projectionIntent.insertBlogFrames({ RequestKind: 'normal' })
  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  assert.deepEqual(view.map((m) => m.parts[0]?.text), ['only'])
})

// ── PROJ-008 Step4/5/6: production-shape byte contracts (algebra half) ─────

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step6_Reanchor_with_Keep_is_wire_noop_and_plan_ok', () => {
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'physical' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'history' }] },
  ]
  const snapshot = stage3Snapshot(raw, {
    committedPrefix: undefined,
    hostReanchor: projectionSnapshot.hostReanchor({ previous: '0', next: '1', run: 'compact-x' }),
  })
  const intents = [projectionIntent.keepPhysicalPrefix, projectionIntent.reanchorAfterCompaction]
  assert.deepEqual(planNames(intents), ['KeepPhysicalPrefix', 'ReanchorAfterCompaction'])

  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), intents)
  assert.deepEqual(
    view.map((m) => m.parts[0]?.text),
    ['physical', 'history'],
    'reanchor projection does not rewrite wire bytes (CommittedPrefix=None → Keep)',
  )
})

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step6_Reanchor_conflicts_with_Activate_fail_closed', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.activatePrefixEpoch(activation()),
    projectionIntent.reanchorAfterCompaction,
  ])
  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingPrefixLifecycle')
})

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step6_Reanchor_is_idempotent_in_plan', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.reanchorAfterCompaction,
      projectionIntent.reanchorAfterCompaction,
    ]),
    ['ReanchorAfterCompaction'],
  )
})

test('WHAT[PROVIDER-PROJECTION-006] PROJ_008_step3a_two_KeepPhysicalPrefix_merge_idempotently', () => {
  assert.deepEqual(
    planNames([projectionIntent.keepPhysicalPrefix, projectionIntent.keepPhysicalPrefix]),
    ['KeepPhysicalPrefix'],
  )
})

// ── PROVIDER-PROJECTION-007: DSL 不负责生命周期 ────────────────────────────
//
// 投影管线只负责 不可变快照 → 确定性 provider-visible projection。结构性
// 守卫（与 ARCH_011 no-parser 同手法）：四个投影模块的 public 面不得出现
// 生命周期动词——不启动/等待 Agent、不执行工具、不写 Journal、不恢复
// Prompt、不管理 ProviderRunIdentity、不推进生命周期状态。

test('WHAT[PROVIDER-PROJECTION-007] PROJ_007_projection_pipeline_owns_no_lifecycle_verbs', () => {
  // The registered surface exposes only pure planning/rendering operations.
  assert.equal(typeof Projection.plan, 'function')
  assert.equal(typeof Projection.renderMessages, 'function')
  assert.deepEqual(Projection.pureContractNames, [
    'plan',
    'renderPrefix',
    'renderMessages',
    'renderMessagesWithHostIds',
    'renderWire',
    'renderSemantic',
    'isAppendOnlyPrefix',
    'sealDigest',
    'toolResultDigests',
  ])

  const lifecycleVerbs = ['start', 'stop', 'wait', 'join', 'spawn', 'resume', 'abort', 'update', 'advance', 'execute']
  for (const verb of lifecycleVerbs) {
    assert.equal(Projection[verb], undefined, `projection must not expose a lifecycle verb: ${verb}`)
  }
})

// ── PROVIDER-PROJECTION-011: semantic equality ≠ wire equality；digest 从 Semantic 算 ──
//
// Semantic 去 ID（跨会话可比较）；Wire 含 ID、字节相等。同语义跨 ID 的对话
// 必须产出同一 semantic 投影（canonical digest 的唯一输入），而 wire bytes
// 必须不同——两者相等键不同，混用必错。

test('WHAT[PROVIDER-PROJECTION-011] PROJ_003_semantic_equality_ignores_wire_ids_but_wire_bytes_differ', () => {
  const build = (callId) =>
    providerProjection.decodeMessageView(items([
      { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
      { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'tool-call', callID: callId, name: 'read', args: '{"p":1}' }] },
    ]))
  const withCallA = build('call-a')
  const withCallB = build('call-b')

  const semanticA = providerProjection.toSemantic(withCallA)
  const semanticB = providerProjection.toSemantic(withCallB)

  // 语义相等：不同 wire id 的同一对话产出同一 semantic 投影（digest 唯一输入）。
  assert.equal(providerProjection.renderSemantic(semanticA), providerProjection.renderSemantic(semanticB))
  assert.equal(providerProjection.semanticallyEqual(semanticA, semanticB), true)

  // wire 不等：id 属于 wire 层，字节形状不同不得进入 digest 判定。
  assert.notEqual(providerProjection.renderWire(withCallA), providerProjection.renderWire(withCallB))
})


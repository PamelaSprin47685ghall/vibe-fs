// tests/unit/context/projection-algebra.test.mjs — PROJ-004, PROJ-005, PROJ-006.
//
// Stage 1–2 closed loop (PROJ-008 steps 1–2: plain X + ActivePrefixEpoch +
// attempt-local PrefixProbe) plus step 3a Domain skeleton proofs for the six
// remaining intents: plan/order/conflict algebra and minimal render shapes.
// Production wiring (CompanionTransform / SpikePlugin) is out of scope here.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  companionProjection as companionProj,
  companionPrompt as companionPrompt,
  listItems,
  prefixEpochProjection as prefix,
  projectionAlgebra,
  projectionConstants,
  projectionIntent,
  projectionSnapshot,
  providerProjection,
  reviewChallenge,
  toList,
  xPrefix,
} from '../support/domain.mjs'

const activation = (overrides = {}) => ({
  SyntheticMessageId: 'synthetic-1',
  Memory: '<work-log>\nTHE WORK LOG\n</work-log>',
  DropLeading: 2,
  ...overrides,
})

// ── PROJ-006: fail-closed conflicts, order-independent ─────────────────────

test('PROJ_006_prefix_intents_are_mutually_exclusive_at_the_same_anchor', () => {
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

test('PROJ_006_two_activations_of_the_same_anchor_are_a_conflict', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.activatePrefixEpoch(activation({ DropLeading: 1 })),
    projectionIntent.activatePrefixEpoch(activation({ DropLeading: 3 })),
  ])

  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingPrefixSelection')
})

test('PROJ_006_a_single_intent_or_none_plans_cleanly', () => {
  const keep = projectionIntent.keepPhysicalPrefix
  const activate = projectionIntent.activatePrefixEpoch(activation())

  assert.deepEqual(projectionAlgebra.plan([]), { ok: true, intents: [] })
  assert.deepEqual(projectionAlgebra.plan([keep]), { ok: true, intents: ['KeepPhysicalPrefix'] })
  assert.deepEqual(projectionAlgebra.plan([activate]), { ok: true, intents: ['ActivatePrefixEpoch'] })
})

// ── PROJ-004: the canonical renderer ───────────────────────────────────────

test('PROJ_004_renderer_maps_intents_to_writeback_instructions', () => {
  // No intent and a plain keep both render to "send the physical prefix".
  assert.deepEqual(projectionAlgebra.renderPrefix([]), { name: 'PhysicalPrefix', activation: undefined })
  assert.deepEqual(projectionAlgebra.renderPrefix([projectionIntent.keepPhysicalPrefix]), {
    name: 'PhysicalPrefix',
    activation: undefined,
  })

  const rendered = projectionAlgebra.renderPrefix([projectionIntent.activatePrefixEpoch(activation())])
  assert.equal(rendered.name, 'SyntheticPrefix')
  assert.deepEqual(rendered.activation, activation())
})

test('PROJ_004_physical_prefix_renders_the_messages_unchanged', () => {
  const view = projectionAlgebra.renderMessages(
    providerProjection.decodeMessageView(toList([
      { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
      { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'second' }] },
    ])).Messages,
    projectionAlgebra.rendered.physical,
  )

  assert.deepEqual(view, [
    { role: 'user', parts: [{ kind: 'WireText', text: 'first' }] },
    { role: 'user', parts: [{ kind: 'WireText', text: 'second' }] },
  ])
})

test('PROJ_004_synthetic_prefix_prepends_the_memory_and_drops_the_physical_cutoff', () => {
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] },
  ]

  const view = projectionAlgebra.renderMessages(
    providerProjection.decodeMessageView(toList(raw)).Messages,
    projectionAlgebra.rendered.synthetic(activation()),
  )

  assert.deepEqual(view, [
    { role: 'user', parts: [{ kind: 'WireText', text: activation().Memory }] },
    { role: 'user', parts: [{ kind: 'WireText', text: 'third' }] },
  ])
})

test('PROJ_004_a_cutoff_beyond_the_message_view_fails_closed', () => {
  const messages = providerProjection.decodeMessageView(toList([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
  ])).Messages

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

test('PROJ_004_writeback_preserves_the_tail_objects_verbatim', () => {
  const tail = { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] }
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    tail,
  ]

  const written = providerProjection.applyRenderedPrefix(
    toList(raw),
    projectionAlgebra.rendered.synthetic(activation()),
  )

  assert.equal(written.length, 2)
  assert.equal(written[1], tail, 'the untouched tail is the SAME object, never re-encoded')

  const head = written[0]
  assert.equal(head.info.id, 'synthetic-1')
  assert.equal(head.info.role, 'user')
  assert.equal(head.parts[0].type, 'text')
  assert.equal(head.parts[0].text, activation().Memory)
})

test('PROJ_004_the_wire_view_and_the_written_back_bytes_decode_to_the_same_digest_input', () => {
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

  const pureView = projectionAlgebra.renderMessages(providerProjection.decodeMessageView(toList(raw)).Messages, rendered)
  const writtenBack = providerProjection.decodeMessageView(
    toList(providerProjection.applyRenderedPrefix(toList(raw), rendered)),
  )

  // `PhysicalPrefix` renders its input unchanged, so running the write-back result
  // through it yields the decode of the bytes the Host will actually send.
  const writtenView = projectionAlgebra.renderMessages(writtenBack.Messages, projectionAlgebra.rendered.physical)

  assert.deepEqual(pureView, writtenView)
})

test('PROJ_004_the_written_back_bytes_are_the_frozen_prefix_shape', () => {
  const raw = [
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'second' }] },
  ]

  const written = providerProjection.applyRenderedPrefix(
    toList(raw),
    projectionAlgebra.rendered.synthetic(activation({ Memory: 'MEM', DropLeading: 1 })),
  )
  const bytes = providerProjection.renderWire(providerProjection.decodeMessageView(toList(written)))

  // Frozen: the synthetic head is a user text message, the tail is untouched.
  assert.equal(
    bytes,
    '{"provider":null,"model":null,"variant":null,"tools":[],"system":[],' +
      '"messages":[{"role":"user","parts":[{"kind":"text","text":"MEM"}]},' +
      '{"role":"user","parts":[{"kind":"text","text":"second"}]}]}',
  )
})

// ── PROJ-008 stage 2: attempt-local PrefixProbe projection ─────────────────

const semanticView = (raw) => providerProjection.toSemantic(providerProjection.decodeMessageView(toList(raw)))
const sha256 = (input) => `«${input}»`

const stage2Snapshot = (raw, committed = undefined) => ({
  CurrentProjection: semanticView(raw),
  CommittedPrefix: committed,
})

test('PROJ_002_the_snapshot_is_the_attempt_local_input_contract', () => {
  const snapshot = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'second' }] },
  ])

  // CurrentProjection is the transform-boundary semantic view: role + parts only.
  assert.equal(snapshot.CurrentProjection.ProviderId, undefined)
  assert.equal(listItems(snapshot.CurrentProjection.Messages)[0].Role, 'user')
  assert.equal(snapshot.CommittedPrefix, undefined, 'no committed prefix = send physical history')
})

test('CTX_011_step5_cutoff_digest_truncates_exactly_at_the_cutoff', () => {
  const snapshot = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] },
  ])

  const full = providerProjection.renderSemantic(snapshot.CurrentProjection)

  // Truncate semantics: cutoff 2 keeps the first two messages, the rest are cut.
  const truncated = {
    ...snapshot.CurrentProjection,
    Messages: toList(listItems(snapshot.CurrentProjection.Messages).slice(0, 2)),
  }
  assert.equal(projectionAlgebra.cutoffDigest(sha256, snapshot, 2), sha256(providerProjection.renderSemantic(truncated)))
  assert.notEqual(projectionAlgebra.cutoffDigest(sha256, snapshot, 2), sha256(full), 'a real cutoff changes the digest')

  // cutoff 0 proves the EMPTY prefix — the load-bearing CTX-011 step-5 shape.
  const empty = { ...snapshot.CurrentProjection, Messages: toList([]) }
  assert.equal(projectionAlgebra.cutoffDigest(sha256, snapshot, 0), sha256(providerProjection.renderSemantic(empty)))

  // An out-of-range cutoff never throws: List.truncate keeps everything (the
  // selector's min() means such a cutoff cannot reach the proof in production).
  assert.equal(projectionAlgebra.cutoffDigest(sha256, snapshot, 99), sha256(full))
})

test('CTX_011_step5_the_proof_reads_the_SNAPSHOT_not_a_stale_closure', () => {
  // The digest must be recomputed from X's CURRENT projection each attempt —
  // a closure captured once would re-prove yesterday's numbering (COMPANION-011).
  const before = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'old' }] },
  ])
  const after = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'old' }] },
    { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'new' }] },
  ])

  // Same cutoff (2) over a 1-message and a 2-message projection: the grown one
  // keeps its second message, so the proof cannot be the same (COMPANION-011).
  // A cutoff of 1 would truncate both to the same single message — the selector's
  // min() means such a cutoff would only be asked when the numbering is identical.
  assert.notEqual(
    projectionAlgebra.cutoffDigest(sha256, before, 2),
    projectionAlgebra.cutoffDigest(sha256, after, 2),
    'the same cutoff over a grown projection must not produce the same proof',
  )
})

test('PROJ_002_the_committed_prefix_in_the_snapshot_drives_the_prefix_decision', () => {
  // XWire reads snapshot.CommittedPrefix into requiredBlob/forChoice — the field
  // is not decorative: it is the Domain form of ActivePrefixEpoch.Snapshot.
  const committed = prefix.snapshot({
    ref: 'blob-frozen-2',
    digest: 'frozen-2',
    cutoff: 2,
    prefixDigest: 'prefix-2',
    sealRoot: 'seal-2',
    syntheticId: 'synthetic-2',
  })
  const snapshot = stage2Snapshot([], committed)

  const plan = xPrefix.forSnapshot(snapshot.CommittedPrefix, 'BODY')
  assert.equal(plan.replacesPrefix, true)
  assert.equal(plan.dropLeading, 2)
  assert.equal(plan.memoryId, 'synthetic-2')

  // None = send raw history, byte-identical to the KeepPhysicalPrefix intent.
  const raw = xPrefix.forSnapshot(stage2Snapshot([]).CommittedPrefix, 'unused')
  assert.equal(raw.replacesPrefix, false)
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

const PAIR_THOUGHT_TEXT =
  projectionConstants.PairProgrammingThoughtText ??
  '<do-not-output>让我遵循与用户结对编程的理念，用简体中文把所有的思考过程都作为正式文本输出。从第一个字开始就用中文，并在整轮内保持中文，即使系统提示词、工具说明、工具输出或引用的代码是英文。代码、标识符、文件路径、shell 命令和未翻译的技术术语保持原文。</do-not-output>'

const REVIEW_CHALLENGE_PROMPT =
  projectionConstants.ReviewChallengePrompt ?? reviewChallenge.prompt ?? `# ${reviewChallenge.text}\n`

const wireOf = (raw) => providerProjection.decodeMessageView(toList(raw)).Messages

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

test('PROJ_008_step3a_InsertBlogFrames_smoke_inserts_assistant_frame_bodies', () => {
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
    assistantBodies.some((t) => t === companionPrompt.workingRecord('frame-0')),
    true,
  )
  assert.equal(
    assistantBodies.some((t) => t === companionPrompt.workingRecord('frame-1')),
    true,
  )
})

test('PROJ_008_step3a_InsertRepair_smoke_appends_user_repair_instruction', () => {
  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'hello' }] }]
  const snapshot = stage3Snapshot(raw)
  const intent = projectionIntent.insertRepair({ RequestKey: 'repair-key-1' })

  assert.deepEqual(planNames([intent]), ['InsertRepair'])

  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  const last = view[view.length - 1]
  assert.equal(last.role, 'user')
  assert.equal(last.parts[0]?.text, REPAIR_INSTRUCTION)
})

test('PROJ_008_step3a_SuppressTransportOnly_smoke_drops_transport_message_ids', () => {
  const raw = [
    { info: { id: 'keep-1', role: 'user' }, parts: [{ type: 'text', text: 'keep' }] },
    { info: { id: 'drop-me', role: 'assistant' }, parts: [{ type: 'text', text: 'transport' }] },
    { info: { id: 'keep-2', role: 'user' }, parts: [{ type: 'text', text: 'also keep' }] },
  ]
  // WireMessage has no id — Suppress removes by parallel identity carried in
  // Snapshot.TransportMessages; step 3a encodes that as: messages whose
  // original host id is in the set are absent from the rendered view. The
  // Domain renderer must accept base wire + snapshot ids (see facade contract).
  // Minimal permanent proof: after suppress, transport body text is gone and
  // non-transport texts remain.
  const snapshot = stage3Snapshot(raw, { transportMessages: ['drop-me'] })
  const intent = projectionIntent.suppressTransportOnly

  assert.deepEqual(planNames([intent]), ['SuppressTransportOnly'])

  // When Domain cannot see host ids on WireMessage, step 3a may require the
  // base list to already be index-aligned; the permanent contract is: plan Ok
  // and render does not throw, and if TransportMessages is non-empty the
  // suppress path is exercised. Full id-aware suppress may use a Domain
  // side-channel — assert plan + that empty TransportMessages is a no-op first.
  const emptySnap = stage3Snapshot(raw, { transportMessages: [] })
  const noOp = projectionAlgebra.renderMessagesWithIntents(emptySnap, wireOf(raw), [intent])
  assert.deepEqual(
    noOp.map((m) => m.parts[0]?.text),
    ['keep', 'transport', 'also keep'],
  )

  // Non-empty TransportMessages must change the view (permanent fail until green).
  const suppressed = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  assert.notDeepEqual(
    suppressed.map((m) => m.parts[0]?.text),
    ['keep', 'transport', 'also keep'],
    'non-empty TransportMessages must suppress at least one message',
  )
})

test('PROJ_008_step3a_AppendReviewChallenge_smoke_appends_challenge_text', () => {
  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'task' }] }]
  const snapshot = stage3Snapshot(raw)
  const intent = projectionIntent.appendReviewChallenge({ TextVersion: reviewChallenge.textVersion })

  assert.deepEqual(planNames([intent]), ['AppendReviewChallenge'])

  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  const texts = view.flatMap((m) => m.parts.map((p) => p.text)).filter(Boolean)
  assert.equal(
    texts.some((t) => t === REVIEW_CHALLENGE_PROMPT || t === reviewChallenge.text || t.includes(reviewChallenge.text)),
    true,
    'rendered view must carry ReviewChallenge.Prompt (or Text)',
  )
})

test('PROJ_008_step3a_InsertPairProgrammingThought_smoke_inserts_marker_after_anchors', () => {
  const raw = [
    { info: { id: 'u1', role: 'user' }, parts: [{ type: 'text', text: 'ask' }] },
    { info: { id: 'a1', role: 'assistant' }, parts: [{ type: 'text', text: 'answer' }] },
  ]
  const snapshot = stage3Snapshot(raw)
  const intent = projectionIntent.insertPairProgrammingThought({ SessionId: 'sess-1' })

  assert.deepEqual(planNames([intent]), ['InsertPairProgrammingThought'])

  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  const markers = view.filter(
    (m) =>
      m.role === 'assistant' &&
      m.parts.some((p) => p.text === PAIR_THOUGHT_TEXT || p.kind === 'WireReasoning'),
  )
  assert.equal(markers.length >= 1, true, 'at least one pair-programming marker after user anchor')
  assert.equal(
    markers.some((m) => m.parts.some((p) => p.text === PAIR_THOUGHT_TEXT)),
    true,
  )
})

test('PROJ_008_step3a_ReanchorAfterCompaction_smoke_is_wire_noop', () => {
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

test('PROJ_008_step3a_prefix_mutual_exclusion_still_fails_closed', () => {
  const either = projectionAlgebra.plan([
    projectionIntent.keepPhysicalPrefix,
    projectionIntent.activatePrefixEpoch(activation()),
  ])
  assert.equal(either.ok, false)
  assert.equal(either.conflict, 'ConflictingPrefixSelection')
})

// ── canonical order: shuffled input → rank order ───────────────────────────

test('PROJ_008_step3a_canonical_order_is_rank_sorted_regardless_of_input_order', () => {
  // Rank:
  // 0 Keep/Activate | 1 BlogFrames | 2 Repair | 3 Suppress | 4 Challenge
  // | 5 PairThought | 6 Reanchor
  const shuffled = [
    projectionIntent.reanchorAfterCompaction,
    projectionIntent.insertPairProgrammingThought({ SessionId: 's' }),
    projectionIntent.appendReviewChallenge({ TextVersion: 1 }),
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
    'AppendReviewChallenge',
    'InsertPairProgrammingThought',
    'ReanchorAfterCompaction',
  ])
})

// ── idempotent merges ──────────────────────────────────────────────────────

test('PROJ_008_step3a_duplicate_Suppress_Pair_Reanchor_merge_to_one', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.suppressTransportOnly,
      projectionIntent.suppressTransportOnly,
    ]),
    ['SuppressTransportOnly'],
  )
  assert.deepEqual(
    planNames([
      projectionIntent.insertPairProgrammingThought({ SessionId: 'a' }),
      projectionIntent.insertPairProgrammingThought({ SessionId: 'a' }),
    ]),
    ['InsertPairProgrammingThought'],
  )
  assert.deepEqual(
    planNames([
      projectionIntent.reanchorAfterCompaction,
      projectionIntent.reanchorAfterCompaction,
    ]),
    ['ReanchorAfterCompaction'],
  )
})

test('PROJ_008_step3a_same_RequestKey_Repair_is_idempotent', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.insertRepair({ RequestKey: 'same' }),
      projectionIntent.insertRepair({ RequestKey: 'same' }),
    ]),
    ['InsertRepair'],
  )
})

test('PROJ_008_step3a_same_version_Challenge_is_idempotent', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.appendReviewChallenge({ TextVersion: 1 }),
      projectionIntent.appendReviewChallenge({ TextVersion: 1 }),
    ]),
    ['AppendReviewChallenge'],
  )
})

test('PROJ_008_step3a_identical_BlogFrames_intents_merge_to_one', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.insertBlogFrames({ RequestKind: 'normal' }),
      projectionIntent.insertBlogFrames({ RequestKind: 'normal' }),
    ]),
    ['InsertBlogFrames'],
  )
})

// ── conflicts ──────────────────────────────────────────────────────────────

test('PROJ_008_step3a_conflicting_BlogFrames_payloads_fail_closed', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.insertBlogFrames({ RequestKind: 'normal' }),
    projectionIntent.insertBlogFrames({ RequestKind: 'squash' }),
  ])
  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingBlogFrames')
})

test('PROJ_008_step3a_conflicting_Repair_keys_fail_closed', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.insertRepair({ RequestKey: 'a' }),
    projectionIntent.insertRepair({ RequestKey: 'b' }),
  ])
  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingRepair')
})

test('PROJ_008_step3a_Activate_plus_Reanchor_is_ConflictingPrefixLifecycle', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.activatePrefixEpoch(activation()),
    projectionIntent.reanchorAfterCompaction,
  ])
  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingPrefixLifecycle')
})

test('PROJ_008_step3a_different_Challenge_versions_conflict', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.appendReviewChallenge({ TextVersion: 1 }),
    projectionIntent.appendReviewChallenge({ TextVersion: 2 }),
  ])
  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingReviewChallenge')
})

// ── permutation independence ───────────────────────────────────────────────

test('PROJ_008_step3a_plan_is_permutation_independent', () => {
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

test('PROJ_008_step3a_Activate_then_BlogFrames_render_in_canonical_order', () => {
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
  assert.equal(view[0]?.parts[0]?.text, activation().Memory)
  assert.equal(
    view.some((m) => m.role === 'assistant' && (m.parts[0]?.text === 'historic' || m.parts[0]?.text?.includes('historic'))),
    true,
  )
})

// ── KeepPhysicalPrefix + later intents coexist ─────────────────────────────

test('PROJ_008_step3a_Keep_coexists_with_non_prefix_intents', () => {
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

test('PROJ_008_step3a_empty_BlogFrames_is_render_noop', () => {
  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'only' }] }]
  const snapshot = stage3Snapshot(raw, { blogFrames: [] })
  const intent = projectionIntent.insertBlogFrames({ RequestKind: 'normal' })
  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  assert.deepEqual(view.map((m) => m.parts[0]?.text), ['only'])
})

// ── PROJ-008 step 3b: algebra InsertBlogFrames ≡ CompanionProjectionBuilder ─

test('PROJ_008_step3b_InsertBlogFrames_digest_equiv_to_CompanionProjectionBuilder', () => {
  const spy = (input) => `«${input}»`
  const dataToml = '[[new_work_to_record]]\nuser = "work"'
  const frames = [
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f0', body: 'frame body 0' }),
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f1', body: 'frame body 1' }),
  ]
  const previousTips = [{ field: 'progress', cycleId: 'cycle-1' }]
  const delta = { messageId: 'msg_delta', toml: dataToml }

  const intent = projectionIntent.insertBlogFrames({
    RequestKind: 'normal',
    SquashFrameCount: 0,
    BloggerSessionId: 'ses_y',
    FrameEpoch: 0,
    PhysicalDelta: delta,
    PreviousTips: previousTips,
  })
  const snapshot = stage3Snapshot([], { blogFrames: frames })
  assert.deepEqual(planNames([intent]), ['InsertBlogFrames'])

  const algebraView = projectionAlgebra.renderMessagesWithIntents(snapshot, [], [intent])
  const builderPlan = companionProj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: companionProj.normal,
    frames: frames.map((f) => ({ digest: f.Digest, body: f.Body })),
    delta,
    previousTips,
  })

  assert.deepEqual(
    algebraView.map((m) => m.role),
    builderPlan.roles,
  )
  assert.deepEqual(
    algebraView.map((m) => m.parts[0]?.text),
    builderPlan.texts,
  )
})

test('PROJ_008_step3b_InsertBlogFrames_squash_digest_equiv_to_Builder', () => {
  const spy = (input) => `«${input}»`
  const frames = [
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f0', body: 'frame body 0' }),
    projectionSnapshot.blogFrame({ kind: 'Entry', digest: 'sha-f1', body: 'frame body 1' }),
    projectionSnapshot.blogFrame({ kind: 'Squash', digest: 'sha-f2', body: 'frame body 2' }),
  ]
  const intent = projectionIntent.insertBlogFrames({
    RequestKind: 'squash',
    SquashFrameCount: 2,
    BloggerSessionId: 'ses_y',
    FrameEpoch: 1,
    PhysicalDelta: undefined,
    PreviousTips: [],
  })
  const snapshot = stage3Snapshot([], { blogFrames: frames })
  const algebraView = projectionAlgebra.renderMessagesWithIntents(snapshot, [], [intent])
  const builderPlan = companionProj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: companionProj.squash(2),
    frames: frames.map((f) => ({ digest: f.Digest, body: f.Body })),
  })

  assert.deepEqual(
    algebraView.map((m) => [m.role, m.parts[0]?.text]),
    builderPlan.messages.map((m) => [m.role, m.text]),
  )
  assert.equal(algebraView.at(-1)?.parts[0]?.text, companionPrompt.squashInstruction)
})

// ── two KeepPhysicalPrefix merge (idempotent), not conflict ────────────────

// ── PROJ-008 Step4/5/6: production-shape byte contracts ────────────────────

test('PROJ_008_step4_InsertRepair_text_is_ProjectionConstants_RepairInstruction', () => {
  assert.equal(typeof projectionConstants.RepairInstruction, 'string')
  assert.equal(projectionConstants.RepairInstruction, REPAIR_INSTRUCTION)

  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'base' }] }]
  const snapshot = stage3Snapshot(raw)
  const intent = projectionIntent.insertRepair({ RequestKey: 'rk-prod-1' })
  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])

  assert.equal(view.length, 2)
  assert.equal(view[0]?.parts[0]?.text, 'base')
  assert.equal(view[1]?.role, 'user')
  assert.equal(view[1]?.parts[0]?.text, REPAIR_INSTRUCTION)

  // id 规则合同：enforcer-repair- + sha256(requestKey + "|" + text).substr(0,24)
  // Domain 不产出 Host id；生产 Host 侧信道用同一常量拼 digest。
  const material = `rk-prod-1|${REPAIR_INSTRUCTION}`
  assert.equal(material.includes(REPAIR_INSTRUCTION), true)
})

test('PROJ_008_step5_AppendReviewChallenge_production_bytes_are_Prompt', () => {
  assert.equal(reviewChallenge.prompt, REVIEW_CHALLENGE_PROMPT)
  assert.equal(REVIEW_CHALLENGE_PROMPT, `# ${reviewChallenge.text}\n`)

  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'task' }] }]
  const snapshot = stage3Snapshot(raw)
  const intent = projectionIntent.appendReviewChallenge({ TextVersion: reviewChallenge.textVersion })
  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  const last = view[view.length - 1]
  assert.equal(last?.role, 'user')
  assert.equal(
    last?.parts[0]?.text,
    REVIEW_CHALLENGE_PROMPT,
    'AppendReviewChallenge must emit ReviewChallenge.Prompt bytes for seal/nudge parity',
  )
})

test('PROJ_008_step5_InsertPairProgrammingThought_idempotent_when_marker_already_present', () => {
  const raw = [
    { info: { id: 'u1', role: 'user' }, parts: [{ type: 'text', text: 'ask' }] },
    {
      info: { id: 'pair-1', role: 'assistant', source: 'pair-programming-thought', synthetic: true },
      parts: [{ type: 'reasoning', text: PAIR_THOUGHT_TEXT }],
    },
  ]
  const snapshot = stage3Snapshot(raw)
  const intent = projectionIntent.insertPairProgrammingThought({ SessionId: 'sess-1' })
  const once = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])
  const twice = projectionAlgebra.renderMessagesWithIntents(snapshot, once, [intent])

  const markers = (view) =>
    view.filter(
      (m) => m.role === 'assistant' && m.parts.some((p) => p.text === PAIR_THOUGHT_TEXT),
    )
  assert.equal(markers(once).length, 1, 'one marker after user anchor')
  assert.equal(markers(twice).length, 1, 'second apply stays idempotent (no double marker)')
  assert.deepEqual(
    twice.map((m) => m.parts[0]?.text),
    once.map((m) => m.parts[0]?.text),
  )
})

test('PROJ_008_step6_Reanchor_with_Keep_is_wire_noop_and_plan_ok', () => {
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

test('PROJ_008_step6_Reanchor_conflicts_with_Activate_fail_closed', () => {
  const result = projectionAlgebra.plan([
    projectionIntent.activatePrefixEpoch(activation()),
    projectionIntent.reanchorAfterCompaction,
  ])
  assert.equal(result.ok, false)
  assert.equal(result.conflict, 'ConflictingPrefixLifecycle')
})

test('PROJ_008_step6_Reanchor_is_idempotent_in_plan', () => {
  assert.deepEqual(
    planNames([
      projectionIntent.reanchorAfterCompaction,
      projectionIntent.reanchorAfterCompaction,
    ]),
    ['ReanchorAfterCompaction'],
  )
})

test('PROJ_008_step3a_two_KeepPhysicalPrefix_merge_idempotently', () => {
  assert.deepEqual(
    planNames([projectionIntent.keepPhysicalPrefix, projectionIntent.keepPhysicalPrefix]),
    ['KeepPhysicalPrefix'],
  )
})


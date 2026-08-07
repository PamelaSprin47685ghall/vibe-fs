// tests/unit/context/projection-algebra.test.mjs — PROJ-004, PROJ-005, PROJ-006.
//
// The projection DSL's stage-1 closed loop (PROJ-008 migration order step 1: plain X
// + ActivePrefixEpoch projection): the intent DU, the pure planner's fail-closed
// conflict rules, the canonical renderer, and the byte-equality of the write-back
// path with the wire view a digest is computed from.

import assert from 'node:assert/strict'
import test from 'node:test'
import { projectionAlgebra, projectionIntent, providerProjection, toList } from '../support/domain.mjs'

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

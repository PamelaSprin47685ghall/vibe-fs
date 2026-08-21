// Split from tests/unit/context/projection-algebra.test.mjs (cutover Wave 2a); owner: prefix-stability.
//
// CTX_011 step-5 digest proofs: the cutoff digest truncates exactly at the
// cutoff (cutoff 0 = the EMPTY prefix), and the proof reads the SNAPSHOT not a
// stale closure (COMPANION-011).

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

import * as providerCodec from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'
import * as providerProjection from '../../../dist/Participant/Provider/Projection/Surface.js'

const semanticView = (raw) => providerProjection.semanticProjection(providerCodec.decodeMessageView(raw).messages)
const sha256 = (input) => `«${input}»`

const stage2Snapshot = (raw, committed = null) => ({
  currentProjection: semanticView(raw),
  committedPrefix: committed,
})

const cutoffDigest = (sha, snapshot, cutoff) => sha(
  providerProjection.renderSemantic({
    ...snapshot.currentProjection,
    messages: snapshot.currentProjection.messages.slice(0, cutoff),
  }),
)

test('WHAT[PREFIX-STABILITY-009] CTX_011_step5_cutoff_digest_truncates_exactly_at_the_cutoff', () => {
  const snapshot = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] },
  ])

  const full = providerProjection.renderSemantic(snapshot.currentProjection)
  const truncated = {
    ...snapshot.currentProjection,
    messages: snapshot.currentProjection.messages.slice(0, 2),
  }
  assert.equal(cutoffDigest(sha256, snapshot, 2), sha256(providerProjection.renderSemantic(truncated)))
  assert.notEqual(cutoffDigest(sha256, snapshot, 2), sha256(full), 'a real cutoff changes the digest')

  // cutoff 0 proves the EMPTY prefix — the load-bearing CTX-011 step-5 shape.
  const empty = { ...snapshot.currentProjection, messages: [] }
  assert.equal(cutoffDigest(sha256, snapshot, 0), sha256(providerProjection.renderSemantic(empty)))

  // An out-of-range cutoff keeps every message; the selector clamps before this
  // proof is requested, but the proof itself remains total.
  assert.equal(cutoffDigest(sha256, snapshot, 99), sha256(full))
})

test('WHAT[PREFIX-STABILITY-009] CTX_011_step5_the_proof_reads_the_SNAPSHOT_not_a_stale_closure', () => {
  // The digest must be recomputed from X's CURRENT projection each attempt — a
  // closure captured once would re-prove yesterday's numbering.
  const before = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'old' }] },
  ])
  const after = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'old' }] },
    { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'new' }] },
  ])

  // Same cutoff over a 1-message and a 2-message projection: the grown one keeps
  // its second message, so the proof cannot be the same.
  assert.notEqual(
    cutoffDigest(sha256, before, 2),
    cutoffDigest(sha256, after, 2),
    'the same cutoff over a grown projection must not produce the same proof',
  )
})

test('WHAT[PREFIX-STABILITY-009] prefix_proof_and_writeback_use_canonical_XTrace_not_request_local_message_positions', () => {
  const wireSource = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Context/Prefix/Wire.fs'),
    'utf8',
  )

  assert.match(wireSource, /XTraceMaterialization\.currentProjection/)
  assert.match(wireSource, /XTraceProjection\.tryTurnOfHostMessageId/)
  assert.match(wireSource, /XTraceProjection\.hostMessageIdsBeforeTurn/)
  assert.match(wireSource, /applyRenderedPrefixByHostIds/)
  assert.doesNotMatch(
    wireSource,
    /ProviderWireCapture\.decodeMessageView\(rawMessages\)[\s\S]{0,500}ProjectionRenderer\.cutoffDigest/,
    'step-5 proof must not hash the mutable request presentation',
  )
})

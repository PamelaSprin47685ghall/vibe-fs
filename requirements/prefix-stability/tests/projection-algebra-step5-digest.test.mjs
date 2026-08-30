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
import * as XWireSurface from '../../../dist/Context/Prefix/XWireSurface.js'

const semanticView = (raw) => providerProjection.semanticProjection(providerCodec.decodeMessageView(raw).messages)
const stage2Snapshot = (raw, committed = null) => ({
  currentProjection: semanticView(raw),
  committedPrefix: committed,
})

test('WHAT[PREFIX-STABILITY-009] CTX_011_step5_cutoff_digest_truncates_exactly_at_the_cutoff', () => {
  const snapshot = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] },
  ])

  const truncated = {
    ...snapshot.currentProjection,
    messages: snapshot.currentProjection.messages.slice(0, 2),
  }
  assert.equal(
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 2),
    XWireSurface.coveredPrefixDigest(truncated, 2),
  )
  assert.notEqual(
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 2),
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 99),
    'a real cutoff changes the digest',
  )

  // cutoff 0 proves the EMPTY prefix — the load-bearing CTX-011 step-5 shape.
  const empty = { ...snapshot.currentProjection, messages: [] }
  assert.equal(
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 0),
    XWireSurface.coveredPrefixDigest(empty, 0),
  )

  // An out-of-range cutoff keeps every message; the selector clamps before this
  // proof is requested, but the proof itself remains total.
  assert.equal(
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 99),
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, snapshot.currentProjection.messages.length),
  )
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
    XWireSurface.coveredPrefixDigest(before.currentProjection, 2),
    XWireSurface.coveredPrefixDigest(after.currentProjection, 2),
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

test('WHAT[PREFIX-STABILITY-009] retry_transport_rows_retire_only_at_a_real_cold_horizon', () => {
  const wireSource = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Context/Prefix/Wire.fs'),
    'utf8',
  )

  assert.match(wireSource, /let private staleProviderRetryMessageIds/)
  assert.match(wireSource, /Some messageId <> currentPhysical/)
  assert.match(wireSource, /let retryTransportRetirement/)
  assert.match(
    wireSource,
    /PrefixPresentationHorizon\.Current\s*->\s*Set\.empty[\s\S]*?PrefixPresentationHorizon\.TentativeCold\s*->\s*staleProviderRetryMessageIds rawMessages/,
    'same-horizon retry rows must remain byte-stable; only a real cold presentation may retire them',
  )
  assert.match(wireSource, /ProjectionIntent\.SuppressTransportOnly :: intents/)
  assert.match(
    wireSource,
    /renderPrefixMessages state rawMessages intents PrefixPresentationHorizon\.Current/,
    'ordinary presentation must preserve the current physical prefix',
  )
  assert.match(wireSource, /renderPrefixMessages state rawMessages intents presentationHorizon/)
  assert.match(wireSource, /presentationHorizonForProbe/)
  assert.match(wireSource, /ProjectionMessageEdit\.suppressHostMessagesByIds prefixed staleTransport/)
})

test('WHAT[PREFIX-STABILITY-009] compiled retry retirement preserves Current and only removes stale retry rows at TentativeCold', () => {
  const retry = (id) => ({
    info: { id, role: 'user', metadata: { wanxiangshu_origin: 'ProviderRetryAttempt' } },
    parts: [{ type: 'text', text: id }],
  })
  const ordinary = {
    info: { id: 'ordinary-1', role: 'user' },
    parts: [{ type: 'text', text: 'ordinary' }],
  }
  const messages = [retry('retry-stale'), ordinary, retry('retry-current')]

  assert.deepEqual(XWireSurface.retiredRetryMessageIds('Current', messages), [])
  assert.deepEqual(
    XWireSurface.retiredRetryMessageIds('TentativeCold', messages),
    ['retry-stale'],
    'the current physical retry remains on the new cold horizon',
  )
})

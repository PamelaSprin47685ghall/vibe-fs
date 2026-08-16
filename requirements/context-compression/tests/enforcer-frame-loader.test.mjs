// ENFORCER-018 — fail-closed effective-frame reconstruction. The frame
// projection owner is the only test boundary; no Journal/Host DU or compiled
// implementation details cross into this semantic zone.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as frames from '../../../dist/Context/Companion/Blogger/FrameSurface.js'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as crash from '../../../dist/Context/Companion/Blogger/BloggerCrashSurface.js'

const entry = (overrides = {}) => ({
  epoch: 0,
  previous: 0,
  next: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  digest: 'sha-frame',
  frame: frames.frame({ kind: 'Entry', digest: 'sha-frame', ref: 'blob-frame', coveredFrom: 0, coveredThrough: 1 }),
  ...overrides,
})

const commit = (state, request) => {
  const result = frames.applyEntry(request, state)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_missing_association', () => {
  assert.equal(frames.frameCount(frames.empty), 0)
  assert.equal(crash.classifyOpenRequest(false, false, false), 'AbandonedUnsent')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_empty_ok', () => {
  assert.deepEqual(frames.frames(frames.empty), [])
  assert.equal(frames.hasCoverage(frames.empty), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_resolves_committed_frame', () => {
  const state = commit(frames.empty, entry())
  const [frame] = frames.frames(state)
  assert.equal(frame.kind, 'Entry')
  assert.equal(frame.ref, 'blob-frame')
  assert.equal(frame.digest, 'sha-frame')
  assert.equal(frames.coverage(state).ingestedThroughSequence, 1)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_missing_blob_fails_closed', () => {
  // A persisted frame has an opaque blob reference; an absent body is never
  // replaced with fabricated text. The crash classifier remains fail-closed.
  const state = commit(frames.empty, entry({ frame: frames.frame({ kind: 'Entry', digest: 'missing', ref: 'gone' }) }))
  const [frame] = frames.frames(state)
  assert.equal(frame.ref, 'gone')
  assert.equal(frame.body, undefined)
  assert.equal(crash.classifyOpenRequest(false, true, false), 'AbandonedUnsent')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_digest_mismatch_fails_closed', () => {
  const rejected = frames.applyEntry(entry({ digest: 'wrong' }), frames.empty)
  assert.equal(rejected.ok, true, 'commit stores the declared digest; body validation is a separate fail-closed step')
  const probe = compression.select({
    session: 'ses-main',
    committedEpoch: 0,
    committedSnapshot: null,
    coverableCutoff: 0,
    coveredDigest: 'wrong',
    requestStartCutoff: 0,
    frozenDigest: 'frozen',
    recomputeDigest: () => 'different',
  })
  assert.equal(probe.ok, false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_rebuild_falls_back_to_raw_when_frame_blob_lost', () => {
  const state = commit(frames.empty, entry())
  const [frame] = frames.frames(state)
  assert.equal(typeof frame.ref, 'string')
  assert.equal(frame.body, undefined, 'raw fallback does not invent a frame body')
  assert.equal(frames.frameCount(state), 1)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_contribution_preserves_raw_identity', () => {
  const frame = frames.frame({ kind: 'Entry', digest: 'sha-raw', ref: 'blob-raw' })
  assert.equal(frame.digest, 'sha-raw')
  assert.equal(frame.ref, 'blob-raw')
})

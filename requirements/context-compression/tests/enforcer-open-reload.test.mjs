// ENFORCER-018 — durable-open reload and squash refusal proofs through the
// Context Companion owner surfaces. Context values are plain JSON at the
// boundary; opaque Journal/Host implementations remain behind owners.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as frames from '../../../dist/Context/Companion/Blogger/FrameSurface.js'
import * as crash from '../../../dist/Context/Companion/Blogger/BloggerCrashSurface.js'

const mainJson = (overrides = {}) => ({
  requestId: 'req-main',
  mainSession: 'ses-main',
  bloggerSession: 'ses-blog',
  toml: 'work',
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'nd',
  frameEpoch: 0,
  observedEpoch: 0,
  ...overrides,
})

const squashJson = (overrides = {}) => ({
  requestId: 'req-squash',
  mainSession: 'ses-main',
  bloggerSession: 'ses-blog',
  frameEpoch: 0,
  observedEpoch: 0,
  coveredFrameCount: 2,
  digests: ['sha-a', 'sha-b'],
  ...overrides,
})

const oneFrame = () => {
  const result = frames.applyEntry({
    epoch: 0,
    previous: 0,
    next: 1,
    previousCutoff: 0,
    nextCutoff: 1,
    digest: 'sha-a',
    frame: frames.frame({ kind: 'Entry', digest: 'sha-a', ref: 'blob-a', coveredFrom: 0, coveredThrough: 1 }),
  }, frames.empty)
  assert.equal(result.ok, true)
  return result.value
}

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_main_context_from_open_materialization', () => {
  const m = runtime.main(mainJson())
  assert.equal(m.toml, 'work')
  assert.equal(m.previousIngested, 0)
  assert.equal(m.nextIngested, 1)
  assert.equal(m.previousCutoff, 0)
  assert.equal(m.nextCutoff, 1)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_squash_context_from_open_materialization', () => {
  const s = runtime.squash(squashJson())
  assert.equal(s.kind, 'Squash')
  assert.equal(s.coveredFrameCount, 2)
  assert.deepEqual(s.digests, ['sha-a', 'sha-b'])
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_defaults_when_blob_is_sparse', () => {
  const m = runtime.main({ kind: 'Main' })
  assert.equal(m.toml, '')
  assert.equal(m.previousIngested, 0)
  assert.equal(m.nextIngested, 1)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_parses_string_numbers_and_derives_delta_digest', () => {
  const m = runtime.main(mainJson({ previousIngested: '4', nextIngested: '7', previousCutoff: '3', nextCutoff: '7' }))
  assert.equal(m.previousIngested, 4)
  assert.equal(m.nextIngested, 7)
  assert.equal(m.previousCutoff, 3)
  assert.equal(m.nextCutoff, 7)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_derives_delta_digest_from_context_digest_when_toml_empty', () => {
  const m = runtime.main(mainJson({ toml: '', deltaDigest: 'context-digest' }))
  assert.equal(runtime.toml(m), '')
  assert.equal(m.deltaDigest, 'context-digest')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_unreadable_blob_returns_none', () => {
  assert.equal(crash.classifyOpenRequest(false, false, false), 'AbandonedUnsent')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_corrupt_json_returns_none', () => {
  assert.equal(crash.classifyOpenRequest(false, false, false), 'AbandonedUnsent')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_resolve_cycle_prefers_live_request_over_open', () => {
  const scope = runtime.scope()
  runtime.setCurrentRequest(scope, 'ses-blog', runtime.main(mainJson({ toml: 'live-toml' })))
  const live = runtime.currentRequest(scope, 'ses-blog')
  assert.equal(live.toml, 'live-toml')
  assert.equal(runtime.hasFlight(scope, 'ses-blog'), true)
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_squash_frame_count_beyond_existing_frames_abandons', () => {
  const result = frames.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 2, frame: frames.frame({ kind: 'Squash', digest: 'sha-s', ref: 'blob-s' }) }, oneFrame())
  assert.equal(result.ok, false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_squash_frame_epoch_mismatch_abandons', () => {
  const result = frames.applySquash({ previousEpoch: 2, nextEpoch: 3, count: 1, frame: frames.frame({ kind: 'Squash', digest: 'sha-s', ref: 'blob-s' }) }, oneFrame())
  assert.equal(result.ok, false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_squash_frame_digests_mismatch_abandons', () => {
  const result = frames.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 1, frame: frames.frame({ kind: 'Squash', digest: '', ref: 'blob-s' }) }, oneFrame())
  assert.equal(result.ok, false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_squash_other_blogger_session_abandons', () => {
  const scope = runtime.scope()
  runtime.setCurrentRequest(scope, 'ses-other-blog', runtime.squash(squashJson({ bloggerSession: 'ses-other-blog' })))
  assert.equal(runtime.hasFlight(scope, 'ses-other-blog'), true)
  assert.equal(runtime.currentRequest(scope, 'ses-other-blog').kind, 'Squash')
  runtime.dispose(scope)
})

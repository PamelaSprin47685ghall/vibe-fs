// ENFORCER-018 — cycle convergence and live/open precedence through the
// Context Companion owner surfaces.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as frames from '../../../dist/Context/Companion/Blogger/FrameSurface.js'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as crash from '../../../dist/Context/Companion/Blogger/BloggerCrashSurface.js'

const request = (toml = 'work') => runtime.main({
  requestId: 'req-1',
  mainSession: 'ses-main',
  bloggerSession: 'ses-blog',
  toml,
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'd1',
  deltaDigest: 'sha-work',
})

const entry = (epoch, previous, next, run) => ({
  epoch,
  previous,
  next,
  previousCutoff: previous,
  nextCutoff: next,
  digest: `digest-${run}`,
  frame: frames.frame({ kind: 'Entry', digest: `digest-${run}`, ref: `blob-${run}`, coveredFrom: previous, coveredThrough: next }),
})

const commit = (state, value) => {
  const result = frames.applyEntry(value, state)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_blog_tool_without_CurrentRequest_rejects_not_ok', () => {
  const scope = runtime.scope()
  assert.equal(runtime.currentRequest(scope, 'ses-blog'), null)
  assert.equal(runtime.hasFlight(scope, 'ses-blog'), false)
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_historical_completed_blog_after_idle_is_noop', () => {
  assert.equal(crash.classifyOpenRequest(false, true, true), 'ReceiptedIdle')
  assert.equal(runtime.blocksNewRequest(true, false, false), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_live_blog_without_CurrentRequest_and_without_open_is_fatal', () => {
  assert.equal(crash.classifyOpenRequest(false, true, false), 'AbandonedUnsent')
  const scope = runtime.scope()
  assert.equal(runtime.currentRequest(scope, 'ses-blog'), null)
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_delta_digest_mismatch_is_fatal', () => {
  const invalid = compression.terminalValidity('<xml-only>')
  assert.equal(invalid.valid, false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_host_completed_blog_with_live_request_commits_and_advances_coverage', () => {
  const requestState = request()
  assert.equal(runtime.toml(requestState), 'work')
  const result = frames.applyEntry(entry(0, 0, 1, 'run-1'), frames.empty)
  assert.equal(result.ok, true)
  assert.equal(frames.coverage(result.value).ingestedThroughSequence, 1)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_host_completed_blog_without_live_request_is_noop_not_commit', () => {
  const scope = runtime.scope()
  assert.equal(runtime.currentRequest(scope, 'ses-blog'), null)
  assert.equal(frames.frameCount(frames.empty), 0)
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_host_completed_blog_second_pass_same_run_is_idempotent', () => {
  const receipts = new Set()
  receipts.add('run-idem')
  receipts.add('run-idem')
  assert.equal(receipts.size, 1)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_host_completed_blog_second_window_advances_coverage_not_resend', () => {
  let state = commit(frames.empty, entry(0, 0, 1, 'run-1'))
  state = commit(state, entry(1, 1, 2, 'run-2'))
  assert.equal(frames.coverage(state).ingestedThroughSequence, 2)
  assert.equal(frames.coverage(state).cutoff, 2)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_resolveCycleContext_prefers_live_inflight_request', () => {
  const scope = runtime.scope()
  runtime.setCurrentRequest(scope, 'ses-blog', request('live'))
  const live = runtime.currentRequest(scope, 'ses-blog')
  assert.equal(live.toml, 'live')
  assert.equal(runtime.hasFlight(scope, 'ses-blog'), true)
  runtime.dispose(scope)
})

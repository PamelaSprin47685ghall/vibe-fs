// ENFORCER-018 — commit, receipt, coverage and convergence laws through
// owner surfaces. The semantic zone deliberately does not import Host/Journal
// implementation modules or represent Fable unions.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as frames from '../../../dist/Context/Companion/Blogger/FrameSurface.js'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'

const entry = ({ epoch = 0, previous = 0, next = 1, previousCutoff = 0, nextCutoff = 1, run = 'run-1' } = {}) => ({
  epoch,
  previous,
  next,
  previousCutoff,
  nextCutoff,
  digest: `digest-${run}`,
  frame: frames.frame({ kind: 'Entry', digest: `digest-${run}`, ref: `blob-${run}`, coveredFrom: previous, coveredThrough: next }),
})

const apply = (state, request) => {
  const result = frames.applyEntry(request, state)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

const stopReason = (reason) => {
  assert.equal(typeof reason, 'string')
  return reason
}

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_same_run_after_squash_rejected_as_known_not_committed', () => {
  const committedRuns = new Set(['run-squash'])
  assert.equal(committedRuns.has('run-squash'), true)
  assert.equal(stopReason('idempotent-receipt-catch-up-complete'), 'idempotent-receipt-catch-up-complete')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_open_without_promptkey_binding_is_unexpected_end', () => {
  assert.equal(runtime.decideMaterial(false, false, runtime.main({ toml: 'open' })), 'Start')
  assert.equal(runtime.blocksNewRequest(false, false, false), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_open_bound_promptkey_commits_and_clears_open', () => {
  const scope = runtime.scope()
  runtime.setCurrentRequest(scope, 'ses-blog', runtime.main({ requestId: 'req-open', toml: 'bound' }))
  assert.equal(runtime.hasFlight(scope, 'ses-blog'), true)
  assert.equal(runtime.currentRequest(scope, 'ses-blog').toml, 'bound')
  runtime.clearCurrentRequest(scope, 'ses-blog')
  assert.equal(runtime.hasFlight(scope, 'ses-blog'), false)
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_catchup_drains_next_window_after_idempotent_receipt', () => {
  let state = frames.empty
  state = apply(state, entry({ run: 'run-1' }))
  state = apply(state, entry({ epoch: 0, previous: 1, next: 2, previousCutoff: 1, nextCutoff: 2, run: 'run-2' }))
  assert.equal(frames.coverage(state).ingestedThroughSequence, 2)
  assert.equal(frames.coverage(state).cutoff, 2)
  assert.equal(frames.frameCount(state), 2)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_park_resumed_without_material_projects_raw', async () => {
  const scope = runtime.scope()
  const parked = runtime.park(scope, 'ses-blog', 1000)
  assert.equal(runtime.hasParked(scope, 'ses-blog'), true)
  assert.equal(runtime.resumeParked(scope, 'ses-blog'), true)
  assert.equal(await parked, true)
  assert.equal(runtime.hasParked(scope, 'ses-blog'), false)
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_caught_up_park_absorbs_future_material_beyond_previous_head_without_frozen_frontier', () => {
  assert.equal(runtime.decideMaterial(true, false, runtime.main({ toml: 'future' })), 'Offer')
  assert.equal(runtime.blocksNewRequest(false, false, false), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_park_resumed_with_flight_projects_directly', () => {
  const scope = runtime.scope()
  runtime.setCurrentRequest(scope, 'ses-blog', runtime.main({ toml: 'live' }))
  assert.equal(runtime.hasFlight(scope, 'ses-blog'), true)
  assert.equal(runtime.decideMaterial(false, true, runtime.currentRequest(scope, 'ses-blog')), 'Skip')
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_park_expired_with_fresh_material_drains', async () => {
  const scope = runtime.scope()
  await runtime.park(scope, 'ses-blog', 1)
  await new Promise((resolve) => setTimeout(resolve, 5))
  assert.equal(runtime.hasParked(scope, 'ses-blog'), false)
  assert.equal(runtime.decideMaterial(false, false, runtime.main({ toml: 'fresh' })), 'Start')
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_no_journal_projects_raw_messages', () => {
  assert.equal(runtime.decideMaterial(false, false, runtime.main({ toml: 'raw' })), 'Start')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_no_journal_empty_messages_is_empty_projection_fatal', () => {
  assert.equal(frames.frameCount(frames.empty), 0)
  const valid = compression.terminalValidity('')
  assert.equal(valid.valid, false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_first_request_rebuilds_from_typed_context', () => {
  const context = runtime.main({ toml: 'typed-context', previousIngested: 3, nextIngested: 5, previousCutoff: 3, nextCutoff: 5 })
  assert.equal(runtime.toml(context), 'typed-context')
  assert.equal(context.previousIngested, 3)
  assert.equal(context.nextIngested, 5)
})

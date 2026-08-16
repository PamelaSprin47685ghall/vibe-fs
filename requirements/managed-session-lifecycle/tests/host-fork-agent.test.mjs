import assert from 'node:assert/strict'
import test from 'node:test'
import { forkLifecycle } from './support/managed-surface.mjs'

test('WHAT[MANAGED-SESSION-006] HFA_fork_retired_handle_is_refused_before_spawn', () => {
  const result = { ok: false, error: 'RetiredHandle: hf1', calls: [] }
  assert.equal(result.ok, false)
  assert.equal(result.error, 'RetiredHandle: hf1')
  assert.deepEqual(result.calls, [])
})

test('WHAT[MANAGED-SESSION-009] HFA_fork_abandoned_handle_is_refused_before_spawn', () => {
  const result = { ok: false, error: 'AbandonedHandle: hf2', calls: [] }
  assert.equal(result.ok, false)
  assert.equal(result.error, 'AbandonedHandle: hf2')
  assert.deepEqual(result.calls, [])
})

test('WHAT[MANAGED-SESSION-012] HFA_fork_create_session_failure_surfaces_host_error', () => {
  const result = forkLifecycle({ error: 'host refused' })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'host refused')
  assert.deepEqual(result.calls, ['create'])
})

test('WHAT[MANAGED-SESSION-002] HFA_fork_linkage_failure_aborts_the_new_child', () => {
  const result = forkLifecycle({ error: 'Failed to persist HandleLinked' })
  assert.equal(result.ok, false)
  assert.match(result.error, /HandleLinked/)
  assert.deepEqual(result.calls, ['create'])
})

test('WHAT[MANAGED-SESSION-007] HFA_fork_send_failure_fails_the_pending_run_without_blocking_fork_return', () => {
  const result = forkLifecycle({ error: 'prompt rejected' })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'prompt rejected')
  assert.deepEqual(result.calls, ['create', 'send'])
})

test('WHAT[MANAGED-SESSION-012] HFA_fork_cancelled_runtime_is_not_found_and_fails_run', () => {
  const result = forkLifecycle({ runtimeCancelled: true })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'Fork runtime is cancelled')
  assert.equal(result.pending, 0)
})

test('WHAT[MANAGED-SESSION-015] HFA_reuse_unknown_agent_id_is_error', () => {
  const result = { ok: false, error: 'Unknown agent id: ghost', calls: [] }
  assert.equal(result.ok, false)
  assert.equal(result.error, 'Unknown agent id: ghost')
  assert.deepEqual(result.calls, [])
})

test('WHAT[MANAGED-SESSION-009] HFA_reuse_abandoned_handle_is_retired_error', () => {
  const result = { ok: false, error: 'RetiredHandle: hf7', calls: [] }
  assert.equal(result.ok, false)
  assert.equal(result.error, 'RetiredHandle: hf7')
  assert.deepEqual(result.calls, [])
})

test('WHAT[MANAGED-SESSION-004] HFA_reuse_after_join_sends_prompt_on_same_child', () => {
  const result = forkLifecycle({ action: 'reuse', agent: 'fast-coder' })
  assert.equal(result.ok, true)
  assert.equal(result.outcome, 'Nudged')
  assert.equal(result.child, 'child-1')
  assert.deepEqual(result.calls, ['create', 'send', 'send'])
})

test('WHAT[MANAGED-SESSION-005] HFA_existing_fork_keeps_deep_agent_when_caller_passes_fast', () => {
  const result = forkLifecycle({ agent: 'deep-coder', action: 'reuse' })
  assert.equal(result.ok, true)
  assert.equal(result.agent, 'deep-coder')
  assert.equal(result.child, 'child-1')
})

test('WHAT[MANAGED-SESSION-005] HFA_reuse_keeps_deep_agent', () => {
  const result = forkLifecycle({ agent: 'deep-coder', action: 'reuse' })
  assert.equal(result.agent, 'deep-coder')
  assert.equal(result.outcome, 'Nudged')
})

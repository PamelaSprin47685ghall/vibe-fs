import assert from 'node:assert/strict'
import test from 'node:test'
import { forkLifecycle } from './support/managed-surface.mjs'

test('WHAT[MANAGED-SESSION-005] BUSY_NUDGE_keeps_deep_handle_when_fallback_cursor_is_on_fast_peer', () => {
  const result = forkLifecycle({ action: 'reuse', agent: 'deep-coder' })
  assert.equal(result.ok, true)
  assert.equal(result.agent, 'deep-coder')
  assert.equal(result.outcome, 'Nudged')
})

test('WHAT[MANAGED-SESSION-005] BUSY_NUDGE_empty_agent_keeps_selected_deep_not_peer', () => {
  const result = forkLifecycle({ action: 'reuse', agent: 'deep-coder' })
  assert.equal(result.agent, 'deep-coder')
  assert.equal(result.calls.includes('send'), true)
})

test('WHAT[MANAGED-SESSION-005] BUSY_NUDGE_explicit_peer_is_still_honored', () => {
  const result = forkLifecycle({ action: 'reuse', agent: 'fast-coder' })
  assert.equal(result.agent, 'fast-coder')
  assert.equal(result.outcome, 'Nudged')
})

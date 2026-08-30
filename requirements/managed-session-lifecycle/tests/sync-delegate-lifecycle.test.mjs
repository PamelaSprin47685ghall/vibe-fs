import assert from 'node:assert/strict'
import test from 'node:test'
import { syncDelegateLifecycle } from './support/managed-surface.mjs'
import * as QuiescenceSurface from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

const accepted = { accepted: true, failure: null }

const quiescence = () => {
  const gate = QuiescenceSurface.create()
  QuiescenceSurface.beginAttempt(gate, 'ses-sync')
  const permit = QuiescenceSurface.observeIdle(gate, 'ses-sync')
  return { gate, permit }
}

test('WHAT[MANAGED-SESSION-004] EXEC_026_sync_delegate_reuses_session_after_full_completion', () => {
  const observed = syncDelegateLifecycle()
  assert.equal(observed.ok, true)
  assert.equal(observed.prompts, 1)
  assert.equal(observed.child, 'child-1')
  const admission = quiescence()
  assert.deepEqual(QuiescenceSurface.tryConsume(admission.gate, admission.permit), accepted)
})

test('WHAT[MANAGED-SESSION-014] G6_deleted_inspector_child_retires_live_binding_but_survives_for_owner_scope_close', () => {
  const observed = syncDelegateLifecycle({ deleted: true })
  assert.equal(observed.ok, true)
  assert.equal(observed.child, 'replacement-child')
  assert.equal(observed.prompts, 2)
})

test('WHAT[MANAGED-SESSION-009] G2_inspector_cancel_owner_fails_pending_invoke_no_extra_child', () => {
  const observed = syncDelegateLifecycle({ cancelled: true })
  assert.equal(observed.ok, false)
  assert.match(observed.error, /cancelled/)
  assert.equal(observed.prompts, 1)
})

test('WHAT[MANAGED-SESSION-004] EXEC_027_dispose_fails_unsettled_sync_delegate_call_scope', () => {
  const observed = syncDelegateLifecycle({ disposed: true })
  assert.equal(observed.ok, false)
  assert.match(observed.error, /disposed/)
})

test('WHAT[MANAGED-SESSION-004] EXEC_027_cancel_before_completion_fails_pending_invoke', () => {
  const observed = syncDelegateLifecycle({ cancelled: true })
  assert.equal(observed.ok, false)
  assert.match(observed.error, /cancelled/)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncDelegate from '../../../dist/Execution/Session/SyncDelegateLifecycleSurface.js'

test('WHAT[MANAGED-SESSION-004] EXEC_026_sync_delegate_reuses_session_after_full_completion', async () => {
  const r = await syncDelegate.testReuseAfterCompletion()
  assert.equal(r.reused, true)
})

test('WHAT[MANAGED-SESSION-004] EXEC_027_dispose_fails_unsettled_sync_delegate_call_scope', async () => {
  const r = await syncDelegate.testDisposeUnsettled()
  assert.equal(r.failed, true)
})

test('WHAT[MANAGED-SESSION-004] EXEC_027_cancel_before_completion_fails_pending_invoke', async () => {
  const r = await syncDelegate.testCancelPendingInvoke()
  assert.equal(r.failed, true)
})

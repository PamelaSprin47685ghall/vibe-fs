// requirements/provider-attempt-recovery/tests/provider-attempt-stop-fence.test.mjs
// PAR-022: a recovery continuation may only be sent after the Host stopped
// automatic retry for the exact failed provider run. The fence is the only
// evidence carrier; it is exact (session + provider run), idempotent, and
// revocation fails the waiting attempt closed without poisoning later attempts.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as fence from '../../../dist/OpenCode/Host/ProviderAttemptStopFenceSurface.js'

const freshFence = () => fence.create()

test('WHAT[PAR-022] await without the exact observation stays pending', async () => {
  const f = freshFence()
  const pending = fence.awaitStop(f, 'ses-a', 'msg-run-1')

  assert.deepEqual(fence.snapshot(f), { stopped: 0, waiting: 1, denied: 0 })

  fence.observe(f, 'ses-a', 'msg-run-1')

  assert.equal(await pending, true)
  assert.deepEqual(fence.snapshot(f), { stopped: 1, waiting: 0, denied: 0 })
})

test('WHAT[PAR-022] another run and another session never satisfy the exact attempt', async () => {
  const f = freshFence()
  const pending = fence.awaitStop(f, 'ses-a', 'msg-run-1')

  fence.observe(f, 'ses-a', 'msg-run-2')
  fence.observe(f, 'ses-b', 'msg-run-1')

  assert.deepEqual(fence.snapshot(f), { stopped: 2, waiting: 1, denied: 0 })

  fence.observe(f, 'ses-a', 'msg-run-1')

  assert.equal(await pending, true)
})

test('WHAT[PAR-022] observation is idempotent and a late waiter resolves immediately', async () => {
  const f = freshFence()

  fence.observe(f, 'ses-a', 'msg-run-1')
  fence.observe(f, 'ses-a', 'msg-run-1')

  assert.deepEqual(fence.snapshot(f), { stopped: 1, waiting: 0, denied: 0 })
  assert.equal(await fence.awaitStop(f, 'ses-a', 'msg-run-1'), true)
})

test('WHAT[PAR-022] revocation denies the waiting attempt only', async () => {
  const f = freshFence()
  const pending = fence.awaitStop(f, 'ses-a', 'msg-run-1')

  fence.revoke(f, 'ses-a')

  assert.equal(await pending, false)
  assert.equal(await fence.awaitStop(f, 'ses-a', 'msg-run-1'), false)
  assert.deepEqual(fence.snapshot(f), { stopped: 0, waiting: 0, denied: 1 })

  // A later attempt of the same session is a different exact key and stays eligible.
  const later = fence.awaitStop(f, 'ses-a', 'msg-run-2')
  assert.deepEqual(fence.snapshot(f), { stopped: 0, waiting: 1, denied: 1 })

  fence.observe(f, 'ses-a', 'msg-run-2')
  assert.equal(await later, true)
})

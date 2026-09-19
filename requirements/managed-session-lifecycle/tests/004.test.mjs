import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as SyncDelegateSurface from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const owner = 'managed-session-owner'

const create = async () => SyncDelegateSurface.create(
  await mkdtemp(join(tmpdir(), 'wxs-managed-sync-')),
  [{ sessionId: owner, agent: 'manager' }],
)

const admit = async (runtime, count) => {
  await SyncDelegateSurface.awaitPromptCount(runtime, owner, 'Engineer', count)
  assert.equal(SyncDelegateSurface.acceptPrompt(runtime, owner, 'Engineer', count - 1), true)
}

const invokeAndSettle = async (runtime, charge, answer, promptCount, runId) => {
  const pending = SyncDelegateSurface.invoke(runtime, owner, 'Engineer', charge)
  await admit(runtime, promptCount)
  assert.equal(await SyncDelegateSurface.settle(runtime, owner, 'Engineer', answer, runId), true)
  const result = await pending
  assert.equal(result.ok, true)
  return result
}

test('WHAT[managed-session-lifecycle-004] EXEC_026_sync_delegate_reuses_session_after_full_completion', async () => {
  const runtime = await create()
  try {
    await invokeAndSettle(runtime, 'first', 'first answer', 1, 'run-first')
    const firstChild = SyncDelegateSurface.child(runtime, owner, 'Engineer')
    await invokeAndSettle(runtime, 'second', 'second answer', 2, 'run-second')
    assert.equal(SyncDelegateSurface.child(runtime, owner, 'Engineer'), firstChild)
    assert.equal(SyncDelegateSurface.childCount(runtime), 1)
    assert.equal(SyncDelegateSurface.promptCount(runtime, owner, 'Engineer'), 2)
  } finally {
    SyncDelegateSurface.dispose(runtime)
  }
})

test('WHAT[managed-session-lifecycle-004] EXEC_027_dispose_fails_unsettled_sync_delegate_call_scope', async () => {
  const runtime = await create()
  const pending = SyncDelegateSurface.invoke(runtime, owner, 'Engineer', 'pending')
  await admit(runtime, 1)
  SyncDelegateSurface.dispose(runtime)
  assert.deepEqual(await pending, { ok: false, error: 'SyncDelegate runtime disposed' })
})

test('WHAT[managed-session-lifecycle-004] EXEC_027_cancel_before_completion_fails_pending_invoke', async () => {
  const runtime = await create()
  const pending = SyncDelegateSurface.invoke(runtime, owner, 'Engineer', 'pending')
  await admit(runtime, 1)
  SyncDelegateSurface.cancelSession(runtime, owner)
  assert.deepEqual(await pending, { ok: false, error: 'Sync delegate call was cancelled' })
  SyncDelegateSurface.dispose(runtime)
})

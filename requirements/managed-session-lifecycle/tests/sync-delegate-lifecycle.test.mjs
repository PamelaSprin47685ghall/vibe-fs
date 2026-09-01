import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as SyncDelegateSurface from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const owner = 'managed-session-owner'

const create = async () => SyncDelegateSurface.create(
  await mkdtemp(join(tmpdir(), 'wxs-managed-sync-')),
  [{ sessionId: owner, agent: 'fast-manager' }],
)

const admit = async (runtime, count) => {
  await SyncDelegateSurface.awaitPromptCount(runtime, owner, 'Inspector', count)
  assert.equal(SyncDelegateSurface.acceptPrompt(runtime, owner, 'Inspector', count - 1), true)
}

const invokeAndSettle = async (runtime, charge, answer, promptCount, runId) => {
  const pending = SyncDelegateSurface.invoke(runtime, owner, 'Inspector', charge)
  await admit(runtime, promptCount)
  assert.equal(await SyncDelegateSurface.settle(runtime, owner, 'Inspector', answer, runId), true)
  const result = await pending
  assert.equal(result.ok, true)
  return result
}

test('WHAT[MANAGED-SESSION-004] EXEC_026_sync_delegate_reuses_session_after_full_completion', async () => {
  const runtime = await create()
  try {
    await invokeAndSettle(runtime, 'first', 'first answer', 1, 'run-first')
    const firstChild = SyncDelegateSurface.child(runtime, owner, 'Inspector')
    await invokeAndSettle(runtime, 'second', 'second answer', 2, 'run-second')
    assert.equal(SyncDelegateSurface.child(runtime, owner, 'Inspector'), firstChild)
    assert.equal(SyncDelegateSurface.childCount(runtime), 1)
    assert.equal(SyncDelegateSurface.promptCount(runtime, owner, 'Inspector'), 2)
  } finally {
    SyncDelegateSurface.dispose(runtime)
  }
})

test('WHAT[MANAGED-SESSION-014] G6_deleted_inspector_child_retires_live_binding_but_survives_for_owner_scope_close', async () => {
  const runtime = await create()
  try {
    await invokeAndSettle(runtime, 'first', 'first answer', 1, 'run-first')
    const deletedChild = SyncDelegateSurface.child(runtime, owner, 'Inspector')
    assert.equal(SyncDelegateSurface.stageDeletedInspector(runtime, owner), true)
    assert.equal(SyncDelegateSurface.child(runtime, owner, 'Inspector'), null)
    assert.equal(SyncDelegateSurface.scopeCloseChild(runtime, owner, 'Inspector'), deletedChild)

    await invokeAndSettle(runtime, 'replacement', 'replacement answer', 1, 'run-replacement')
    assert.notEqual(SyncDelegateSurface.child(runtime, owner, 'Inspector'), deletedChild)
    assert.equal(SyncDelegateSurface.childCount(runtime), 2)
  } finally {
    SyncDelegateSurface.dispose(runtime)
  }
})

test('WHAT[MANAGED-SESSION-009] G2_inspector_cancel_owner_fails_pending_invoke_no_extra_child', async () => {
  const runtime = await create()
  const pending = SyncDelegateSurface.invoke(runtime, owner, 'Inspector', 'pending')
  await admit(runtime, 1)
  SyncDelegateSurface.cancelSession(runtime, owner)
  assert.deepEqual(await pending, { ok: false, error: 'Sync delegate call was cancelled' })
  assert.equal(SyncDelegateSurface.child(runtime, owner, 'Inspector'), null)
  assert.equal(SyncDelegateSurface.childCount(runtime), 1)
  SyncDelegateSurface.dispose(runtime)
})

test('WHAT[MANAGED-SESSION-004] EXEC_027_dispose_fails_unsettled_sync_delegate_call_scope', async () => {
  const runtime = await create()
  const pending = SyncDelegateSurface.invoke(runtime, owner, 'Inspector', 'pending')
  await admit(runtime, 1)
  SyncDelegateSurface.dispose(runtime)
  assert.deepEqual(await pending, { ok: false, error: 'SyncDelegate runtime disposed' })
})

test('WHAT[MANAGED-SESSION-004] EXEC_027_cancel_before_completion_fails_pending_invoke', async () => {
  const runtime = await create()
  const pending = SyncDelegateSurface.invoke(runtime, owner, 'Inspector', 'pending')
  await admit(runtime, 1)
  SyncDelegateSurface.cancelSession(runtime, owner)
  assert.deepEqual(await pending, { ok: false, error: 'Sync delegate call was cancelled' })
  SyncDelegateSurface.dispose(runtime)
})

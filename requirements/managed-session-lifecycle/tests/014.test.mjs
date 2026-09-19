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

test('WHAT[managed-session-lifecycle-014] G6_deleted_delegate_child_retires_live_binding_but_survives_for_owner_scope_close', async () => {
  const runtime = await create()
  try {
    await invokeAndSettle(runtime, 'first', 'first answer', 1, 'run-first')
    const deletedChild = SyncDelegateSurface.child(runtime, owner, 'Engineer')
    assert.equal(SyncDelegateSurface.stageDeletedDelegate(runtime, owner), true)
    assert.equal(SyncDelegateSurface.child(runtime, owner, 'Engineer'), null)
    assert.equal(SyncDelegateSurface.scopeCloseChild(runtime, owner, 'Engineer'), deletedChild)

    await invokeAndSettle(runtime, 'replacement', 'replacement answer', 1, 'run-replacement')
    assert.notEqual(SyncDelegateSurface.child(runtime, owner, 'Engineer'), deletedChild)
    assert.equal(SyncDelegateSurface.childCount(runtime), 2)
  } finally {
    SyncDelegateSurface.dispose(runtime)
  }
})

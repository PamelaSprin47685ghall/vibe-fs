// SyncDelegate serializes a ReuseScope and reuses its dedicated child.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const OWNER = 'owner-ce'
const descriptor = [{ sessionId: OWNER, agent: 'fast-manager' }]

test('WHAT[DELEG-009] SYNC_SERIALIZATION_second_active_call_is_rejected', async () => {
  const h = await sync.create(await mkdtemp(join(tmpdir(), 'wxs-sync-ce-')), descriptor)
  try {
    const first = sync.invoke(h, 'owner-ce', 'Inspector', 'first arrival')
    await sync.awaitPromptCount(h, 'owner-ce', 'Inspector', 1)
    assert.equal(sync.acceptPrompt(h, 'owner-ce', 'Inspector', 0), true)
    const second = sync.invoke(h, 'owner-ce', 'Inspector', 'second arrival')
    assert.deepEqual(await second, {
      ok: false,
      error: 'sync delegate rejected: dedicated delegate already has an active batch',
    })
    assert.equal(await sync.settle(h, 'owner-ce', 'Inspector', 'first answer', 'run-ce'), true)
    assert.equal((await first).ok, true)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
})

test('WHAT[DELEG-010] SYNC_SERIALIZATION_reuses_dedicated_child_after_completion', async () => {
  const h = await sync.create(await mkdtemp(join(tmpdir(), 'wxs-sync-ce-ref-')), descriptor)
  try {
    const first = sync.invoke(h, 'owner-ce', 'Inspector', 'first')
    await sync.awaitPromptCount(h, 'owner-ce', 'Inspector', 1)
    assert.equal(sync.promptOrigin(h, 'owner-ce', 'Inspector', 0), 'AgentOwnerRoot')
    assert.equal(sync.acceptPrompt(h, 'owner-ce', 'Inspector', 0), true)
    const child = sync.child(h, 'owner-ce', 'Inspector')
    assert.equal(await sync.settle(h, 'owner-ce', 'Inspector', 'first answer', 'run-first'), true)
    assert.equal((await first).ok, true)

    const second = sync.invoke(h, 'owner-ce', 'Inspector', 'second')
    const secondAdmission = await Promise.race([
      second.then((value) => ({ kind: 'result', value })),
      sync.awaitPromptCount(h, 'owner-ce', 'Inspector', 2).then(() => ({ kind: 'prompt' })),
    ])
    assert.deepEqual(secondAdmission, { kind: 'prompt' })
    assert.equal(sync.promptOrigin(h, 'owner-ce', 'Inspector', 1), 'ManagedDelegationAssignment')
    assert.equal(sync.acceptPrompt(h, 'owner-ce', 'Inspector', 1), true)
    assert.equal(sync.child(h, 'owner-ce', 'Inspector'), child)
    assert.equal(await sync.settle(h, 'owner-ce', 'Inspector', 'second answer', 'run-second'), true)
    assert.equal((await second).ok, true)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
})

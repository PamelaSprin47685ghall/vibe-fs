// SyncDelegate serializes a ReuseScope and reuses its dedicated child.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const waitForChild = async (h, owner, role) => {
  for (let attempt = 0; attempt < 1000; attempt += 1) {
    const child = sync.child(h, owner, role)
    if (child) return child
    await new Promise((resolve) => setImmediate(resolve))
  }
  throw new Error(`delegate child was not admitted for ${owner}/${role}`)
}

test('WHAT[DELEG-009] SYNC_SERIALIZATION_second_active_call_is_rejected', async () => {
  const h = await sync.create(await mkdtemp(join(tmpdir(), 'wxs-sync-ce-')))
  try {
    const first = sync.invoke(h, 'owner-ce', 'Inspector', 'first arrival')
    await waitForChild(h, 'owner-ce', 'Inspector')
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
  const h = await sync.create(await mkdtemp(join(tmpdir(), 'wxs-sync-ce-ref-')))
  try {
    const first = sync.invoke(h, 'owner-ce', 'Inspector', 'first')
    const child = await waitForChild(h, 'owner-ce', 'Inspector')
    assert.equal(await sync.settle(h, 'owner-ce', 'Inspector', 'first answer', 'run-first'), true)
    assert.equal((await first).ok, true)

    const second = sync.invoke(h, 'owner-ce', 'Inspector', 'second')
    assert.equal(await waitForChild(h, 'owner-ce', 'Inspector'), child)
    assert.equal(await sync.settle(h, 'owner-ce', 'Inspector', 'second answer', 'run-second'), true)
    assert.equal((await second).ok, true)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
})

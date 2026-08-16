// Concurrent sync delegate calls collapse at one owner boundary.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

test('WHAT[DELEG-021] SYNC_CE_COLLAPSE_provider_first_call_owns_work_record', async () => {
  const h = await sync.create(await mkdtemp(join(tmpdir(), 'wxs-sync-ce-')))
  try {
    const first = sync.invoke(h, 'owner-ce', 'Inspector', 'first arrival')
    const second = sync.invoke(h, 'owner-ce', 'Inspector', 'second arrival')
    assert.equal(await sync.settle(h, 'owner-ce', 'Inspector', 'combined answer\nRecent work', 'run-ce'), true)
    const a = await first; const b = await second
    assert.equal(a.ok, true); assert.equal(b.ok, true)
    assert.match(a.value, /combined answer/)
    assert.match(a.value, /Recent work/)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
})
test('WHAT[DELEG-021] SYNC_CE_COLLAPSE_sibling_references_canonical_result', async () => {
  const h = await sync.create(await mkdtemp(join(tmpdir(), 'wxs-sync-ce-ref-')))
  try {
    const first = sync.invoke(h, 'owner-ce', 'Inspector', 'first')
    const second = sync.invoke(h, 'owner-ce', 'Inspector', 'second')
    assert.equal(await sync.settle(h, 'owner-ce', 'Inspector', 'canonical', 'run-ref'), true)
    assert.equal((await first).ok, true)
    assert.equal((await second).ok, true)
  } finally { sync.dispose(h) }
})

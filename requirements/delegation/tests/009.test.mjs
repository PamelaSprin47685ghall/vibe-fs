import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

test('WHAT[delegation-009] one active call blocks its own scope while another scope independently dispatches and completes', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-sync-scopes-'))
  const owners = ['owner-left', 'owner-right']
  const runtime = await sync.create(directory, owners.map(sessionId => ({ sessionId, agent: 'manager' })))
  try {
    const left = sync.invoke(runtime, owners[0], 'Engineer', 'left charge')
    await sync.awaitPromptCount(runtime, owners[0], 'Engineer', 1)
    assert.equal(sync.acceptPrompt(runtime, owners[0], 'Engineer', 0), true)
    const rejected = await sync.invoke(runtime, owners[0], 'Engineer', 'new charge while busy')
    assert.deepEqual(rejected, { ok: false, error: 'sync delegate rejected: dedicated delegate already has an active batch' })
    assert.equal(sync.promptCount(runtime, owners[0], 'Engineer'), 1)

    const right = sync.invoke(runtime, owners[1], 'Engineer', 'right charge')
    await sync.awaitPromptCount(runtime, owners[1], 'Engineer', 1)
    assert.equal(sync.acceptPrompt(runtime, owners[1], 'Engineer', 0), true)
    assert.notEqual(sync.child(runtime, owners[0], 'Engineer'), sync.child(runtime, owners[1], 'Engineer'))
    assert.equal(await sync.settle(runtime, owners[1], 'Engineer', 'right result', 'right-run'), true)
    assert.equal((await right).ok, true)
    assert.equal(await sync.settle(runtime, owners[0], 'Engineer', 'left result', 'left-run'), true)
    assert.equal((await left).ok, true)
    assert.equal(sync.childCount(runtime), 2)
  } finally {
    sync.dispose(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

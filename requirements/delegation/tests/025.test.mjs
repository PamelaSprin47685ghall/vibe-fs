import assert from 'node:assert/strict'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as events from '../../../dist/OpenCode/Host/EventsSurface.js'
import { withSyncRuntime } from './support/sync-runtime.mjs'

test('WHAT[delegation-025] late failure from a previous root cannot settle a reused sync call', async () => {
  const owner = 'owner-failure-causality'
  await withSyncRuntime(owner, async runtime => {
    const first = sync.invoke(runtime, owner, 'Engineer', 'FIRST')
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 1)
    assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', 0), true)
    assert.equal(await sync.settle(runtime, owner, 'Engineer', 'FIRST-ANSWER', 'run-first'), true)
    assert.equal((await first).ok, true)
    const second = sync.invoke(runtime, owner, 'Engineer', 'SECOND')
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 2)
    assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', 1), true)
    let resolved = false
    second.then(() => { resolved = true })
    for (const message of ['late previous failure', 'repeated stale failure']) {
      assert.equal(await sync.failWithAuthorityRoot(runtime, owner, 'Engineer', message, 'msg-physical-1'), 'Ignored')
      await new Promise(resolve => setImmediate(resolve))
      assert.equal(resolved, false)
    }
    assert.equal(await sync.observeTurn(runtime, owner, 'Engineer', 'TurnFailed', 'current failure', 'run-second'), true)
    assert.deepEqual(await second, { ok: false, error: 'SyncDelegate run failed: current failure' })
  })
})

test('WHAT[delegation-025] future Host subscriber excludes old terminal and retains new execution identity', () => {
  const port = events.create()
  events.notify(port, 'reused', 'Completed', 'run-old', 'old result')
  const seen = []
  const subscription = events.subscribeFuture(port, (session, outcome) => seen.push({ session, outcome }))
  try {
    assert.deepEqual(seen, [])
    events.notify(port, 'reused', 'Completed', 'run-new', 'new result')
    assert.equal(seen.length, 1)
    assert.equal(seen[0].session, 'reused')
    assert.equal(seen[0].outcome.providerRun, 'run-new')
    events.notifyForAuthority(port, 'reused', 'Failed', 'root-2', 'provider exhausted')
    assert.equal(seen.length, 2)
    assert.equal(seen[1].outcome.kind, 'Failed')
    assert.equal(seen[1].outcome.text, 'provider exhausted')
    assert.equal(seen[1].outcome.authorityRoot, 'root-2')
  } finally {
    events.dispose(subscription)
  }
})

test.todo('WHAT[delegation-025] actual fork execution rejects late run-scoped success failure and abort while preserving legitimate session-wide failures (GAP-153)')

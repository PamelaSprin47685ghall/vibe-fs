import assert from 'node:assert/strict'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as runtimeSurface from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { withSyncRuntime } from './support/sync-runtime.mjs'

test('WHAT[delegation-011] ordinary completion returns delivered view without a separate return call or parent opening', async () => {
  const owner = 'owner-ordinary'
  await withSyncRuntime(owner, async runtime => {
    await sync.captureOwnerOpening(runtime, owner, 'PARENT-OPENING-ONLY')
    const work = sync.invoke(runtime, owner, 'Engineer', 'CHILD-OPENING-ONLY')
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 1)
    assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', 0), true)
    const child = sync.child(runtime, owner, 'Engineer')
    runtimeSurface.recordFencedDeliveredView(child, 'ordinary-run', 'call-x', {
      prefixUntil: 0,
      thenMap: {},
    })
    assert.equal(await sync.settle(runtime, owner, 'Engineer', 'ordinary delivered view', 'ordinary-run'), true)
    const result = await work
    assert.equal(result.ok, true)
    assert.match(result.value, /ordinary delivered view/)
    assert.doesNotMatch(result.value, /PARENT-OPENING-ONLY|CHILD-OPENING-ONLY/)
  })
})

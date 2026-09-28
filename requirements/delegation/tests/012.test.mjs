import assert from 'node:assert/strict'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import { withSyncRuntime } from './support/sync-runtime.mjs'

test('WHAT[delegation-012] only the canonical call receives the WorkRecord and all siblings refer to it', async () => {
  const owner = 'owner-canonical'
  await withSyncRuntime(owner, async runtime => {
    const order = ['first', 'second', 'third']
    const calls = order.map(call => sync.invokeBatch(runtime, owner, 'Engineer', `${call} charge`, 'canonical-run', call, order))
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 1)
    assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', 0), true)
    assert.equal(await sync.settle(runtime, owner, 'Engineer', 'canonical WorkRecord', 'canonical-run'), true)
    const [canonical, ...siblings] = await Promise.all(calls)
    assert.equal(canonical.kind, 'WorkRecord')
    assert.match(canonical.value, /canonical WorkRecord/)
    assert.deepEqual(siblings, [
      { kind: 'MergedInto', canonical: 'first' },
      { kind: 'MergedInto', canonical: 'first' },
    ])
    assert.equal(sync.promptCount(runtime, owner, 'Engineer'), 1)
  })
})

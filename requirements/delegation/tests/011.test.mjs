import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncRuntime from '../../../dist/Execution/Delegation/SyncDelegateRuntimeSurface.js'

test('WHAT[DELEG-011] SYNC_RUNTIME_ordinary_completion_settles_batch_without_return_channel', () => {
  const batch = syncRuntime.createBatch(['c1', 'c2'])
  syncRuntime.completeAssistantTurn(batch, 'done')
  assert.equal(syncRuntime.isBatchSettled(batch), true)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncRuntime from '../../../dist/Execution/Delegation/SyncDelegateRuntimeSurface.js'

test('WHAT[DELEG-008] SYNC_RUNTIME_provider_tool_call_collection_preserves_role_order', () => {
  const calls = [
    { role: 'inspector', charge: 'check1' },
    { role: 'coder', charge: 'fix1' },
  ]
  const aggregated = syncRuntime.aggregateSyncCalls(calls)
  assert.equal(aggregated[0].role, 'inspector')
  assert.equal(aggregated[1].role, 'coder')
})

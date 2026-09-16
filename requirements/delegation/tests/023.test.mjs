import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncRuntime from '../../../dist/Execution/Delegation/SyncDelegateRuntimeSurface.js'

test('WHAT[DELEG-023] SYNC_RUNTIME_transient_turn_failure_stays_child_local_until_exhausted', () => {
  const res = syncRuntime.handleTransientFailure({ retriesLeft: 2 })
  assert.equal(res.propagatedToCaller, false)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncRuntime from '../../../dist/Execution/Delegation/SyncDelegateRuntimeSurface.js'

test('WHAT[DELEG-017] SYNC_RUNTIME_work_record_is_evidence_and_does_not_transfer_authority', () => {
  const callerAuthBefore = syncRuntime.getAuthority('manager')
  syncRuntime.receiveWorkRecord('manager', { findings: [] })
  const callerAuthAfter = syncRuntime.getAuthority('manager')
  assert.deepEqual(callerAuthBefore, callerAuthAfter)
})

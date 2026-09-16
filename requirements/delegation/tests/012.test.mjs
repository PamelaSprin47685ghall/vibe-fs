import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncRuntime from '../../../dist/Execution/Delegation/SyncDelegateRuntimeSurface.js'

test('WHAT[DELEG-012] SYNC_RUNTIME_first_provider_call_receives_canonical_record_and_sibling_receives_reference', () => {
  const res = syncRuntime.distributeResults(['call-1', 'call-2'], { output: 'text-data' })
  assert.equal(res['call-1'].kind, 'CanonicalRecord')
  assert.equal(res['call-2'].kind, 'SiblingReference')
})

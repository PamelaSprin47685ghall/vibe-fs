import assert from 'node:assert/strict'
import test from 'node:test'
import * as attached from '../../../dist/Execution/Session/AttachedSessionRuntimeSurface.js'
import * as recovery from '../../../dist/Execution/Session/SessionRecoverySurface.js'

test('WHAT[MANAGED-SESSION-001] attached session runtime ensures single owner', async () => {
  const r = await attached.testSingleOwner()
  assert.equal(r.ok, true)
})

test('WHAT[MANAGED-SESSION-001] session_recovery_contract_attached_runtime_single_owner_pure_evidence', async () => {
  const r = await recovery.testAttachedRuntimeSingleOwner()
  assert.equal(r.ok, true)
})

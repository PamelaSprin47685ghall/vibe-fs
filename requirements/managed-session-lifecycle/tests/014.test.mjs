import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncDelegate from '../../../dist/Execution/Session/SyncDelegateLifecycleSurface.js'

test('WHAT[MANAGED-SESSION-014] G6_deleted_inspector_child_retires_live_binding_but_survives_for_owner_scope_close', async () => {
  const r = await syncDelegate.testDeletedInspectorChild()
  assert.equal(r.ok, true)
})

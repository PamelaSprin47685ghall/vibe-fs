import assert from 'node:assert/strict'
import test from 'node:test'
import * as finalize from '../../../dist/Execution/Session/InspectorFinalizeSettlementSurface.js'

test('WHAT[MANAGED-SESSION-022] INSPECTOR_SETTLE_uncommitted_finalize_does_not_publish_a_case', async () => {
  const r = await finalize.testUncommittedFinalizeNoPublish()
  assert.equal(r.published, false)
})

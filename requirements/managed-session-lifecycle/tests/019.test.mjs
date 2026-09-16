import assert from 'node:assert/strict'
import test from 'node:test'
import * as settlement from '../../../dist/Execution/Session/ExactExecutionSettlementSurface.js'

test('WHAT[MANAGED-SESSION-019] cancel and delete lifecycle signals settle exact terminal resources through managed-chat-execution barrier', async () => {
  const r = await settlement.testCancelDeleteSettleExact()
  assert.equal(r.settled, true)
})

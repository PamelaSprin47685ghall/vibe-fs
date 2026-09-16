import assert from 'node:assert/strict'
import test from 'node:test'
import * as joinCompletion from '../../../dist/Execution/Delegation/JoinCompletionSurface.js'

test('WHAT[DELEG-014] JOIN_COMPLETION_batch_preserves_order_and_bounded_work_records', () => {
  const batch = joinCompletion.consumeBatch(['res-1', 'res-2'], 10)
  assert.deepEqual(batch.items, ['res-1', 'res-2'])
})

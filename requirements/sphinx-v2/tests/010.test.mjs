import assert from 'node:assert/strict'
import test from 'node:test'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'

test('WHAT[sphinx-v2-010] recovery classification distinguishes unsent intent from an unrecorded Host receipt', () => {
  assert.equal(Loop.recoveryAction('DispatchPending'), 'dispatch')
  assert.equal(Loop.recoveryAction('ReceiptPending'), 'reconcile-by-intent')
  assert.equal(Loop.recoveryAction('RunningUnmarked'), 'reconcile-by-intent')
  assert.equal(Loop.recoveryMaySpend('DispatchPending'), true)
  assert.equal(Loop.recoveryMaySpend('ReceiptPending'), false)
})

test.todo('WHAT[sphinx-v2-010] actual durable append failure prevents Host dispatch and receipt-loss recovery reconciles the same intent without creating a second child')

import assert from 'node:assert/strict'
import test from 'node:test'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'

test('WHAT[sphinx-v2-034] recovery classifies an unconfirmed cancellation as awaiting terminal', () => {
  assert.equal(Loop.recoveryAction('CancelPending'), 'await-terminal')
  assert.equal(Loop.recoveryMaySpend('CancelPending'), false)
})

test.todo('WHAT[sphinx-v2-034] actual Host receipts bind work identity and cancel awaits real child resource termination while late results cannot enter semantic state')

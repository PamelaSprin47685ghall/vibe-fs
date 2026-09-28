import assert from 'node:assert/strict'
import test from 'node:test'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'

test('WHAT[sphinx-v2-021] recovery classifies accepted observations for interpretation without buying another response', () => {
  assert.equal(Loop.recoveryAction('ResultPending'), 'accept-if-valid')
  assert.equal(Loop.recoveryAction('InterpretationPending'), 'interpret')
  assert.equal(Loop.recoveryMaySpend('InterpretationPending'), false)
})

test.todo('WHAT[sphinx-v2-021] actual two-transaction recovery preserves accepted raw responses and retries only pure interpretation after plugin failure')

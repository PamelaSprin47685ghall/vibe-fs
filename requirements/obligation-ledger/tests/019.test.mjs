import assert from 'node:assert/strict'
import test from 'node:test'
import * as proj from '../../../dist/OpenCode/Host/MagicTodoProjectionSurface.js'

test('WHAT[OBLIGATION-LEDGER-019] rejects a legacy seed after the first Magic provider request', () => {
  assert.equal(proj.testRejectsLegacySeedAfterFirstRequest(), true)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as magic from '../../../dist/OpenCode/Host/MagicTodoSurface.js'

test('WHAT[OBLIGATION-LEDGER-007] admits multiple todowrite calls in one assistant message with sequential execution semantics', () => {
  assert.equal(magic.testAdmitsMultipleTodowrites(), true)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as magic from '../../../dist/OpenCode/Host/MagicTodoSurface.js'

test('WHAT[OBLIGATION-LEDGER-006] rejects blank and duplicate obligation names as call syntax', () => {
  assert.equal(magic.testRejectBlankAndDuplicateNames(), true)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'

test('WHAT[OBLIGATION-LEDGER-005] empty placeholders remain invalid while concrete planning work is legal before commitment', () => {
  assert.equal(boundary.testEmptyPlaceholdersInvalid(), true)
})

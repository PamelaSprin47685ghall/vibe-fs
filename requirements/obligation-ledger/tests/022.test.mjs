import assert from 'node:assert/strict'
import test from 'node:test'
import * as magic from '../../../dist/OpenCode/Host/MagicTodoSurface.js'

test('WHAT[OBLIGATION-LEDGER-022] blocks retirement suicide until plan commitment, not merely until any checkpoint', () => {
  assert.equal(magic.testBlocksRetirementUntilCommitment(), true)
})

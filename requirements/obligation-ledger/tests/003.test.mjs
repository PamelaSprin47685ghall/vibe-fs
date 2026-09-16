import assert from 'node:assert/strict'
import test from 'node:test'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'

test('WHAT[OBLIGATION-LEDGER-003] clean break removes the legacy todo ontology from the production graph', () => {
  assert.equal(boundary.testCleanBreakLegacyOntology(), true)
})

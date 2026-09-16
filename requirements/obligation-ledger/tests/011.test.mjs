import assert from 'node:assert/strict'
import test from 'node:test'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'

test('WHAT[OBLIGATION-LEDGER-011] next checkpoint updates Current without rollback', async () => {
  const r = await membrane.testNextCheckpointUpdatesCurrent()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-011] production checkpoint path has no reviewer settlement owner', () => {
  assert.equal(boundary.testProductionCheckpointNoReviewer(), true)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'

test('WHAT[OBLIGATION-LEDGER-014] successive checkpoints can be prepared and accepted seamlessly', async () => {
  const r = await membrane.testSuccessiveCheckpointsSeamless()
  assert.equal(r.ok, true)
})

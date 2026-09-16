import assert from 'node:assert/strict'
import test from 'node:test'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'
import * as proj from '../../../dist/OpenCode/Host/MagicTodoProjectionSurface.js'

test('WHAT[OBLIGATION-LEDGER-013] T2 prepare after T1 succeeds immediately without process review wait', async () => {
  const r = await membrane.testT2PrepareAfterT1NoWait()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-013] successive checkpoints can be prepared and accepted without review blockage', () => {
  assert.equal(proj.testSuccessiveCheckpointsNoBlockage(), true)
})

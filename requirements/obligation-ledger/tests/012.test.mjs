import assert from 'node:assert/strict'
import test from 'node:test'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'
import * as magic from '../../../dist/OpenCode/Host/MagicTodoSurface.js'

test('WHAT[OBLIGATION-LEDGER-012] T1 accept creates the checkpoint (SSOT = TodoWriteAccepted)', async () => {
  const r = await membrane.testT1AcceptCreatesCheckpoint()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-012] TodoWriteAccepted is the sole SSOT for checkpoints', () => {
  assert.equal(boundary.testTodoWriteAcceptedIsSsot(), true)
})

test('WHAT[OBLIGATION-LEDGER-012] replays an identical obligation checkpoint even while its review is outstanding (no new review from replay)', () => {
  assert.equal(magic.testReplayIdenticalCheckpoint(), true)
})

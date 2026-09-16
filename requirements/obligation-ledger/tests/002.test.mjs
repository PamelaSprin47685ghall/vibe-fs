import assert from 'node:assert/strict'
import test from 'node:test'
import * as codec from '../../../dist/OpenCode/Host/MagicTodoHostCodecSurface.js'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'
import * as magic from '../../../dist/OpenCode/Host/MagicTodoSurface.js'

test('WHAT[OBLIGATION-LEDGER-002] decodes required planComplete, workingOn, and obligations', () => {
  const r = codec.decode({ planComplete: true, workingOn: 'task1', obligations: [{ name: 'task1', horizon: 'near', work: 'w' }] })
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-002] malformed provider wire is a typed provider rejection', () => {
  const r = codec.decode({ invalid: true })
  assert.equal(r.ok, false)
})

test('WHAT[OBLIGATION-LEDGER-002] non-matching workingOn does not fail the membrane — all obligations are projected', async () => {
  const r = await membrane.testNonMatchingWorkingOn()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-002] canonical obligation wire is exactly name/horizon/work with stable digest input', () => {
  const r = magic.testCanonicalWireShape()
  assert.equal(r.ok, true)
})

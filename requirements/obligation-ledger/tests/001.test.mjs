import assert from 'node:assert/strict'
import test from 'node:test'
import * as magic from '../../../dist/OpenCode/Host/MagicTodoSurface.js'

test('WHAT[OBLIGATION-LEDGER-001] canonical obligation wire carries no provider-visible cold state', () => {
  const wire = magic.encodeCanonical([])
  assert.equal(wire.includes('status'), false)
})

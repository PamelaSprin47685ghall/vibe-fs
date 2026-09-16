import assert from 'node:assert/strict'
import test from 'node:test'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'
import * as magic from '../../../dist/OpenCode/Host/MagicTodoSurface.js'

test('WHAT[OBLIGATION-LEDGER-027] provider prose freezes progressive elaboration around workingOn', () => {
  assert.equal(boundary.testProviderProseProgressiveElaboration(), true)
})

test('WHAT[OBLIGATION-LEDGER-027] horizon is planning resolution, not provider-visible lifecycle state', () => {
  assert.equal(magic.testHorizonIsPlanningResolution(), true)
})

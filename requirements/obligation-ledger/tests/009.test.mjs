import assert from 'node:assert/strict'
import test from 'node:test'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'

test('WHAT[OBLIGATION-LEDGER-009] duplicate obligation name is the provider-red class', async () => {
  const r = await membrane.testDuplicateNameProviderRed()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-009] prepare and accept succeed directly without review runtime', async () => {
  const r = await membrane.testPrepareAcceptNoReview()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-009] failure triage keeps red for syntax and kills OpenCode on infrastructure faults', () => {
  assert.equal(boundary.testFailureTriageSyntaxVsInfrastructure(), true)
})

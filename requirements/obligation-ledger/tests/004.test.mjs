import assert from 'node:assert/strict'
import test from 'node:test'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'

test('WHAT[OBLIGATION-LEDGER-004] Manager Role Law distinguishes planning relation from entrusted mission without owning tool timing', () => {
  assert.equal(boundary.testManagerRoleLawDistinguishesPlanning(), true)
})

test('WHAT[OBLIGATION-LEDGER-004] committed mode rejects planning-only debt by consequence, not keywords', () => {
  assert.equal(boundary.testCommittedModeRejectsPlanningDebt(), true)
})

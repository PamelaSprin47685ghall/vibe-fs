import assert from 'node:assert/strict'
import test from 'node:test'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'
import * as proj from '../../../dist/OpenCode/Host/MagicTodoProjectionSurface.js'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'
import * as magic from '../../../dist/OpenCode/Host/MagicTodoSurface.js'

test('WHAT[OBLIGATION-LEDGER-010] T1 accept makes the proposed account Current immediately, before any review', async () => {
  const r = await membrane.testT1AcceptCurrentImmediately()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-010] T2 accepted account supersedes CurrentObligations', async () => {
  const r = await membrane.testT2SupersedesCurrent()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-010] Accepted supersedes Current immediately', () => {
  assert.equal(proj.testAcceptedSupersedesCurrent(), true)
})

test('WHAT[OBLIGATION-LEDGER-010] provider wording says Accepted becomes Current without reviewer settlement', () => {
  assert.equal(boundary.testAcceptedBecomesCurrentNoReviewer(), true)
})

test('WHAT[OBLIGATION-LEDGER-010] fresh admission freezes Base and Submitted without a merge preview', () => {
  assert.equal(magic.testFreshAdmissionFreezesBaseSubmitted(), true)
})

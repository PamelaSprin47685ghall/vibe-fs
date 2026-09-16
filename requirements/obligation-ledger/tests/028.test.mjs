import assert from 'node:assert/strict'
import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'
import * as refusal from '../../../dist/OpenCode/Host/MagicTodoHookRefusalSurface.js'

test('WHAT[OBLIGATION-LEDGER-028] before refuses when the durable journal is absent', async () => {
  const r = await refusal.testRefuseJournalAbsent()
  assert.equal(r.refused, true)
})

test('WHAT[OBLIGATION-LEDGER-028] before refuses when sessionID or callID is missing or blank', async () => {
  const r = await refusal.testRefuseSessionIdOrCallIdMissing()
  assert.equal(r.refused, true)
})

test('WHAT[OBLIGATION-LEDGER-028] before refuses when the snapshot port is absent', async () => {
  const r = await refusal.testRefuseSnapshotPortAbsent()
  assert.equal(r.refused, true)
})

test('WHAT[OBLIGATION-LEDGER-028] ledger fatal follows exact checkpoint settlement and one injected fuse', () => assertFatalBoundary('obligation-ledger'))

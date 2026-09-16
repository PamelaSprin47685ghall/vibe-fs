import assert from 'node:assert/strict'
import test from 'node:test'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'
import * as magicTodoLocalitySurface from '../../../dist/Mission/Obligation/Todo/MagicTodoLocalitySurface.js'

test('WHAT[OBLIGATION-LEDGER-025] accept rejects unknown physical success evidence', async () => {
  const r = await membrane.testAcceptRejectsUnknownEvidence()
  assert.equal(r.ok, false)
})

test('WHAT[OBLIGATION-LEDGER-025] openLife and compatibility injection do not wait for snapshot IO', async () => {
  const r = await membrane.testOpenLifeNoSnapshotIOWait()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-025] prepare rejects a pending ToolPart whose provider input is still empty', async () => {
  const r = await membrane.testPrepareRejectsEmptyProviderInput()
  assert.equal(r.ok, false)
})

test('WHAT[OBLIGATION-LEDGER-025] before materializes the exact provider input including planComplete and workingOn', () => {
  assert.equal(membrane.testBeforeMaterializesExactInput(), true)
})

test('WHAT[OBLIGATION-LEDGER-025] materialization fails closed when the provider input differs', () => {
  assert.equal(membrane.testMaterializationFailsClosedOnDiff(), true)
})

test('WHAT[OBLIGATION-LEDGER-025] materialized snapshot input must still match tool.execute.before args', async () => {
  const r = await membrane.testMaterializedSnapshotMatchesArgs()
  assert.equal(r.ok, true)
})

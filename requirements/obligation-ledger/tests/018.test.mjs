import assert from 'node:assert/strict'
import test from 'node:test'
import * as eventStore from '../../../dist/OpenCode/Host/MagicTodoEventStoreSurface.js'
import * as proj from '../../../dist/OpenCode/Host/MagicTodoProjectionSurface.js'
import * as workflow from '../../../dist/OpenCode/Host/ObligationLedgerWorkflowContractSurface.js'
import * as magicTodoProjectionCodecSurface from '../../../dist/Mission/Obligation/Todo/MagicTodoProjectionCodecSurface.js'

test('WHAT[OBLIGATION-LEDGER-018] persists typed prepared identity through AgentJournal and EventStore boot', async () => {
  const r = await eventStore.testPersistPreparedIdentity()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-018] checkpoint lifecycle is tagged Prepared and Accepted', () => {
  assert.equal(proj.testCheckpointLifecycleTagged(), true)
})

test('WHAT[OBLIGATION-LEDGER-018] stores typed Magic Todo bytes in the canonical Fact envelope', () => {
  assert.equal(proj.testStoresTypedBytesInEnvelope(), true)
})

test('WHAT[OBLIGATION-LEDGER-018] legacy Prepared without planComplete decodes as committed true', () => {
  assert.equal(proj.testLegacyPreparedDecodesCommittedTrue(), true)
})

test('WHAT[OBLIGATION-LEDGER-018] rejects forward Magic Todo payloads without throwing through boot fold', () => {
  assert.equal(proj.testRejectsForwardPayloadsNoThrow(), true)
})

test('WHAT[OBLIGATION-LEDGER-018] folds a typed Magic Todo envelope into the one canonical projection', () => {
  assert.equal(proj.testFoldsEnvelopeIntoProjection(), true)
})

test('WHAT[OBLIGATION-LEDGER-018] business sequencing prepares and accepts checkpoints through public membrane surface', async () => {
  const r = await workflow.testBusinessSequencing()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-018] hot-path queries use incremental projection facts on IncumbencyMagicTodoState', () => {
  assert.equal(workflow.testHotPathQueriesIncremental(), true)
})

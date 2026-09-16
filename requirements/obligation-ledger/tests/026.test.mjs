import assert from 'node:assert/strict'
import test from 'node:test'
import * as after from '../../../dist/OpenCode/Host/MagicTodoAfterSurface.js'
import * as canaries from '../../../dist/OpenCode/Host/MagicTodoHostCanariesSurface.js'
import * as membrane from '../../../dist/OpenCode/Host/MagicTodoMembraneSurface.js'

test('WHAT[OBLIGATION-LEDGER-026] after hook accepts checkpoint durably and enriches T1 revelation', async () => {
  const r = await after.testAfterHookAcceptsCheckpoint()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-026] after does not run when executor throws', async () => {
  const r = await canaries.testAfterDoesNotRunOnThrow()
  assert.equal(r.executed, false)
})

test('WHAT[OBLIGATION-LEDGER-026] after runs when executor succeeds', async () => {
  const r = await canaries.testAfterRunsOnSuccess()
  assert.equal(r.executed, true)
})

test('WHAT[OBLIGATION-LEDGER-026] accepted planComplete=false carries no T1 entrustment revelation (revelation is reserved for the first accepted true)', async () => {
  const r = await membrane.testPlanCompleteFalseNoT1Revelation()
  assert.equal(r.revealed, false)
})

test('WHAT[OBLIGATION-LEDGER-026] first accepted planComplete=true reveals entrustment in the enriched result', async () => {
  const r = await membrane.testPlanCompleteTrueRevealsEntrustment()
  assert.equal(r.revealed, true)
})

test('WHAT[OBLIGATION-LEDGER-026] enriched result for normal checkpoints contains epilogue instructions', async () => {
  const r = await membrane.testNormalCheckpointEpilogueInstructions()
  assert.equal(r.hasInstructions, true)
})

test('WHAT[OBLIGATION-LEDGER-026] prepare without open life is a structured rejection, never a provider red path', async () => {
  const r = await membrane.testPrepareWithoutOpenLifeStructuredRejection()
  assert.equal(r.structuredRejection, true)
})

test('WHAT[OBLIGATION-LEDGER-026] host before/after hook rejects without active incumbency as typed tool rejection, not infrastructure fatal', async () => {
  const r = await membrane.testRejectWithoutIncumbencyTyped()
  assert.equal(r.typedRejection, true)
})

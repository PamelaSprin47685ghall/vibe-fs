import assert from 'node:assert/strict'
import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'
import * as boundary from '../../../dist/Knowledge/Casebook/CutBoundarySurface.js'

test('WHAT[KNOWLEDGE-REUSE-013] cut_boundary_legal_commands_always_fold_per_fact_constructor', async () => {
  const r = await boundary.verifyLegalCommandsFold()
  assert.equal(r, true)
})

test('WHAT[KNOWLEDGE-REUSE-013] cut_boundary_interrupted_archive_never_leads_the_receipt', async () => {
  const r = await boundary.verifyInterruptedArchive()
  assert.equal(r, true)
})

test('WHAT[KNOWLEDGE-REUSE-013] cut_boundary_complete_observation_set_survives_refresh', async () => {
  const r = await boundary.verifyObservationSetSurvives()
  assert.equal(r, true)
})

test('WHAT[KNOWLEDGE-REUSE-013] production_store_has_no_optional_fatal_handler_path', async () => {
  const r = await boundary.verifyNoOptionalFatalHandler()
  assert.equal(r, true)
})

test('WHAT[KNOWLEDGE-REUSE-013] Casebook fatal follows durable cut settlement and one injected fuse', () => assertFatalBoundary('knowledge-reuse'))

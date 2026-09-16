import assert from 'node:assert/strict'
import test from 'node:test'
import * as mech from '../../../dist/Knowledge/Bookkeeper/MechanicalSurface.js'
import * as session from '../../../dist/Knowledge/Bookkeeper/SessionSurface.js'
import * as synth from '../../../dist/Knowledge/Bookkeeper/SynthesisSurface.js'
import * as editTool from '../../../dist/OpenCode/Tools/EditQaToolSurface.js'
import * as jsTool from '../../../dist/OpenCode/Tools/JsBookkeeperToolSurface.js'
import * as bookkeeperRefreshSurface from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRefreshSurface.js'

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_synthesis_refresh_publishes_refreshed_with_revised_a', async () => {
  const r = await mech.refreshMechanical('c1', 'q', 'old', 'new', [])
  assert.equal(r.answer, 'new')
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_mechanical_refresh_no_case_is_noop', async () => {
  const r = await mech.refreshMissing('c-none')
  assert.equal(r, null)
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_mechanical_refresh_missing_file_still_publishes', async () => {
  const r = await mech.refreshWithMissingFile('c1', 'q', 'ans')
  assert.equal(r.ok, true)
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_create_child_once_per_refresh_via_js_bookkeeper', async () => {
  const count = await session.bookkeeperInvocationCount('c1')
  assert.equal(count, 1)
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_injected_synthesizer_error_keeps_old_case', async () => {
  const r = await synth.synthesizeWithError('c1', 'old-ans')
  assert.equal(r.answer, 'old-ans')
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_synthesizer_runs_once_per_stale_refresh', async () => {
  const count = await synth.runCount('c1')
  assert.equal(count, 1)
})

test('WHAT[KNOWLEDGE-REUSE-006] CASE006_bookkeeper_provider_contract_is_one_program', () => {
  assert.equal(editTool.isSingleProgramContract(), true)
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_surface_is_program_only_and_has_case_sdk', () => {
  assert.equal(jsTool.hasProgramCapability(), true)
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_program_reshapes_question_and_answer_atomically', async () => {
  const r = await jsTool.executeAtomic('c1', 'new-q', 'new-a')
  assert.equal(r.ok, true)
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_zero_mutation_is_legal', async () => {
  const r = await jsTool.executeNoop('c1')
  assert.equal(r.ok, true)
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_duplicate_set_rolls_back_the_whole_program', async () => {
  const r = await jsTool.executeDuplicateSet('c1')
  assert.equal(r.ok, false)
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_program_failure_rolls_back_staged_mutation', async () => {
  const r = await jsTool.executeFailingProgram('c1')
  assert.equal(r.ok, false)
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_unbound_session_cannot_change_a_case', async () => {
  const r = await jsTool.executeUnbound('c1')
  assert.equal(r.ok, false)
})

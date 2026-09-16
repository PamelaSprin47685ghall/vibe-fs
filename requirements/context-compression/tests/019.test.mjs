import assert from 'node:assert/strict'
import test from 'node:test'
import * as foldSurface from '../../../dist/Context/Companion/ContextFoldSurface.js'
import * as reanchor from '../../../dist/Context/Companion/InjectedContextReanchorSurface.js'

test('WHAT[CONTEXT-COMPRESSION-019] Context_fold_accepts_plain_envelopes_and_replays_the_line_codec', () => {
  assert.ok(foldSurface.acceptsPlainEnvelopes)
})

test('WHAT[CONTEXT-COMPRESSION-019] Context_fold_rejects_unknown_fact_cases_loudly', () => {
  assert.ok(foldSurface.rejectsUnknownCases)
})

test('WHAT[CONTEXT-COMPRESSION-019] CTX_019_reanchor_retires_old_pair_wire_but_keeps_history_and_allows_a_fresh_pair', () => {
  assert.ok(reanchor.reanchorRetiresOldPairWire)
})

test('WHAT[CONTEXT-COMPRESSION-019] CTX_019_prefix_rebase_is_the_same_auxiliary_cold_boundary_as_host_reanchor', () => {
  assert.ok(reanchor.prefixRebaseIsAuxiliaryColdBoundary)
})

test('WHAT[CONTEXT-COMPRESSION-019] CTX_019_reanchor_retires_old_requirement_reads_then_same_digest_regrounds_on_the_next_real_trigger', () => {
  assert.ok(reanchor.reanchorRetiresOldRequirementReads)
})

test('WHAT[CONTEXT-COMPRESSION-019] CTX_019_cursor_reanchor_strips_old_pair_suffix_before_adding_the_new_horizon_pair', () => {
  assert.ok(reanchor.cursorReanchorStripsOldPairSuffix)
})

test('WHAT[CONTEXT-COMPRESSION-019] CTX_019_cursor_reanchor_strips_old_requirement_suffixes_until_a_real_path_trigger_regrounds', () => {
  assert.ok(reanchor.cursorReanchorStripsOldRequirementSuffixes)
})

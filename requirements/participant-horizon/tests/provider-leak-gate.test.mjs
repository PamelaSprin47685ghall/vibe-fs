/**
 * ARCH-016 Gate B — provider leak gate (no dist).
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  FORBIDDEN_DTO_PATTERNS,
  FORBIDDEN_TOKENS,
  scanEntries,
  scanRepo,
  scanText,
} from '../../../scripts/checks/provider-leak-gate.mjs'

const CLEAN_HORIZON = `
module HorizonTool =
    let private lineForHandle handle _ =
        sprintf "# %s is still away." "Coder"

    let spec scope =
        { Name = "horizon"
          Description = "Orient to what remains at your horizon."
          Arguments = []
          Execute = fun _ _ _ -> task { return ToolHostCodec.tomlObjectWithInstructions ["# Nothing"] [] } }
`

const LEAKY_JOIN = `
module JoinResultRenderer =
    let renderInterrupted reason =
        field "status" (str "interrupted")
        field "pty_id" (str payload.PtyId)
        SessionId.value sid
`

test('WHAT[PARTICIPANT-HORIZON-002] gate_b_documents_forbidden_machine_tokens', () => {
  assert.ok(FORBIDDEN_TOKENS.includes('SessionId'))
  assert.ok(FORBIDDEN_TOKENS.includes('pty_id'))
})

test('WHAT[PARTICIPANT-HORIZON-003] gate_b_documents_forbidden_dto_patterns', () => {
  assert.ok(FORBIDDEN_DTO_PATTERNS.some((p) => p.id === 'field-status'))
})

test('WHAT[PARTICIPANT-HORIZON-001] gate_b_clean_horizon_fixture_is_green', () => {
  assert.equal(scanText('HorizonTool.fs', CLEAN_HORIZON).length, 0)
})

test('WHAT[PARTICIPANT-HORIZON-003] gate_b_leaky_renderer_fixture_is_red_for_dto_fields', () => {
  const hits = scanText('JoinResultRenderer.fs', LEAKY_JOIN)
  assert.ok(hits.some((h) => h.id === 'field-status'))
})

test('WHAT[PARTICIPANT-HORIZON-002] gate_b_leaky_renderer_fixture_is_red_for_machine_tokens', () => {
  const hits = scanText('JoinResultRenderer.fs', LEAKY_JOIN)
  assert.ok(hits.some((h) => h.id.startsWith('token:SessionId') || h.id === 'token:pty_id'))
})

test('WHAT[PARTICIPANT-HORIZON-002] gate_b_scan_entries_aggregates', () => {
  const hits = scanEntries([
    { file: 'HorizonTool.fs', text: CLEAN_HORIZON },
    { file: 'JoinResultRenderer.fs', text: LEAKY_JOIN },
  ])
  assert.ok(hits.length >= 2)
})

test('WHAT[PARTICIPANT-HORIZON-002] gate_b_repo_scan_without_baseline_is_zero', () => {
  const result = scanRepo(process.cwd())
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  assert.deepEqual(result.counts, {})
})

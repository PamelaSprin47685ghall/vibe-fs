/**
 * ARCH-016 Gate B — provider leak gate (no dist).
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  FORBIDDEN_DTO_PATTERNS,
  FORBIDDEN_TOKENS,
  compareBaseline,
  countByFile,
  scanEntries,
  scanRepo,
  scanText,
} from '../../../scripts/checks/provider-leak-gate.mjs'

const CLEAN_HORIZON = `
module ListTool =
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

test('gate_b_documents_forbidden_vocabulary', () => {
  assert.ok(FORBIDDEN_TOKENS.includes('SessionId'))
  assert.ok(FORBIDDEN_TOKENS.includes('pty_id'))
  assert.ok(FORBIDDEN_DTO_PATTERNS.some((p) => p.id === 'field-status'))
})

test('gate_b_clean_horizon_fixture_is_green', () => {
  assert.equal(scanText('ListTool.fs', CLEAN_HORIZON).length, 0)
})

test('gate_b_leaky_renderer_fixture_is_red', () => {
  const hits = scanText('JoinResultRenderer.fs', LEAKY_JOIN)
  assert.ok(hits.some((h) => h.id === 'field-status'))
  assert.ok(hits.some((h) => h.id.startsWith('token:SessionId') || h.id === 'token:pty_id'))
})

test('gate_b_scan_entries_aggregates', () => {
  const hits = scanEntries([
    { file: 'ListTool.fs', text: CLEAN_HORIZON },
    { file: 'JoinResultRenderer.fs', text: LEAKY_JOIN },
  ])
  assert.ok(hits.length >= 2)
})

test('gate_b_baseline_ratchet_blocks_regression', () => {
  const current = countByFile(scanEntries([{ file: 'JoinResultRenderer.fs', text: LEAKY_JOIN }]))
  const { ok, regressions } = compareBaseline({ 'JoinResultRenderer.fs': 1 }, current)
  assert.equal(ok, false)
  assert.ok(regressions[0].current > regressions[0].baseline)
})

test('gate_b_repo_scan_with_baseline_is_green', () => {
  const baseline = JSON.parse(
    readFileSync(new URL('../../../scripts/checks/provider-leak-gate-baseline.json', import.meta.url), 'utf8'),
  )
  const result = scanRepo(process.cwd(), { baseline })
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('gate_b_new_leak_without_baseline_is_red', () => {
  const result = scanRepo(process.cwd())
  assert.equal(result.ok, false, 'Join migration debt must remain visible without baseline')
  assert.ok(result.violations.length > 0)
})

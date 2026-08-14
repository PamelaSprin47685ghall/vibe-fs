/**
 * ARCH-016 Gate E — provider prose ownership ratchet (no dist).
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  PROVIDER_PROSE_SCAN_ROOTS,
  compareBaseline,
  countByFile,
  generateBaseline,
  isProviderProseLiteral,
  scanEntries,
  scanRepo,
  scanText,
} from '../../../scripts/checks/provider-prose-ownership.mjs'

const GREEN_FIXTURE = `
module CleanTool =
    let name = "horizon"
    let path = "resources/provider/tool/horizon/en.md"
    let wire = "exit_code"
    let verdict = "PERFECT"
    let role = "Coder"
`

const RED_FIXTURE = `
module LeakyPrompt =
    let english = "Continue the same work from the evidence already before you."
    let chinese = "请继续完成同一项工作。"
    let tech = "deadline_seconds"
`

test('gate_e_scan_roots_cover_gate0_owners', () => {
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('Nudge.fs') || p.endsWith('RuntimeNudge.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('Challenge.fs') || p.endsWith('ReviewChallenge.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('HorizonTool.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('JoinResultRenderer.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('Surface.fs') || p.endsWith('MagicTodoSurface.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('ToolRegistry.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('FileMutationTools.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('ToolHost.fs') || p.endsWith('JsToolHost.fs')))
})

test('gate_e_green_fixture_is_zero_hits', () => {
  assert.equal(scanText('CleanTool.fs', GREEN_FIXTURE).length, 0)
})

test('gate_e_red_fixture_counts_english_and_chinese', () => {
  const hits = scanText('LeakyPrompt.fs', RED_FIXTURE)
  assert.ok(hits.length >= 2)
  assert.ok(hits.some((h) => h.text.includes('Continue the same')))
  assert.ok(hits.some((h) => h.text.includes('请继续')))
  assert.ok(!hits.some((h) => h.text === 'deadline_seconds'))
})

test('gate_e_heuristic_excludes_paths_and_identifiers', () => {
  assert.equal(isProviderProseLiteral('resources/provider/role/manager/en.md'), false)
  assert.equal(isProviderProseLiteral('world_lock'), false)
  assert.equal(isProviderProseLiteral('BackgroundJoinGuard'), false)
  assert.equal(isProviderProseLiteral('horizon'), false)
  assert.equal(isProviderProseLiteral('{{name}}'), false)
  assert.equal(isProviderProseLiteral('{{a}} {{b}} {{c}}'), false)
})

test('gate_e_baseline_ratchet_blocks_regression', () => {
  const current = countByFile(scanEntries([{ file: 'LeakyPrompt.fs', text: RED_FIXTURE }]))
  const { ok, regressions } = compareBaseline({ 'LeakyPrompt.fs': 1 }, current)
  assert.equal(ok, false)
  assert.ok(regressions[0].current > regressions[0].baseline)
})

test('gate_e_repo_scan_with_generated_baseline_is_green', () => {
  const baseline = generateBaseline(process.cwd())
  const result = scanRepo(process.cwd(), { baseline })
  assert.equal(result.ok, true, JSON.stringify(result.hits, null, 2))
})

test('gate_e_zero_hits_is_closed', () => {
  const result = scanRepo(process.cwd())
  assert.equal(result.ok, true, JSON.stringify(result.hits, null, 2))
  assert.deepEqual(result.counts, {})
})

test('gate_e_committed_baseline_matches_repo', () => {
  const baseline = JSON.parse(
    readFileSync(
      new URL('../../../scripts/checks/provider-prose-ownership-baseline.json', import.meta.url),
      'utf8',
    ),
  )
  const result = scanRepo(process.cwd(), { baseline })
  assert.equal(result.ok, true, JSON.stringify(result.hits, null, 2))
})

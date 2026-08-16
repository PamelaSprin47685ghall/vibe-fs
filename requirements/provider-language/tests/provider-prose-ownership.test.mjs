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

test('WHAT[PROVIDER-LANGUAGE-009] Gate E scan roots cover Gate 0 owners', () => {
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('Nudge.fs') || p.endsWith('RuntimeNudge.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('Challenge.fs') || p.endsWith('ReviewChallenge.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('HorizonTool.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('JoinResultRenderer.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('Surface.fs') || p.endsWith('MagicTodoSurface.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('ToolRegistry.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('FileMutationTools.fs')))
  assert.ok(PROVIDER_PROSE_SCAN_ROOTS.some((p) => p.endsWith('ToolHost.fs') || p.endsWith('JsToolHost.fs')))
})

test('WHAT[PROVIDER-LANGUAGE-009] green fixture is zero hits', () => {
  assert.equal(scanText('CleanTool.fs', GREEN_FIXTURE).length, 0)
})

test('WHAT[PROVIDER-LANGUAGE-009] red fixture counts english and chinese literals', () => {
  const hits = scanText('LeakyPrompt.fs', RED_FIXTURE)
  assert.ok(hits.length >= 2)
  assert.ok(hits.some((h) => h.text.includes('Continue the same')))
  assert.ok(hits.some((h) => h.text.includes('请继续')))
  assert.ok(!hits.some((h) => h.text === 'deadline_seconds'))
})

test('WHAT[PROVIDER-LANGUAGE-005] heuristic excludes paths and identifiers from Class A', () => {
  assert.equal(isProviderProseLiteral('resources/provider/role/manager/en.md'), false)
  assert.equal(isProviderProseLiteral('world_lock'), false)
  assert.equal(isProviderProseLiteral('BackgroundJoinGuard'), false)
  assert.equal(isProviderProseLiteral('horizon'), false)
  assert.equal(isProviderProseLiteral('{{name}}'), false)
  assert.equal(isProviderProseLiteral('{{a}} {{b}} {{c}}'), false)
})

test('WHAT[PROVIDER-LANGUAGE-009] baseline ratchet blocks regression', () => {
  const current = countByFile(scanEntries([{ file: 'LeakyPrompt.fs', text: RED_FIXTURE }]))
  const { ok, regressions } = compareBaseline({ 'LeakyPrompt.fs': 1 }, current)
  assert.equal(ok, false)
  assert.ok(regressions[0].current > regressions[0].baseline)
})

test('WHAT[PROVIDER-LANGUAGE-009] repo scan with generated baseline is green', () => {
  const baseline = generateBaseline(process.cwd())
  const result = scanRepo(process.cwd(), { baseline })
  assert.equal(result.ok, true, JSON.stringify(result.hits, null, 2))
})

test('WHAT[PROVIDER-LANGUAGE-009] zero hits is closed', () => {
  const result = scanRepo(process.cwd())
  assert.equal(result.ok, true, JSON.stringify(result.hits, null, 2))
  assert.deepEqual(result.counts, {})
})

test('WHAT[PROVIDER-LANGUAGE-009] committed baseline matches repo', () => {
  const baseline = JSON.parse(
    readFileSync(
      new URL('../../../scripts/checks/provider-prose-ownership-baseline.json', import.meta.url),
      'utf8',
    ),
  )
  const result = scanRepo(process.cwd(), { baseline })
  assert.equal(result.ok, true, JSON.stringify(result.hits, null, 2))
})

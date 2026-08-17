/**
 * ARCH-016 Gate E — provider prose ownership ratchet (no dist).
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  PROVIDER_PROSE_SCAN_ROOTS,
  collectEntries,
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

test('WHAT[PROVIDER-LANGUAGE-009] zero hits is closed', () => {
  const result = scanRepo(process.cwd())
  assert.equal(result.ok, true, JSON.stringify(result.hits, null, 2))
  assert.deepEqual(result.counts, {})
})

test('WHAT[PROVIDER-LANGUAGE-009] missing configured scan root fails closed with path context', () => {
  // A required owner surface that no longer exists must abort the gate, not
  // be silently skipped — otherwise provider prose outside the surviving
  // roots passes undetected.
  assert.throws(
    () => collectEntries(process.cwd(), ['does/not/exist.fs']),
    /scan root missing on disk: does\/not\/exist\.fs/,
  )
})

test('WHAT[PROVIDER-LANGUAGE-009] unreadable scan root fails closed with path context', () => {
  // readFileSync on a directory throws EISDIR — a deterministic, cross-platform
  // read failure that exercises the unreadable-root path without chmod.
  assert.throws(
    () => collectEntries(process.cwd(), ['src/Wanxiangshu']),
    /scan root unreadable: src\/Wanxiangshu/,
  )
})

test('WHAT[PROVIDER-LANGUAGE-009] scanRepo propagates missing-root failure (gate cannot pass)', () => {
  // The gate entry point must surface the failure, never swallow it into ok.
  assert.throws(
    () => scanRepo(process.cwd(), { roots: ['does/not/exist.fs'] }),
    /scan root missing on disk: does\/not\/exist\.fs/,
  )
})

test('WHAT[PROVIDER-LANGUAGE-009] every configured scan root exists on disk', () => {
  // Guards against a root being added to the config but never created; if one
  // is missing the gate would have skipped it silently under the old policy.
  const entries = collectEntries(process.cwd())
  assert.equal(entries.length, PROVIDER_PROSE_SCAN_ROOTS.length)
})

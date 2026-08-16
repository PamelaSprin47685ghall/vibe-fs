/**
 * office-capability — role-law contract tests for propositions whose proof
 * anchors live in the provider Role Laws (bilingual docs under
 * resources/provider/role/*). These are live-repo canaries: each proposition
 * is proven by asserting its entitled consequence / non-consequence against
 * the actual role documents, in both locales.
 *
 * Imports: node builtins only (contract §4.6).
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const readRole = (role, locale) => readFileSync(join(ROOT, 'resources/provider/role', role, locale), 'utf8')

test('WHAT[OFF-004] capability_is_consequence_model_not_tool_whitelist_transcription', () => {
  // The manager law names offices by what they can establish or change (the
  // consequence), never by the instruments inside them (ARCH-017: consequence
  // model, not a whitelist transcription).
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /Know another office by its promises, not by its keys/i)
  assert.match(en, /not by the instruments hidden[\s\S]{0,20}inside it/i)
  assert.match(zh, /应看它的承诺，而不是它的钥匙/)
  assert.match(zh, /而不是看它内部隐藏着什么工具/)
})

test('WHAT[OFF-007] manager_has_no_personal_repository_witness', () => {
  // Manager's entitled consequence is coordination; it must not establish
  // repository facts with its own hands (ROLE_SEMANTIC_ANCHORS.manager
  // no-personal-repository-witness projection, both locales).
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /do not establish repository facts with your own hands/i)
  assert.match(zh, /不以自己的双手去建立 repository 事实/)
})

test('WHAT[OFF-011] reviewer_consequence_is_readonly_judgement_not_repair', () => {
  // Reviewer may inspect (read-only) and judge; it must not repair the work
  // it judges (AGENT-014).
  const en = readRole('reviewer', 'en.md')
  const zh = readRole('reviewer', 'zh-CN.md')
  assert.match(en, /Inspect the work independently where the judgment requires it/i)
  assert.match(en, /You do not repair the work you judge/i)
  assert.match(zh, /当 judgment 需要时，独立检查工作/)
  assert.match(zh, /你不修复由你判断的工作/)
})

test('WHAT[OFF-012] orchestrator_commissions_manager_roads_not_phases', () => {
  // Orchestrator commissions independent destinations (each with its own
  // Manager), never technical phases or machine machinery (AGENT-015).
  const en = readRole('orchestrator', 'en.md')
  const zh = readRole('orchestrator', 'zh-CN.md')
  assert.match(en, /You commission independent destinations, not technical phases/i)
  assert.match(en, /give it its own Manager and its own road/i)
  assert.match(zh, /你委派的是彼此独立的目的地，而不是技术阶段/)
  assert.match(zh, /给它自己的 Manager，给它自己的道路/)
})

test('WHAT[OFF-013] browser_consequence_is_external_facts_with_provenance_not_local_repo', () => {
  // Browser establishes facts from the external world with provenance; the
  // local repository is not web evidence (ARCH-017 Browser row).
  const en = readRole('browser', 'en.md')
  const zh = readRole('browser', 'zh-CN.md')
  assert.match(en, /establish facts from the Internet and[\s\S]{0,60}other external web sources/i)
  assert.match(en, /Do not inspect the local repository/i)
  assert.match(zh, /从 Internet 与其他外部 web sources 建立事实/)
  assert.match(zh, /本地 repository，就去检查/)
})

test('WHAT[OFF-014] inquiry_consequence_is_semantic_understanding_not_evidence_minting', () => {
  // Inquiry contributes semantic intelligence; thinking twice does not turn a
  // thought into evidence — it must not mint evidence from ideas (ARCH-017
  // Inquiry row).
  const en = readRole('inquiry', 'en.md')
  const zh = readRole('inquiry', 'zh-CN.md')
  assert.match(en, /semantic intelligence/i)
  assert.match(en, /A thought does not become an observation by being thought twice/i)
  assert.match(zh, /语义智能/)
  assert.match(zh, /一个想法不会因为被想了两次，就变成 observation/)
})

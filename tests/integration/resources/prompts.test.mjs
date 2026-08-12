// tests/integration/resources/prompts.test.mjs — package prompt load contract.
//
// 10 role system prompts load via PromptResources / RuntimeResources from
// resources/provider/role/<name>/{en.md,zh-CN.md} (legacy resources/prompts/ fallback).
// Path resolution is import.meta.url-relative, not cwd.
// G3: Student/Teacher prompts deleted with the roles (AGENT-002 twenty-agent baseline).
// GrandRewrite: Role Law prompts; no tdd/list/fork-manager/verdict legacy contracts.
//
// Not discovered by tests/unit/runner.mjs. Run standalone:
//   node --test tests/integration/resources/prompts.test.mjs
// (requires dist/ built; import through tests/unit/support/domain.mjs facade)

import assert from 'node:assert/strict'
import test from 'node:test'
import { promptResources, providerLanguage, providerResources, runtimeResources } from '../../unit/support/domain.mjs'

const PROMPT_FIELDS = [
  'ManagerSystemPrompt',
  'CoderSystemPrompt',
  'DevopsSystemPrompt',
  'InspectorSystemPrompt',
  'ReviewerSystemPrompt',
  'BrowserSystemPrompt',
  'InquirySystemPrompt',
  'OrchestratorSystemPrompt',
  'DistillerSystemPrompt',
  'BloggerSystemPrompt',
]

const assertTenNonEmpty = (catalog, label) => {
  for (const field of PROMPT_FIELDS) {
    const text = catalog[field]
    assert.equal(typeof text, 'string', `${label}: ${field} must be string`)
    assert.ok(text.trim().length > 0, `${label}: ${field} must be non-empty`)
  }
  assert.equal(PROMPT_FIELDS.length, 10)
  assert.equal(catalog.StudentSystemPrompt, undefined, `${label}: StudentSystemPrompt must be absent`)
  assert.equal(catalog.TeacherSystemPrompt, undefined, `${label}: TeacherSystemPrompt must be absent`)
}

const assertNoLegacyToolVocabulary = (text, label) => {
  assert.doesNotMatch(text, /\bfork-manager\b/)
  assert.doesNotMatch(text, /\bblog\b/)
  assert.doesNotMatch(text, /\bfork-pty\b/)
  assert.doesNotMatch(text, /\bedit-qa\b/)
  assert.doesNotMatch(text, /\bmeditator\b/i)
  assert.doesNotMatch(text, /\bRole\.Executor\b|\bfast-executor\b|\bdeep-executor\b/i)
}

test('AGENT_002_resource_ten_prompts_load_via_PromptResources', () => {
  const catalog = promptResources.load()
  assertTenNonEmpty(catalog, 'PromptResources.load')
})

test('AGENT_002_resource_ten_prompts_load_via_RuntimeResources', () => {
  const bundle = runtimeResources.load()
  assertTenNonEmpty(bundle.Prompts, 'RuntimeResources.load().Prompts')
  assert.ok(bundle.EnforcerRules !== undefined)
})

test('ENFORCER_resource_prompts_load_independent_of_process_cwd', () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    const catalog = promptResources.load()
    assertTenNonEmpty(catalog, 'PromptResources.load after chdir(/)')
    const bundle = runtimeResources.load()
    assertTenNonEmpty(bundle.Prompts, 'RuntimeResources.load after chdir(/)')
  } finally {
    process.chdir(previous)
  }
})

test('PROMPT_manager_blindplan_and_horizon_vocabulary', () => {
  const text = promptResources.load().ManagerSystemPrompt
  assert.match(text, /Planning Table|planning table/i)
  assert.match(text, /\bhorizon\b/)
  assert.match(text, /\btodowrite\b/)
  assert.match(text, /\bfork\b/)
  assert.doesNotMatch(text, /carrying one task|Born with a Task/i)
  assertNoLegacyToolVocabulary(text, 'Manager')
})

test('PROMPT_orchestrator_commission_roads_vocabulary', () => {
  const text = promptResources.load().OrchestratorSystemPrompt
  assert.match(text, /\bcommission\b/)
  assert.match(text, /independent road|independent roads/i)
  assert.doesNotMatch(text, /fork-manager|job_id|worktree/i)
  assertNoLegacyToolVocabulary(text, 'Orchestrator')
})

test('PROMPT_coder_mutation_without_tdd_contract', () => {
  const text = promptResources.load().CoderSystemPrompt
  assert.match(text, /Mutation|mutation/i)
  assert.match(text, /\binspect\b/)
  assert.match(text, /bash-honeypot|shell/i)
  assert.doesNotMatch(text, /\btdd\b/i)
  assertNoLegacyToolVocabulary(text, 'Coder')
})

test('PROMPT_devops_engine_room_and_terminals', () => {
  const text = promptResources.load().DevopsSystemPrompt
  assert.match(text, /Engine Room|engine room/i)
  assert.match(text, /open-terminal|send-terminal|read-terminal|signal-terminal/)
  assert.match(text, /\brun\b/)
  assert.match(text, /establish-behavior|repair-behavior/)
  assert.doesNotMatch(text, /\btdd\b/i)
  assertNoLegacyToolVocabulary(text, 'DevOps')
})

test('PROMPT_reviewer_judge_without_formal_report_schema', () => {
  const text = promptResources.load().ReviewerSystemPrompt
  assert.match(text, /\bjudge\b/)
  assert.match(text, /Examiner|Ledger|material/i)
  assert.doesNotMatch(text, /### Evaluation Report|Formal Report Format/i)
  assertNoLegacyToolVocabulary(text, 'Reviewer')
})

test('PROMPT_inspector_evidence_funnel_and_query_shell', () => {
  const text = promptResources.load().InspectorSystemPrompt
  assert.match(text, /Evidence|witness/i)
  assert.match(text, /query-shell/)
  assert.match(text, /Do not compile|do not compile|without changing/i)
  assert.doesNotMatch(text, /There is no bash in Inspector/i)
  assertNoLegacyToolVocabulary(text, 'Inspector')
})

test('PROMPT_inquiry_inspect_only_v1', () => {
  const text = promptResources.load().InquirySystemPrompt
  assert.match(text, /Inquiry|inquiry/i)
  assert.match(text, /\binspect\b/)
  assert.doesNotMatch(text, /Sphinx Kernel|Sphinx contribution|semantic contribution protocol/i)
  assertNoLegacyToolVocabulary(text, 'Inquiry')
})

test('PROMPT_017_loadForLanguage_zh_cn_non_empty_and_differs_from_en', () => {
  const en = promptResources.loadForLanguage(providerLanguage.english)
  const zh = promptResources.loadForLanguage(providerLanguage.simplifiedChinese)
  assertTenNonEmpty(zh, 'PromptResources.loadForLanguage(zh-CN)')
  assert.notEqual(zh.ManagerSystemPrompt, en.ManagerSystemPrompt)
  assert.match(zh.ManagerSystemPrompt, /\bhorizon\b/)
  assert.match(zh.ManagerSystemPrompt, /\btodowrite\b/)
  assert.match(zh.ManagerSystemPrompt, /\bfork\b/)
})

test('PROMPT_017_provider_tree_has_role_law_parity', () => {
  for (const semantic of [
    'role/manager',
    'role/coder',
    'role/devops',
    'role/inspector',
    'role/reviewer',
    'role/browser',
    'role/inquiry',
    'role/orchestrator',
    'role/distiller',
    'role/blogger',
    'role/bookkeeper',
  ]) {
    providerResources.requireLanguagePair(semantic)
    assert.ok(providerResources.exists(providerLanguage.english, semantic), semantic)
    assert.ok(providerResources.exists(providerLanguage.simplifiedChinese, semantic), semantic)
  }
})

test('PROMPT_blogger_chronicle_occurrence_model', () => {
  const text = promptResources.load().BloggerSystemPrompt
  assert.match(text, /\bchronicle\b/)
  assert.match(text, /occurrence|tip/i)
  assert.doesNotMatch(text, /\bevidence\b/i)
  assertNoLegacyToolVocabulary(text, 'Blogger')
})

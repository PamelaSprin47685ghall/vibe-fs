// Provider system composition contract: Common Law → Role Law → Office Library.
// Tool inventories belong to the generated tool surface, never to Role Law.

import assert from 'node:assert/strict'
import test from 'node:test'
import { promptResources, providerLanguage, providerResources, runtimeResources } from '../../../../verification-system/tests/support/domain.mjs'

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
    assert.equal(typeof catalog[field], 'string', `${label}: ${field}`)
    assert.ok(catalog[field].trim().length > 0, `${label}: ${field} non-empty`)
  }
  assert.equal(catalog.StudentSystemPrompt, undefined)
  assert.equal(catalog.TeacherSystemPrompt, undefined)
}

const inOrder = (text, needles) => {
  let cursor = -1
  for (const needle of needles) {
    const next = text.indexOf(needle, cursor + 1)
    assert.ok(next > cursor, `expected ${JSON.stringify(needle)} after offset ${cursor}`)
    cursor = next
  }
}

const ROLE_PATHS = [
  'role/manager', 'role/coder', 'role/devops', 'role/inspector', 'role/reviewer',
  'role/browser', 'role/inquiry', 'role/orchestrator', 'role/distiller', 'role/blogger', 'role/bookkeeper',
]
const SHARED_PATHS = [
  'world/common-law', 'library/ingress', 'library/closing', 'library/kolmogorov',
  'library/scarcity', 'library/reviewer/quality-ledger',
]

// Tool-shaped inventory remains forbidden. Conceptual words such as Fission
// (one life, several presents) are Role Law craft and must stay teachable.
const forbiddenRoleToolInventory = /\b(?:todowrite|open-terminal|send-terminal|read-terminal|signal-terminal|query-shell|sphinx_start|sphinx_resume|js-[a-z-]+)\b/i

const hanRatio = (text) => {
  const han = (text.match(/[\u3400-\u9fff]/g) ?? []).length
  const latinWords = (text.match(/[A-Za-z]{4,}/g) ?? []).length
  return han / Math.max(1, latinWords)
}

test('PROMPT_resources_load_from_package_independent_of_cwd', () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    assertTenNonEmpty(promptResources.load(), 'PromptResources')
    assertTenNonEmpty(runtimeResources.load().Prompts, 'RuntimeResources')
  } finally {
    process.chdir(previous)
  }
})

test('PROMPT_composition_common_law_role_law_then_inherited_library', () => {
  const prompts = promptResources.load()
  inOrder(prompts.ManagerSystemPrompt, ['# Common Law', '# Management', '# Office Library', '# The Kolmogorov Book', '# The Book of Scarcity'])
  inOrder(prompts.CoderSystemPrompt, ['# Common Law', '# Mutation', '# Office Library', '# The Kolmogorov Book'])
  inOrder(prompts.ReviewerSystemPrompt, ['# Common Law', '# Judgment', '# Office Library', '# The Kolmogorov Book', "# The Examiner's Ledger"])
  inOrder(prompts.InspectorSystemPrompt, ['# Common Law', '# Evidence', '# Office Library', '# The Book of Scarcity'])
  inOrder(prompts.DevopsSystemPrompt, ['# Common Law', '# The Engine Room', '# Office Library', '# The Book of Scarcity'])

  for (const field of ['OrchestratorSystemPrompt', 'BrowserSystemPrompt', 'InquirySystemPrompt', 'DistillerSystemPrompt', 'BloggerSystemPrompt']) {
    assert.match(prompts[field], /^# Common Law/)
    assert.doesNotMatch(prompts[field], /# Office Library/)
  }
})

test('PROMPT_role_laws_are_identity_not_tool_inventory', () => {
  for (const path of ROLE_PATHS) {
    const law = providerResources.readText(providerLanguage.english, path)
    assert.doesNotMatch(law, forbiddenRoleToolInventory, path)
  }

  const inquiry = providerResources.readText(providerLanguage.english, 'role/inquiry')
  assert.match(inquiry, /Inspector/)
  assert.doesNotMatch(inquiry, /sphinx_start|sphinx_resume/)
})

test('PROMPT_017_world_role_library_all_have_en_zh_parity', () => {
  for (const semantic of [...ROLE_PATHS, ...SHARED_PATHS]) {
    providerResources.requireLanguagePair(semantic)
    assert.ok(providerResources.exists(providerLanguage.english, semantic), `${semantic}: en`)
    assert.ok(providerResources.exists(providerLanguage.simplifiedChinese, semantic), `${semantic}: zh-CN`)
  }
})

test('PROMPT_017_zh_cn_is_authored_chinese_not_an_english_copy', () => {
  const en = promptResources.loadForLanguage(providerLanguage.english)
  const zh = promptResources.loadForLanguage(providerLanguage.simplifiedChinese)
  assertTenNonEmpty(zh, 'zh-CN')

  for (const field of PROMPT_FIELDS) {
    assert.notEqual(zh[field], en[field], field)
    assert.match(zh[field], /[\u3400-\u9fff]/, field)
    assert.ok(hanRatio(zh[field]) > 1.5, `${field}: Chinese prose should dominate long English words`)
  }
})

test('PROMPT_bookkeeper_inherits_common_law_and_casebook_role_law', () => {
  const en = promptResources.loadBookkeeperSystemFor(providerLanguage.english)
  const zh = promptResources.loadBookkeeperSystemFor(providerLanguage.simplifiedChinese)
  inOrder(en, ['# Common Law', '# The Casebook'])
  assert.match(zh, /^# 共同法/)
  assert.match(zh, /# Casebook/)
})

test('PROMPT_no_legacy_provider_ontology_in_composed_prompts', () => {
  for (const [field, text] of Object.entries(promptResources.load())) {
    assert.doesNotMatch(text, /\bfork-manager\b|\bfork-pty\b|\bedit-qa\b|\bmeditator\b|\bfast-executor\b|\bdeep-executor\b/i, field)
  }
})

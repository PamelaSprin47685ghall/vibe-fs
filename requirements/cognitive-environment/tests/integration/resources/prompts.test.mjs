// Provider system composition contract: Common Law → Role Law → Office Library.
// Tool inventories belong to the generated tool surface, never to Role Law.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as promptResources from '../../../../../dist/Resources/PromptSurface.js'
import * as providerLanguage from '../../../../../dist/Participant/Provider/LanguageSurface.js'

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

const PROMPT_FIELDS = [
  'ManagerSystemPrompt',
  'EngineerSystemPrompt',
  'DevopsSystemPrompt',
  'OrchestratorSystemPrompt',
  'BloggerSystemPrompt',
]

// Retired offices keep historical decoders, never a live prompt slot.
const RETIRED_PROMPT_FIELDS = [
  'CoderSystemPrompt',
  'InspectorSystemPrompt',
  'BrowserSystemPrompt',
  'InquirySystemPrompt',
  'DistillerSystemPrompt',
]

const promptEntries = (catalog) => PROMPT_FIELDS.map((field) => [field, catalog[field]])

const assertActiveNonEmpty = (catalog, label) => {
  for (const field of PROMPT_FIELDS) {
    assert.equal(typeof catalog[field], 'string', `${label}: ${field}`)
    assert.ok(catalog[field].trim().length > 0, `${label}: ${field} non-empty`)
  }
  for (const field of RETIRED_PROMPT_FIELDS) {
    assert.equal(catalog[field], undefined, `${label}: retired ${field} must not exist`)
  }
  assert.equal(catalog.ReviewerSystemPrompt, undefined)
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
  'role/manager', 'role/engineer', 'role/devops', 'role/orchestrator', 'role/blogger', 'role/bookkeeper',
]
const RETIRED_ROLE_PATHS = [
  'role/coder', 'role/inspector', 'role/browser', 'role/inquiry', 'role/distiller',
]
const SHARED_PATHS = [
  'world/common-law', 'library/ingress', 'library/closing', 'library/kolmogorov',
  'library/scarcity',
]

// Tool-shaped inventory remains forbidden. Conceptual words such as Fission
// (one life, several presents) are Role Law craft and must stay teachable.
const forbiddenRoleToolInventory = /\b(?:todowrite|open-terminal|send-terminal|read-terminal|signal-terminal|query-shell|sphinx_start|sphinx_resume|js-[a-z-]+)\b/i

const hanRatio = (text) => {
  const han = (text.match(/[\u3400-\u9fff]/g) ?? []).length
  const latinWords = (text.match(/[A-Za-z]{4,}/g) ?? []).length
  return han / Math.max(1, latinWords)
}

test('WHAT[DISTRIBUTION-002] PROMPT_resources_load_from_package_independent_of_cwd', () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    assertActiveNonEmpty(promptResources.load(), 'PromptResources')
    assertActiveNonEmpty(promptResources.runtimeLoad().Prompts, 'RuntimeResources')
  } finally {
    process.chdir(previous)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-003] PROMPT_composition_common_law_role_law_then_inherited_library', () => {
  const prompts = promptResources.load()
  inOrder(prompts.ManagerSystemPrompt, ['# # Common Law', '# # Management', '# # Office Library', '# # The Kolmogorov Book', '# # The Book of Scarcity'])
  inOrder(prompts.EngineerSystemPrompt, ['# # Common Law', '# # Engineering', '# # Office Library', '# # The Kolmogorov Book'])
  assert.doesNotMatch(prompts.EngineerSystemPrompt, /# # The Book of Scarcity/)
  inOrder(prompts.DevopsSystemPrompt, ['# # Common Law', '# # The Engine Room', '# # Office Library', '# # The Kolmogorov Book', '# # The Book of Scarcity'])

  for (const field of ['OrchestratorSystemPrompt', 'BloggerSystemPrompt']) {
    assert.match(prompts[field], /^# # Common Law/)
    assert.doesNotMatch(prompts[field], /# # Office Library/)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-001] PROMPT_common_law_discourages_ascii_art_in_both_languages', () => {
  const en = promptResources.loadForLanguage(english)
  const zh = promptResources.loadForLanguage(simplifiedChinese)

  for (const [, text] of promptEntries(en)) assert.match(text, /avoid ASCII art where possible/)
  for (const [, text] of promptEntries(zh)) assert.match(text, /输出尽量不要使用 ASCII art/)
})

test('WHAT[COGNITIVE-ENVIRONMENT-004] PROMPT_role_laws_are_identity_not_tool_inventory', () => {
  for (const path of ROLE_PATHS) {
    const law = providerLanguage.readText(english, path)
    assert.doesNotMatch(law, forbiddenRoleToolInventory, path)
  }

  for (const path of RETIRED_ROLE_PATHS) {
    assert.equal(providerLanguage.exists(english, path), false, `${path} must not ship a Role Law`)
    assert.equal(providerLanguage.exists(simplifiedChinese, path), false, `${path} must not ship a Role Law`)
  }
})

test('WHAT[PROVIDER-LANGUAGE-006] PROMPT_017_world_role_library_all_have_en_zh_parity', () => {
  for (const semantic of [...ROLE_PATHS, ...SHARED_PATHS]) {
    providerLanguage.requireLanguagePair(semantic)
    assert.ok(providerLanguage.exists(english, semantic), `${semantic}: en`)
    assert.ok(providerLanguage.exists(simplifiedChinese, semantic), `${semantic}: zh-CN`)
  }
})

test('WHAT[PROVIDER-LANGUAGE-006] PROMPT_017_zh_cn_is_authored_chinese_not_an_english_copy', () => {
  const en = promptResources.loadForLanguage(english)
  const zh = promptResources.loadForLanguage(simplifiedChinese)
  assertActiveNonEmpty(zh, 'zh-CN')

  for (const field of PROMPT_FIELDS) {
    assert.notEqual(zh[field], en[field], field)
    assert.match(zh[field], /[\u3400-\u9fff]/, field)
    assert.ok(hanRatio(zh[field]) > 1.5, `${field}: Chinese prose should dominate long English words`)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-003] PROMPT_bookkeeper_inherits_common_law_and_casebook_role_law', () => {
  const en = promptResources.loadBookkeeperSystemFor(english)
  const zh = promptResources.loadBookkeeperSystemFor(simplifiedChinese)
  inOrder(en, ['# # Common Law', '# # The Casebook'])
  assert.match(zh, /^# # 共同法/)
  assert.match(zh, /# # 案例簿/)
})

test('WHAT[COGNITIVE-ENVIRONMENT-005] PROMPT_no_legacy_provider_ontology_in_composed_prompts', () => {
  for (const [field, text] of promptEntries(promptResources.load())) {
    assert.doesNotMatch(text, /\bfork-manager\b|\bfork-pty\b|\bedit-qa\b|\bmeditator\b|\bfast-executor\b|\bdeep-executor\b/i, field)
  }
})

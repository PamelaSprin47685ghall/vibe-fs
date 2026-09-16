import assert from 'node:assert/strict'
import test from 'node:test'
import * as promptResources from '../../../dist/Resources/PromptSurface.js'
import * as providerLanguage from '../../../dist/Participant/Provider/LanguageSurface.js'

const LANGUAGE = 'English'
const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

const ROLE_PATHS = [
  'role/manager', 'role/coder', 'role/devops', 'role/inspector',
  'role/browser', 'role/inquiry', 'role/orchestrator', 'role/distiller', 'role/blogger', 'role/bookkeeper',
]
const SHARED_PATHS = [
  'world/common-law', 'library/ingress', 'library/closing', 'library/kolmogorov',
  'library/scarcity',
]

const PROMPT_FIELDS = [
  'ManagerSystemPrompt',
  'CoderSystemPrompt',
  'DevopsSystemPrompt',
  'InspectorSystemPrompt',
  'BrowserSystemPrompt',
  'InquirySystemPrompt',
  'OrchestratorSystemPrompt',
  'DistillerSystemPrompt',
  'BloggerSystemPrompt',
]

const assertNineNonEmpty = (catalog, label) => {
  for (const field of PROMPT_FIELDS) {
    assert.equal(typeof catalog[field], 'string', `${label}: ${field}`)
    assert.ok(catalog[field].trim().length > 0, `${label}: ${field} non-empty`)
  }
  assert.equal(catalog.ReviewerSystemPrompt, undefined)
  assert.equal(catalog.StudentSystemPrompt, undefined)
  assert.equal(catalog.TeacherSystemPrompt, undefined)
}

const hanRatio = (text) => {
  const han = (text.match(/[\u3400-\u9fff]/g) ?? []).length
  const latinWords = (text.match(/[A-Za-z]{4,}/g) ?? []).length
  return han / Math.max(1, latinWords)
}

test('WHAT[COGNITIVE-ENVIRONMENT-003] CE_prompt_015_canonical_composition_common_law_role_law_office_library', () => {
  const catalog = promptResources.loadForLanguage(LANGUAGE)
  const coder = catalog.CoderSystemPrompt
  assert.match(coder, /You awaken in a world already in motion/, 'Common Law must lead')
  assert.match(coder, /written world/i, 'Role Law must follow')
  assert.match(coder, /one more inheritance/, 'Office Library ingress must be present for book-owning offices')
  assert.match(coder, /The Kolmogorov Book/, 'inherited volume must be composed')
  assert.match(coder, /These books are older than this assignment/, 'Office Library closing must close the composition')

  const manager = catalog.ManagerSystemPrompt
  assert.match(manager, /The Book of Scarcity/, 'Manager inherits scarcity volume')
  assert.doesNotMatch(coder, /The Book of Scarcity/, 'Coder does not inherit the Manager-only volume')
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
  assertNineNonEmpty(zh, 'zh-CN')

  for (const field of PROMPT_FIELDS) {
    assert.notEqual(zh[field], en[field], field)
    assert.match(zh[field], /[\u3400-\u9fff]/, field)
    assert.ok(hanRatio(zh[field]) > 1.5, `${field}: Chinese prose should dominate long English words`)
  }
})

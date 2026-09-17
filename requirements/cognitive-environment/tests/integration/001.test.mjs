import assert from 'node:assert/strict'
import test from 'node:test'
import * as promptResources from '../../../../dist/Resources/PromptSurface.js'
import * as providerLanguage from '../../../../dist/Participant/Provider/LanguageSurface.js'

const english = 'English'

const simplifiedChinese = 'SimplifiedChinese'

const PROMPT_FIELDS = [
  'ManagerSystemPrompt',
  'EngineerSystemPrompt',
  'DevopsSystemPrompt',
  'OrchestratorSystemPrompt',
  'BloggerSystemPrompt',
]

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

const forbiddenRoleToolInventory = /\b(?:todowrite|open-terminal|send-terminal|read-terminal|signal-terminal|query-shell|sphinx_start|sphinx_resume|js-[a-z-]+)\b/i

const hanRatio = (text) => {
  const han = (text.match(/[\u3400-\u9fff]/g) ?? []).length
  const latinWords = (text.match(/[A-Za-z]{4,}/g) ?? []).length
  return han / Math.max(1, latinWords)
}

test('WHAT[COGNITIVE-ENVIRONMENT-001] PROMPT_common_law_discourages_ascii_art_in_both_languages', () => {
  const en = promptResources.loadForLanguage(english)
  const zh = promptResources.loadForLanguage(simplifiedChinese)

  for (const [, text] of promptEntries(en)) assert.match(text, /avoid ASCII art where possible/)
  for (const [, text] of promptEntries(zh)) assert.match(text, /输出尽量不要使用 ASCII art/)
})

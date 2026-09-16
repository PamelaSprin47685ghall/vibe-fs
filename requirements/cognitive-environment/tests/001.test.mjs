import assert from 'node:assert/strict'
import test from 'node:test'
import * as promptResources from '../../../dist/Resources/PromptSurface.js'

const LANGUAGE = 'English'

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

test('WHAT[COGNITIVE-ENVIRONMENT-001] CE_prompt_015_one_system_prompt_per_role', () => {
  const prompts = promptResources.allForLanguage(LANGUAGE)
  assert.equal(prompts.length, 9, 'exactly one canonical system prompt per public office (Reviewer merged into Manager under Relay)')
})

test('WHAT[DISTRIBUTION-002] PROMPT_resources_load_from_package_independent_of_cwd', () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    assertNineNonEmpty(promptResources.load(), 'PromptResources')
    assertNineNonEmpty(promptResources.runtimeLoad().Prompts, 'RuntimeResources')
  } finally {
    process.chdir(previous)
  }
})

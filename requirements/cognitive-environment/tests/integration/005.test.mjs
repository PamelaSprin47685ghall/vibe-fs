import assert from 'node:assert/strict'
import test from 'node:test'
import * as promptResources from '../../../../dist/Resources/PromptSurface.js'

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

const promptEntries = (catalog) => PROMPT_FIELDS.map((field) => [field, catalog[field]])

test('WHAT[COGNITIVE-ENVIRONMENT-005] PROMPT_no_legacy_provider_ontology_in_composed_prompts', () => {
  for (const [field, text] of promptEntries(promptResources.load())) {
    assert.doesNotMatch(text, /\bfork-manager\b|\bfork-pty\b|\bedit-qa\b|\bmeditator\b|\bfast-executor\b|\bdeep-executor\b/i, field)
  }
})

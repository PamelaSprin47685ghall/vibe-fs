// tests/integration/resources/prompts.test.mjs — package prompt load contract.
//
// 10 role system prompts under resources/prompts/*-system.md load via
// PromptResources / RuntimeResources; path is import.meta.url-relative, not cwd.
//
// Not discovered by tests/unit/runner.mjs. Run standalone:
//   node --test tests/integration/resources/prompts.test.mjs
// (requires dist/ built; import through tests/unit/support/domain.mjs facade)

import assert from 'node:assert/strict'
import test from 'node:test'
import { promptResources, runtimeResources } from '../../unit/support/domain.mjs'

const PROMPT_FIELDS = [
  'ManagerSystemPrompt',
  'CoderSystemPrompt',
  'DevopsSystemPrompt',
  'InspectorSystemPrompt',
  'ReviewerSystemPrompt',
  'BrowserSystemPrompt',
  'MeditatorSystemPrompt',
  'OrchestratorSystemPrompt',
  'ExecutorSystemPrompt',
  'BloggerSystemPrompt',
]

const assertTenNonEmpty = (catalog, label) => {
  for (const field of PROMPT_FIELDS) {
    const text = catalog[field]
    assert.equal(typeof text, 'string', `${label}: ${field} must be string`)
    assert.ok(text.trim().length > 0, `${label}: ${field} must be non-empty`)
  }
  assert.equal(PROMPT_FIELDS.length, 10)
}

test('ENFORCER_resource_ten_prompts_load_via_PromptResources', () => {
  const catalog = promptResources.load()
  assertTenNonEmpty(catalog, 'PromptResources.load')
})

test('ENFORCER_resource_ten_prompts_load_via_RuntimeResources', () => {
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

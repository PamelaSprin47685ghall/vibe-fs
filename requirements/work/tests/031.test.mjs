import assert from 'node:assert/strict'
import test from 'node:test'
import * as prompts from '../../../dist/Resources/PromptSurface.js'

test('WHAT[work-031] the prompt catalog carries a localized PlanSystemPrompt for real system transform wiring', () => {
  // WHAT[work-031]: ProviderSystemTransform must really assemble the plan
  // prompt; the catalog is the assembly source, so it must expose the plan
  // system prompt in both languages.
  for (const language of ['en', 'zh-CN']) {
    const catalog = prompts.loadForLanguage(language)
    assert.ok(catalog != null, `loadForLanguage(${language}) must return a catalog`)
    const planPrompt = catalog.PlanSystemPrompt
    assert.ok(
      typeof planPrompt === 'string' && planPrompt.trim().length > 0,
      `the ${language} prompt catalog must carry a non-empty PlanSystemPrompt`,
    )
  }
})

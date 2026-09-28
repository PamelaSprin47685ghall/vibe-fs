import assert from 'node:assert/strict'
import test from 'node:test'
import * as prompts from '../../../dist/Resources/PromptSurface.js'

test('WHAT[cognitive-environment-005] delivered bilingual role prompts contain no fast/deep machine-routing names', () => {
  for (const language of ['English', 'SimplifiedChinese']) {
    for (const prompt of [...prompts.allForLanguage(language), prompts.loadBookkeeperSystemFor(language)]) {
      assert.doesNotMatch(prompt, /\b(?:fast|deep)-[a-z][a-z0-9-]*/i)
    }
  }
})

test.todo('WHAT[cognitive-environment-005] real model-binding changes preserve the complete Role Law and self-model; retired tier input checks only cover planner compatibility (GAP-076)')

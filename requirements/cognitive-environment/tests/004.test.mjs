import assert from 'node:assert/strict'
import test from 'node:test'
import * as promptResources from '../../../dist/Resources/PromptSurface.js'

const LANGUAGE = 'English'

test('WHAT[COGNITIVE-ENVIRONMENT-004] CE_prompt_015_system_prompt_does_not_enumerate_runtime_tool_surface', () => {
  for (const prompt of promptResources.allForLanguage(LANGUAGE)) {
    assert.doesNotMatch(prompt, /\b(fast|deep)-[a-z]+/, 'machine binding names must not appear in system prompts')
    assert.doesNotMatch(prompt, /auto-injected|ToolPermission/, 'runtime tool-surface machinery must not enter Role Law')
  }
})

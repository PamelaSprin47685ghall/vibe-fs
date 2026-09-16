import assert from 'node:assert/strict'
import test from 'node:test'
import * as promptResources from '../../../../dist/Resources/PromptSurface.js'

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

test('WHAT[COGNITIVE-ENVIRONMENT-001] PROMPT_common_law_discourages_ascii_art_in_both_languages', () => {
  const en = promptResources.loadForLanguage(english)
  const zh = promptResources.loadForLanguage(simplifiedChinese)

  for (const text of Object.values(en)) assert.match(text, /avoid ASCII art where possible/)
  for (const text of Object.values(zh)) assert.match(text, /输出尽量不要使用 ASCII art/)
})

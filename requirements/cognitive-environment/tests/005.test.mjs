import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as promptResources from '../../../dist/Resources/PromptSurface.js'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LANGUAGE = 'English'

test('WHAT[COGNITIVE-ENVIRONMENT-005] CE_prompt_015_no_tier_split_duplicates', () => {
  const prompts = promptResources.allForLanguage(LANGUAGE)
  const unique = new Set(prompts)
  assert.equal(unique.size, 9, 'no two offices share a prompt; no tier-split duplicates')
})

test('WHAT[COGNITIVE-ENVIRONMENT-005] CE_role_law_is_enduring_self_model_without_tier_split_or_hidden_orchestration', () => {
  for (const role of ['coder', 'manager', 'devops', 'inspector', 'orchestrator', 'blogger', 'distiller']) {
    for (const locale of ['en', 'zh-CN']) {
      const text = read(`resources/provider/role/${role}/${locale}.md`)
      assert.doesNotMatch(text, /\b(fast|deep)-[a-z]+/, `${role}/${locale}.md must not expose tier identity`)
      assert.doesNotMatch(
        text,
        /\breviewer\b|\bbarrier\b|\b2N\b|\bcohort\b|confirmation rounds/i,
        `${role}/${locale}.md must not teach hidden review orchestration`,
      )
    }
  }
})

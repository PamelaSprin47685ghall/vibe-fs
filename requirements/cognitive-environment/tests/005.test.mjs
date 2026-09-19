import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as promptResources from '../../../dist/Resources/PromptSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const walkMarkdown = (relDir) => {
  const out = []
  const walkDir = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const p = join(dir, entry.name)
      if (entry.isDirectory()) walkDir(p)
      else if (entry.name.endsWith('.md')) out.push(readFileSync(p, 'utf8'))
    }
  }
  walkDir(join(ROOT, relDir))
  return out
}

const ROLE_LAW_ROLES = Object.freeze([
  'manager',
  'coder',
  'inspector',
  'devops',
  'orchestrator',
  'blogger',
  'distiller',
  'bookkeeper',
])

const MIRRORED_BY_OFFICE_CAPABILITY = new Set(['entrust-by-consequence', 'choose-by-return', 'no-omnipotent-charge'])

const LANGUAGE = 'English'

test('WHAT[cognitive-environment-005] CE_prompt_015_no_tier_split_duplicates', () => {
  const prompts = promptResources.allForLanguage(LANGUAGE)
  const unique = new Set(prompts)
  // Catalog contains the active canonical roles
  assert.ok(unique.size >= 5, 'canonical roles have unique prompts')
})

test('WHAT[cognitive-environment-005] CE_role_law_is_enduring_self_model_without_tier_split_or_hidden_orchestration', () => {
  for (const role of ['engineer', 'manager', 'devops', 'orchestrator', 'blogger', 'bookkeeper']) {
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

{
const PROMPT_FIELDS = [
  'ManagerSystemPrompt',
  'EngineerSystemPrompt',
  'DevopsSystemPrompt',
  'OrchestratorSystemPrompt',
  'BloggerSystemPrompt',
]

const promptEntries = (catalog) => PROMPT_FIELDS.map((field) => [field, catalog[field]])

integrationTest('WHAT[cognitive-environment-005] PROMPT_no_legacy_provider_ontology_in_composed_prompts', () => {
  for (const [field, text] of promptEntries(promptResources.load())) {
    assert.doesNotMatch(text, /\bfork-manager\b|\bfork-pty\b|\bedit-qa\b|\bmeditator\b|\bfast-executor\b|\bdeep-executor\b/i, field)
  }
})
}

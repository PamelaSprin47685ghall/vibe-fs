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

test('WHAT[cognitive-environment-001] CE_prompt_015_one_system_prompt_per_role', () => {
  const prompts = promptResources.allForLanguage(LANGUAGE)
  assert.equal(prompts.length, 5, 'canonical system prompts in catalog')
})

{
const providerLanguage = await import("../../../dist/Participant/Provider/LanguageSurface.js");

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

const PROMPT_FIELDS = [
  'ManagerSystemPrompt',
  'EngineerSystemPrompt',
  'DevopsSystemPrompt',
  'OrchestratorSystemPrompt',
  'BloggerSystemPrompt',
]

const promptEntries = (catalog) => PROMPT_FIELDS.map((field) => [field, catalog[field]])

integrationTest('WHAT[cognitive-environment-001] PROMPT_common_law_discourages_ascii_art_in_both_languages', () => {
  const en = promptResources.loadForLanguage(english)
  const zh = promptResources.loadForLanguage(simplifiedChinese)

  for (const [, text] of promptEntries(en)) assert.match(text, /avoid ASCII art where possible/)
  for (const [, text] of promptEntries(zh)) assert.match(text, /输出尽量不要使用 ASCII art/)
})
}

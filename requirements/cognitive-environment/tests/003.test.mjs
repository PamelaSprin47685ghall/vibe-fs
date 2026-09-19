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

test('WHAT[cognitive-environment-003] CE_prompt_015_canonical_composition_common_law_role_law_office_library', () => {
  const catalog = promptResources.loadForLanguage(LANGUAGE)
  const coder = catalog.EngineerSystemPrompt
  assert.match(coder, /You awaken in a world (?:already in motion|that is already up and running)/, 'Common Law must lead')
  assert.match(coder, /local facts investigation|local investigation and source work/i, 'the unified engineering Role Law must follow')
  assert.match(coder, /one more inheritance/, 'Office Library ingress must be present for book-owning offices')
  assert.match(coder, /The Kolmogorov Book/, 'inherited volume must be composed')
  assert.match(coder, /These books are older than this assignment/, 'Office Library closing must close the composition')

  const manager = catalog.ManagerSystemPrompt
  assert.match(manager, /The Book of Scarcity/, 'Manager inherits scarcity volume')
  assert.doesNotMatch(coder, /The Book of Scarcity/, 'Coder does not inherit the Manager-only volume')
})

{
const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

const inOrder = (text, needles) => {
  let cursor = -1
  for (const needle of needles) {
    const next = text.indexOf(needle, cursor + 1)
    assert.ok(next > cursor, `expected ${JSON.stringify(needle)} after offset ${cursor}`)
    cursor = next
  }
}

integrationTest('WHAT[cognitive-environment-003] PROMPT_composition_common_law_role_law_then_inherited_library', () => {
  const prompts = promptResources.load()
  inOrder(prompts.ManagerSystemPrompt, ['# # Common Law', '# # Management', '# # Office Library', '# # The Kolmogorov Book', '# # The Book of Scarcity'])
  inOrder(prompts.EngineerSystemPrompt, ['# # Common Law', '# # Engineering', '# # Office Library', '# # The Kolmogorov Book'])
  assert.doesNotMatch(prompts.EngineerSystemPrompt, /# # The Book of Scarcity/)
  inOrder(prompts.DevopsSystemPrompt, ['# # Common Law', '# # The Engine Room', '# # Office Library', '# # The Kolmogorov Book', '# # The Book of Scarcity'])

  for (const field of ['OrchestratorSystemPrompt', 'BloggerSystemPrompt']) {
    assert.match(prompts[field], /^# # Common Law/)
    assert.doesNotMatch(prompts[field], /# # Office Library/)
  }
})

integrationTest('WHAT[cognitive-environment-003] PROMPT_bookkeeper_inherits_common_law_and_casebook_role_law', () => {
  const en = promptResources.loadBookkeeperSystemFor(english)
  const zh = promptResources.loadBookkeeperSystemFor(simplifiedChinese)
  inOrder(en, ['# # Common Law', '# # The Casebook'])
  assert.match(zh, /^# # 共同法/)
  assert.match(zh, /# # 案例簿/)
})
}
